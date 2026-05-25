using System.Globalization;
using System.Text;

namespace Tinkwell.Package;

/// <summary>
/// Reads and writes the <c>security/signature.sig</c> file (text header + binary signature).
/// </summary>
internal static class SignatureFileFormat
{
    private static readonly byte[] Separator = "---\n"u8.ToArray();

    internal const int MaxFileSize = 8192;
    internal const int MaxHeaderSize = 4096;
    internal const int MaxSignatureSize = 1024;
    internal const int MaxFieldLength = 256;

    /// <summary>Parses a <c>signature.sig</c> file from its raw bytes (text header + <c>---</c> separator + binary signature).</summary>
    /// <param name="data">Complete file content. Must not exceed <see cref="MaxFileSize"/> bytes.</param>
    public static SignatureFile Parse(byte[] data)
    {
        if (data.Length > MaxFileSize)
            throw new PackageException(
                $"signature.sig exceeds maximum size ({data.Length} > {MaxFileSize})");

        var sepIndex = FindSeparator(data);
        if (sepIndex < 0)
            throw new PackageException("Invalid signature.sig: missing --- separator");

        if (sepIndex > MaxHeaderSize)
            throw new PackageException(
                $"signature.sig header exceeds maximum size ({sepIndex} > {MaxHeaderSize})");

        var signatureBytes = new byte[data.Length - sepIndex - Separator.Length];
        if (signatureBytes.Length > MaxSignatureSize)
            throw new PackageException(
                $"signature.sig signature exceeds maximum size ({signatureBytes.Length} > {MaxSignatureSize})");

        var headerText = Encoding.UTF8.GetString(data, 0, sepIndex);
        Buffer.BlockCopy(data, sepIndex + Separator.Length, signatureBytes, 0, signatureBytes.Length);

        string? algorithm = null, keyId = null;
        DateTimeOffset? timestamp = null;

        foreach (var line in headerText.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var colonIdx = line.IndexOf(':');
            if (colonIdx < 0)
                continue;

            var key = line[..colonIdx].Trim();
            var value = line[(colonIdx + 1)..].Trim();

            if (value.Length > MaxFieldLength)
                throw new PackageException(
                    $"signature.sig field '{key}' exceeds maximum length ({value.Length} > {MaxFieldLength})");

            switch (key.ToLowerInvariant())
            {
                case "algorithm": algorithm = value; break;
                case "key-id": keyId = value; break;
                case "timestamp":
                    if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out var ts))
                        timestamp = ts;
                    break;
            }
        }

        if (algorithm is null)
            throw new PackageException("Missing 'algorithm' in signature.sig header");
        if (keyId is null)
            throw new PackageException("Missing 'key-id' in signature.sig header");

        return new SignatureFile
        {
            Algorithm = algorithm,
            KeyId = keyId,
            Timestamp = timestamp ?? DateTimeOffset.MinValue,
            SignatureBytes = signatureBytes,
        };
    }

    /// <summary>Serializes a <see cref="SignatureFile"/> to the <c>signature.sig</c> binary format.</summary>
    /// <param name="sig">Signature metadata and ECDSA bytes to write.</param>
    public static byte[] Write(SignatureFile sig)
    {
        var header = $"algorithm: {sig.Algorithm}\nkey-id: {sig.KeyId}\ntimestamp: {sig.Timestamp:O}\n";
        var headerBytes = Encoding.UTF8.GetBytes(header);

        var result = new byte[headerBytes.Length + Separator.Length + sig.SignatureBytes.Length];
        Buffer.BlockCopy(headerBytes, 0, result, 0, headerBytes.Length);
        Buffer.BlockCopy(Separator, 0, result, headerBytes.Length, Separator.Length);
        Buffer.BlockCopy(sig.SignatureBytes, 0, result, headerBytes.Length + Separator.Length,
            sig.SignatureBytes.Length);

        return result;
    }

    private static int FindSeparator(byte[] data)
    {
        for (int i=0; i <= data.Length - Separator.Length; ++i)
        {
            bool match = true;
            for (int j=0; j < Separator.Length; ++j)
            {
                if (data[i + j] != Separator[j])
                {
                    match = false;
                    break;
                }
            }
            if (match)
                return i;
        }
        return -1;
    }
}
