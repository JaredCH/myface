using Npgsql;
using MyFace.Services;

var connectionString = Environment.GetEnvironmentVariable("MYFACE_CONNECTION")
    ?? "Host=localhost;Database=myface;Username=postgres;Password=postgres";

var seeds = OnionMonitorSeedData.All;

Console.WriteLine($"Resetting onion monitor links (total {seeds.Count}).");

await using var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync();
await using var transaction = await connection.BeginTransactionAsync();

await using (var truncate = new NpgsqlCommand("TRUNCATE \"OnionStatuses\" RESTART IDENTITY;", connection, transaction))
{
    await truncate.ExecuteNonQueryAsync();
}

const string insertSql = @"
    INSERT INTO ""OnionStatuses""
        (""Name"", ""Description"", ""OnionUrl"", ""Status"", ""LastChecked"", ""ResponseTime"", ""ReachableAttempts"", ""TotalAttempts"", ""AverageLatency"", ""ClickCount"")
    VALUES
        (@name, @category, @url, @status, NULL, NULL, 0, 0, NULL, 0);
";

var inserted = 0;

foreach (var seed in seeds)
{
    await using var cmd = new NpgsqlCommand(insertSql, connection, transaction);
    cmd.Parameters.AddWithValue("name", seed.Name);
    cmd.Parameters.AddWithValue("category", seed.Category);
    cmd.Parameters.AddWithValue("url", seed.Url);
    cmd.Parameters.AddWithValue("status", "Unknown");

    inserted += await cmd.ExecuteNonQueryAsync();
}

await transaction.CommitAsync();

Console.WriteLine($"Inserted {inserted} monitor links and reset counters.");
