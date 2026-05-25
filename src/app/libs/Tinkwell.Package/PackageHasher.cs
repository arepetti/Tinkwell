using System.Security.Cryptography;

namespace Tinkwell.Package;

/// <summary>
/// SHA-512 hashing utilities for package integrity verification.
/// </summary>
internal static class PackageHasher
{
    public const string Algorithm = "sha512";

    /// <summary>Computes the lowercase hex-encoded SHA-512 hash of <paramref name="data"/>.</summary>
    /// <param name="data">Bytes to hash.</param>
    public static string ComputeHash(byte[] data)
    {
        var hash = SHA512.HashData(data);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>Computes the lowercase hex-encoded SHA-512 hash of a stream.</summary>
    /// <param name="stream">Readable stream whose full content is hashed. Position is advanced to the end.</param>
    public static string ComputeHash(Stream stream)
    {
        var hash = SHA512.HashData(stream);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>Computes the SHA-512 hash of <paramref name="data"/> and compares it to <paramref name="expectedHash"/> in constant time.</summary>
    /// <param name="data">Bytes to hash.</param>
    /// <param name="expectedHash">Expected 128-character lowercase hex SHA-512 digest.</param>
    public static bool VerifyHash(byte[] data, string expectedHash)
    {
        var computed = SHA512.HashData(data);

        // SHA-512 produces 64 bytes = 128 hex characters.
        if (string.IsNullOrEmpty(expectedHash) || expectedHash.Length != computed.Length * 2)
            return false;

        byte[] expected;
        try
        {
            expected = Convert.FromHexString(expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(computed, expected);
    }
}
