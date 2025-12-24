using System;
using Isopoh.Cryptography.Argon2;

// Simple script to generate Argon2 hash for password reset
var password = "password";
var hash = Argon2.Hash(password);
Console.WriteLine($"Hash for '{password}':");
Console.WriteLine(hash);
