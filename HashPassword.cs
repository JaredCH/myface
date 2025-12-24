using System;
using Isopoh.Cryptography.Argon2;

var hash = Argon2.Hash("password");
Console.WriteLine(hash);
