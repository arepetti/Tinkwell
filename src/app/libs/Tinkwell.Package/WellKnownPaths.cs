namespace Tinkwell.Package;

/// <summary>
/// Fixed paths within a Tinkwell package. These names are not configurable.
/// </summary>
public static class WellKnownPaths
{
    /// <summary>Manifest path at the package root.</summary>
    public const string Manifest = "package.tw";

    /// <summary>Content root directory name (all payload lives under <c>content/…</c>).</summary>
    public const string ContentDirectory = "content";

    /// <summary>Security directory containing the signatures manifest and binary signature.</summary>
    public const string SecurityDirectory = "security";

    /// <summary>Text manifest listing per-file SHA-512 hashes and sizes (outside <c>security/</c>).</summary>
    public const string Signatures = "security/signatures.tw";

    /// <summary>Binary file holding the ECDSA signature over the UTF-8 bytes of <c>signatures.tw</c>.</summary>
    public const string Signature = "security/signature.sig";
}
