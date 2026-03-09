using Microsoft.AspNetCore.Identity;

if (args.Length < 1)
{
    Console.WriteLine("Usage: AdminHashGen <password>");
    return;
}

var password = args[0];
var hasher = new PasswordHasher<object>();
var hash = hasher.HashPassword(new object(), password);
Console.WriteLine(hash);
