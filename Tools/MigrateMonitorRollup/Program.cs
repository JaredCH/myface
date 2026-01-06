using Microsoft.EntityFrameworkCore;
using MyFace.Data;
using MyFace.Services;

var connectionString = Environment.GetEnvironmentVariable("MYFACE_CONNECTION")
    ?? Environment.GetEnvironmentVariable("MYFACE_TEST_CONNECTION")
    ?? "Host=localhost;Database=myface_test;Username=postgres;Password=postgres";

static string DescribeConnection(string raw)
{
    var parts = raw.Split(';', StringSplitOptions.RemoveEmptyEntries);
    return parts.Length switch
    {
        >= 2 => string.Join(';', parts.Take(2)) + ";…",
        > 0 => parts[0] + ";…",
        _ => raw
    };
}

Console.WriteLine("Monitor Link Rollup - Data Migration Script");
Console.WriteLine("=============================================");
Console.WriteLine($"Connection: {DescribeConnection(connectionString)}");
Console.WriteLine();

var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
optionsBuilder.UseNpgsql(connectionString);

await using var context = new ApplicationDbContext(optionsBuilder.Options);

Console.WriteLine("Step 1: Recomputing CanonicalName and NormalizedKey for all records...");

var allRecords = await context.OnionStatuses.ToListAsync();
var canonicalUpdates = 0;

foreach (var record in allRecords)
{
    var canonicalName = LinkNormalizationService.NormalizeToCanonical(record.Name);
    var normalizedKey = LinkNormalizationService.GenerateNormalizedKey(canonicalName);

    if (!string.Equals(record.CanonicalName, canonicalName, StringComparison.Ordinal))
    {
        record.CanonicalName = canonicalName;
        canonicalUpdates++;
    }

    if (!string.Equals(record.NormalizedKey, normalizedKey, StringComparison.Ordinal))
    {
        record.NormalizedKey = normalizedKey;
        canonicalUpdates++;
    }
}

if (canonicalUpdates > 0)
{
    await context.SaveChangesAsync();
    Console.WriteLine($"  ✓ Recomputed metadata on {canonicalUpdates} fields");
}
else
{
    Console.WriteLine("  ✓ Canonical metadata already aligned");
}

Console.WriteLine();
Console.WriteLine("Step 2: Normalizing categories and linking mirror URLs...");

var grouped = allRecords
    .Where(r => !string.IsNullOrEmpty(r.NormalizedKey))
    .GroupBy(r => r.NormalizedKey!, StringComparer.OrdinalIgnoreCase)
    .ToList();

var mirrorsLinked = 0;
var categoriesUpdated = 0;

foreach (var group in grouped)
{
    var services = group
        .OrderBy(s => s.IsMirror ? 1 : 0)
        .ThenBy(s => s.MirrorPriority)
        .ThenBy(s => s.Id)
        .ToList();

    if (services.Count == 0)
    {
        continue;
    }

    var preferredCategory = services
        .Select(s => (s.Description ?? string.Empty).Trim())
        .Where(s => !string.IsNullOrWhiteSpace(s))
        .GroupBy(s => s, StringComparer.OrdinalIgnoreCase)
        .OrderByDescending(g => g.Count())
        .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
        .Select(g => g.Key)
        .FirstOrDefault() ?? (services[0].Description ?? "Other");

    var primary = services[0];
    var canonicalLabel = string.IsNullOrWhiteSpace(primary.CanonicalName) ? primary.Name : primary.CanonicalName;

    Console.WriteLine($"  Group: {canonicalLabel} ({services.Count} entries)");

    for (var i = 0; i < services.Count; i++)
    {
        var service = services[i];
        var desiredMirrorFlag = i > 0;
        var desiredParent = desiredMirrorFlag ? primary.Id : (int?)null;

        if (!string.Equals(service.Description, preferredCategory, StringComparison.Ordinal))
        {
            service.Description = preferredCategory;
            categoriesUpdated++;
        }

        if (service.IsMirror != desiredMirrorFlag ||
            service.ParentId != desiredParent ||
            service.MirrorPriority != i)
        {
            service.IsMirror = desiredMirrorFlag;
            service.ParentId = desiredParent;
            service.MirrorPriority = i;
            if (desiredMirrorFlag)
            {
                mirrorsLinked++;
            }
        }
    }
}

if (categoriesUpdated > 0 || mirrorsLinked > 0)
{
    await context.SaveChangesAsync();
}

Console.WriteLine(categoriesUpdated > 0
    ? $"  ✓ Normalized categories on {categoriesUpdated} rows"
    : "  ✓ Categories already consistent");

Console.WriteLine(mirrorsLinked > 0
    ? $"  ✓ Linked or refreshed {mirrorsLinked} mirrors"
    : "  ✓ Mirror relationships already up to date");

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
