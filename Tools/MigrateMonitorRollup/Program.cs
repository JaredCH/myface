using Microsoft.EntityFrameworkCore;
using MyFace.Data;
using MyFace.Services;

var connectionString = Environment.GetEnvironmentVariable("MYFACE_TEST_CONNECTION")
    ?? "Host=localhost;Database=myface_test;Username=postgres;Password=postgres";

Console.WriteLine("Monitor Link Rollup - Data Migration Script");
Console.WriteLine("=============================================");
Console.WriteLine($"Connection: {connectionString.Split(';')[0..2].Aggregate((a,b) => a + ";" + b)}...");
Console.WriteLine();

var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
optionsBuilder.UseNpgsql(connectionString);

await using var context = new ApplicationDbContext(optionsBuilder.Options);

Console.WriteLine("Step 1: Populating CanonicalName and NormalizedKey for existing records...");

var allRecords = await context.OnionStatuses.ToListAsync();
var updated = 0;

foreach (var record in allRecords)
{
    if (string.IsNullOrEmpty(record.CanonicalName) || string.IsNullOrEmpty(record.NormalizedKey))
    {
        record.CanonicalName = LinkNormalizationService.NormalizeToCanonical(record.Name);
        record.NormalizedKey = LinkNormalizationService.GenerateNormalizedKey(record.Name);
        updated++;
    }
}

if (updated > 0)
{
    await context.SaveChangesAsync();
    Console.WriteLine($"  ✓ Updated {updated} records with canonical names");
}
else
{
    Console.WriteLine($"  ✓ All records already have canonical names");
}

Console.WriteLine();
Console.WriteLine("Step 2: Identifying and linking mirror URLs...");

// Group by normalized key + category to find potential mirrors
var grouped = allRecords
    .Where(r => !r.IsMirror && r.ParentId == null) // Only process primary services
    .GroupBy(r => (r.NormalizedKey ?? string.Empty) + "|" + r.Description)
    .Where(g => g.Count() > 1) // Only groups with potential duplicates
    .ToList();

var mirrorsLinked = 0;

foreach (var group in grouped)
{
    var services = group.OrderBy(s => s.Id).ToList();
    var primary = services[0]; // First by ID is the primary
    
    Console.WriteLine($"  Found duplicate group: {primary.CanonicalName} ({services.Count} entries)");
    
    for (int i = 1; i < services.Count; i++)
    {
        var mirror = services[i];
        mirror.ParentId = primary.Id;
        mirror.IsMirror = true;
        mirror.MirrorPriority = i;
        mirrorsLinked++;
        
        Console.WriteLine($"    - Linked {mirror.OnionUrl} as mirror #{i}");
    }
}

if (mirrorsLinked > 0)
{
    await context.SaveChangesAsync();
    Console.WriteLine($"  ✓ Linked {mirrorsLinked} mirrors");
}
else
{
    Console.WriteLine($"  ✓ No new mirrors to link");
}

Console.WriteLine();
Console.WriteLine("Step 3: Generating summary statistics...");

var primaryCount = await context.OnionStatuses.CountAsync(o => !o.IsMirror);
var mirrorCount = await context.OnionStatuses.CountAsync(o => o.IsMirror);
var servicesWithMirrors = await context.OnionStatuses
    .Include(o => o.Mirrors)
    .Where(o => !o.IsMirror && o.Mirrors.Any())
    .CountAsync();

Console.WriteLine($"  Primary services: {primaryCount}");
Console.WriteLine($"  Mirror URLs: {mirrorCount}");
Console.WriteLine($"  Services with mirrors: {servicesWithMirrors}");
Console.WriteLine();
Console.WriteLine("✓ Migration complete!");
