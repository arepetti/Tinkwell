namespace Tinkwell.Package;

/// <summary>
/// Options for <see cref="TwPackage.PackAsync(string,Stream,PackOptions?,CancellationToken)"/>.
/// </summary>
public sealed class PackOptions
{
    /// <summary>ECDSA private key bytes (PKCS#8). Signing occurs when both
    /// this is non-null and <see cref="Sign"/> is <c>true</c>.</summary>
    public byte[]? PrivateKey { get; set; }

    /// <summary>Whether to sign the package. Ignored when <see cref="PrivateKey"/> is null.</summary>
    public bool Sign { get; set; } = true;
}
