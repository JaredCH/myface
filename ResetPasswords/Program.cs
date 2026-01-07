using System;
using System.Collections.Generic;
using System.Linq;
using Isopoh.Cryptography.Argon2;
using Npgsql;

var connectionString = "Host=localhost;Database=myface;Username=postgres;Password=postgres";

string[] loginNames;
string password;

try
{
    (loginNames, password) = ParseArguments(args);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    PrintUsage();
    return;
}

if (loginNames.Length == 0)
{
    PrintUsage();
    return;
}

var hash = Argon2.Hash(password);
Console.WriteLine($"Updating passwords for: {string.Join(", ", loginNames)}");

using var conn = new NpgsqlConnection(connectionString);
conn.Open();

using var cmd = new NpgsqlCommand(@"
    UPDATE ""Users"" 
    SET ""PasswordHash"" = @hash 
    WHERE ""LoginName"" = ANY(@logins)", conn);
cmd.Parameters.AddWithValue("hash", hash);
cmd.Parameters.AddWithValue("logins", loginNames);

var affected = cmd.ExecuteNonQuery();
Console.WriteLine($"✓ Updated {affected} account(s) successfully!");
Console.WriteLine($"All specified passwords are now set to: {password}");

static (string[] LoginNames, string Password) ParseArguments(string[] args)
{
    var logins = new List<string>();
    var password = "password";

    for (var i = 0; i < args.Length; i++)
    {
        var arg = args[i];
        if (arg == "--password" || arg == "-p")
        {
            if (i + 1 >= args.Length)
            {
                throw new ArgumentException("Missing value for --password.");
            }

            password = args[++i];
            continue;
        }

        logins.Add(arg);
    }

    var normalized = logins
        .Select(login => login.Trim())
        .Where(login => login.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    return (normalized, password);
}

static void PrintUsage()
{
    Console.WriteLine("Usage: dotnet run --project ResetPasswords -- <loginName> [<loginName> ...] [-p|--password <newPassword>]");
    Console.WriteLine("Example: dotnet run --project ResetPasswords -- SignalAdmin -p password");
}
