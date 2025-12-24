using System;
using Isopoh.Cryptography.Argon2;
using Npgsql;

var connectionString = "Host=localhost;Database=myface;Username=postgres;Password=postgres";
var password = "password";
var hash = Argon2.Hash(password);

Console.WriteLine($"Setting password to 'password' for test1, test2, and MyFace...");

using var conn = new NpgsqlConnection(connectionString);
conn.Open();

using var cmd = new NpgsqlCommand(@"
    UPDATE ""Users"" 
    SET ""PasswordHash"" = @hash 
    WHERE ""LoginName"" IN ('test1', 'test2', 'MyFace')", conn);
cmd.Parameters.AddWithValue("hash", hash);

var affected = cmd.ExecuteNonQuery();
Console.WriteLine($"Updated {affected} accounts.");
Console.WriteLine("Done!");
