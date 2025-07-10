using System.Security.Cryptography;
using System.Text;

namespace Tinkwell.Bootstrapper.Expressions.CustomFunctions;

sealed class Base64Encode : UnaryFunction<string>
{
    protected override object? Call(string arg)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(arg));
}

sealed class Base64Decode : UnaryFunction<string>
{
    protected override object? Call(string arg)
        => Encoding.UTF8.GetString(Convert.FromBase64String(arg));
}

sealed class Md5 : UnaryFunction<string>
{
    protected override object? Call(string arg)
    {
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(arg));
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}

sealed class Sha256 : UnaryFunction<string>
{
    protected override object? Call(string arg)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(arg));
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}
