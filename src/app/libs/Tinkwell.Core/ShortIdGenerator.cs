using System.Security.Cryptography;

namespace Tinkwell;

/// <summary>
/// Generates short, human-friendly IDs. Each ID is an 8-character
/// lowercase hex string derived from a SHA-256 hash of a fresh GUID,
/// giving uniform distribution regardless of the GUID version used.
/// </summary>
public static class ShortIdGenerator
{
    /// <summary>
    /// The default length of generated IDs (8 hex characters).
    /// </summary>
    public const int IdLength = 8;

    /// <summary>
    /// Creates a new ID: a <see cref="IdLength"/>-character lowercase hex string.
    /// </summary>
    public static string NewId()
    {
        var bytes = Guid.NewGuid().ToByteArray();
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash)[..IdLength];
    }

    /// <summary>
    /// Creates a new ID: a lowercase hex string with the specified length.
    /// </summary>
    /// <param name="length">The length (in characters) of the generated ID.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// If <paramref name="length"/> is less than 1 or more than 32.
    /// </exception>
    public static string NewId(int length)
    {
        if (length < 1 || length > 32)
            throw new ArgumentOutOfRangeException(nameof(length));

        var bytes = Guid.NewGuid().ToByteArray();
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash)[..length];
    }

    /// <summary>
    /// Validates that a string is a well-formed short ID (lowercase hex characters).
    /// </summary>
    public static bool IsValid(string? id) =>
        id is not null
        && (id.Length > 0 && id.Length <= 32)
        && id.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
}
