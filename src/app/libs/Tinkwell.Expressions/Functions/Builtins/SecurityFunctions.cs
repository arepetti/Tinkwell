using System.Security.Cryptography;
using System.Text;

namespace Tinkwell.Expressions.Functions.Builtins;

/// <summary>
/// <c>base64_encode(s)</c> — UTF-8 text to base64.
/// </summary>
sealed class Base64Encode : UnaryFunction<string>
{
    protected override object? Call(string arg)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(arg));
}

/// <summary>
/// <c>base64_decode(s)</c> — Base64 to UTF-8 text.
/// </summary>
sealed class Base64Decode : UnaryFunction<string>
{
    protected override object? Call(string arg)
        => Encoding.UTF8.GetString(Convert.FromBase64String(arg));
}

/// <summary>
/// <c>md5(s)</c> — MD5 over UTF-8, lowercase hex.
/// </summary>
sealed class Md5Hash : UnaryFunction<string>
{
    public override string Name => "md5";

    protected override object? Call(string arg)
        => Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(arg)));
}

/// <summary>
/// <c>sha256(s)</c> — SHA-256 over UTF-8, lowercase hex.
/// </summary>
sealed class Sha256Hash : UnaryFunction<string>
{
    public override string Name => "sha256";

    protected override object? Call(string arg)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(arg)));
}

/// <summary>
/// <c>sha512(s)</c> — SHA-512 over UTF-8, lowercase hex.
/// </summary>
sealed class Sha512Hash : UnaryFunction<string>
{
    public override string Name => "sha512";

    protected override object? Call(string arg)
        => Convert.ToHexStringLower(SHA512.HashData(Encoding.UTF8.GetBytes(arg)));
}
