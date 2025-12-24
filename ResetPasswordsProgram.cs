using System;
using Isopoh.Cryptography.Argon2;

class Program
{
    static void Main()
    {
        var password = "password";
        var hash = Argon2.Hash(password);
        Console.WriteLine(hash);
    }
}
