using System.IO.Compression;
using System.Text;

namespace Tinkwell.Package;

/// <summary>
/// Core API for packing, unpacking, verifying, and re-signing Tinkwell packages.
/// </summary>
public sealed class TwPackage
{
    /// <summary>Maximum size for a <c>package.tw</c> file read from disk (bytes).</summary>
    internal const int MaxManifestFileSizeBytes = 1 * 1024 * 1024;

    /// <summary>Maximum size for a <c>signatures.tw</c> file read from disk (bytes).</summary>
    internal const int MaxSignaturesFileSizeBytes = 10 * 1024 * 1024;

    private readonly IPackageValidator _validator;

    /// <summary>
    /// Creates a <see cref="TwPackage"/> that uses the given validator, or
    /// <see cref="PackageValidator"/> when <paramref name="validator"/> is <c>null</c>.
    /// </summary>
    /// <param name="validator">Custom <see cref="IPackageValidator"/> for path, size, and structure checks, or <c>null</c> for defaults.</param>
    public TwPackage(IPackageValidator? validator = null)
    {
        _validator = validator ?? new PackageValidator();
    }

    /// <summary>
    /// Packs a directory into a Tinkwell package zip file. The directory must
    /// contain <c>package.tw</c> and a <c>content/</c> subdirectory.
    /// </summary>
    /// <param name="sourceDirectory">Directory containing <c>package.tw</c> and <c>content/</c>.</param>
    /// <param name="outputPath">File path for the output <c>.zip</c> package.</param>
    /// <param name="options">Packing options (signing, compression). Uses defaults when <c>null</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task PackAsync(
        string sourceDirectory, string outputPath,
        PackOptions? options = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sourceDirectory);
        ArgumentNullException.ThrowIfNull(outputPath);
        using var fs = File.Create(outputPath);
        await PackAsync(sourceDirectory, fs, options, ct);
    }

    /// <summary>
    /// Packs a content directory using a programmatic manifest. The
    /// <paramref name="contentDirectory"/> is treated as the <c>content/</c>
    /// root directly (no need for a <c>package.tw</c> file on disk).
    /// </summary>
    /// <param name="manifest">Package metadata (name, version, author, etc.).</param>
    /// <param name="contentDirectory">Directory whose files become the <c>content/</c> entries.</param>
    /// <param name="output">Writable stream to receive the zip archive.</param>
    /// <param name="options">Packing options (signing, compression). Uses defaults when <c>null</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task PackAsync(
        PackageManifest manifest, string contentDirectory, Stream output,
        PackOptions? options = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(contentDirectory);
        ArgumentNullException.ThrowIfNull(output);
        if (!Directory.Exists(contentDirectory))
            throw new PackageException($"Content directory not found: {contentDirectory}");

        var entries = new List<PackageEntry>();
        foreach (var file in Directory.EnumerateFiles(contentDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(contentDirectory, file).Replace('\\', '/');
            entries.Add(new PackageEntry(relativePath, File.OpenRead(file)));
        }

        try
        {
            await PackAsync(manifest, entries, output, options, ct);
        }
        finally
        {
            foreach (var entry in entries)
                entry.Content.Dispose();
        }
    }

    /// <summary>
    /// Packs a content directory using a programmatic manifest into a zip file.
    /// </summary>
    /// <param name="manifest">Package metadata (name, version, author, etc.).</param>
    /// <param name="contentDirectory">Directory whose files become the <c>content/</c> entries.</param>
    /// <param name="outputPath">File path for the output <c>.zip</c> package.</param>
    /// <param name="options">Packing options (signing, compression). Uses defaults when <c>null</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task PackAsync(
        PackageManifest manifest, string contentDirectory, string outputPath,
        PackOptions? options = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(contentDirectory);
        ArgumentNullException.ThrowIfNull(outputPath);
        using var fs = File.Create(outputPath);
        await PackAsync(manifest, contentDirectory, fs, options, ct);
    }

    /// <summary>
    /// Packs a directory into a Tinkwell package. The directory must contain
    /// <c>package.tw</c> and a <c>content/</c> subdirectory.
    /// </summary>
    /// <param name="sourceDirectory">Directory containing <c>package.tw</c> and <c>content/</c>.</param>
    /// <param name="output">Writable stream to receive the zip archive.</param>
    /// <param name="options">Packing options (signing, compression). Uses defaults when <c>null</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task PackAsync(
        string sourceDirectory, Stream output,
        PackOptions? options = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sourceDirectory);
        ArgumentNullException.ThrowIfNull(output);
        options ??= new PackOptions();

        var manifestPath = Path.Combine(sourceDirectory, WellKnownPaths.Manifest);
        if (!File.Exists(manifestPath))
            throw new PackageException($"Missing {WellKnownPaths.Manifest} in {sourceDirectory}");

        var contentDir = Path.Combine(sourceDirectory, WellKnownPaths.ContentDirectory);
        if (!Directory.Exists(contentDir))
            throw new PackageException($"Missing {WellKnownPaths.ContentDirectory}/ in {sourceDirectory}");

        var entries = new List<(string relativePath, byte[] data)>();

        // package.tw
        var manifestFileInfo = new FileInfo(manifestPath);
        if (manifestFileInfo.Length > MaxManifestFileSizeBytes)
        {
            throw new PackageException(
                $"{WellKnownPaths.Manifest} exceeds maximum allowed size ({manifestFileInfo.Length} > {MaxManifestFileSizeBytes} bytes)");
        }

        var manifestData = await File.ReadAllBytesAsync(manifestPath, ct);
        entries.Add((WellKnownPaths.Manifest, manifestData));

        // Validate no files outside allowed structure
        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(file);
            if (!name.Equals(WellKnownPaths.Manifest, StringComparison.OrdinalIgnoreCase))
                throw new PackageException($"File outside allowed structure: {name}");
        }
        foreach (var dir in Directory.EnumerateDirectories(sourceDirectory))
        {
            var name = Path.GetFileName(dir);
            if (!name.Equals(WellKnownPaths.ContentDirectory, StringComparison.OrdinalIgnoreCase) &&
                !name.Equals(WellKnownPaths.SecurityDirectory, StringComparison.OrdinalIgnoreCase))
                throw new PackageException($"Directory outside allowed structure: {name}");
        }

        // content/**
        foreach (var file in Directory.EnumerateFiles(contentDir, "*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            var relativePath = WellKnownPaths.ContentDirectory + "/" +
                Path.GetRelativePath(contentDir, file).Replace('\\', '/');
            var data = await File.ReadAllBytesAsync(file, ct);
            entries.Add((relativePath, data));
        }

        await WritePackageZip(entries, output, options, ct);
    }

    /// <summary>
    /// Packs a manifest and a set of content entries into a Tinkwell package zip file.
    /// </summary>
    /// <param name="manifest">Package metadata (name, version, author, etc.).</param>
    /// <param name="contentFiles">Stream-backed content entries; each entry's <see cref="PackageEntry.RelativePath"/> is placed under <c>content/</c>.</param>
    /// <param name="outputPath">File path for the output <c>.zip</c> package.</param>
    /// <param name="options">Packing options (signing, compression). Uses defaults when <c>null</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task PackAsync(
        PackageManifest manifest, IEnumerable<PackageEntry> contentFiles,
        string outputPath, PackOptions? options = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(contentFiles);
        ArgumentNullException.ThrowIfNull(outputPath);
        using var fs = File.Create(outputPath);
        await PackAsync(manifest, contentFiles, fs, options, ct);
    }

    /// <summary>
    /// Packs a manifest and a set of content entries into a Tinkwell package.
    /// </summary>
    /// <param name="manifest">Package metadata (name, version, author, etc.).</param>
    /// <param name="contentFiles">Stream-backed content entries; each entry's <see cref="PackageEntry.RelativePath"/> is placed under <c>content/</c>.</param>
    /// <param name="output">Writable stream to receive the zip archive.</param>
    /// <param name="options">Packing options (signing, compression). Uses defaults when <c>null</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task PackAsync(
        PackageManifest manifest, IEnumerable<PackageEntry> contentFiles,
        Stream output, PackOptions? options = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(contentFiles);
        ArgumentNullException.ThrowIfNull(output);
        options ??= new PackOptions();

        var entries = new List<(string relativePath, byte[] data)>();

        var manifestData = Encoding.UTF8.GetBytes(ManifestFormat.Write(manifest));
        entries.Add((WellKnownPaths.Manifest, manifestData));

        foreach (var entry in contentFiles)
        {
            ct.ThrowIfCancellationRequested();
            using var ms = new MemoryStream();
            await entry.Content.CopyToAsync(ms, ct);
            entries.Add((WellKnownPaths.ContentDirectory + "/" + entry.RelativePath, ms.ToArray()));
        }

        await WritePackageZip(entries, output, options, ct);
    }

    /// <summary>
    /// Verifies a package. Accepts a zip file path, a directory path, or
    /// a <c>package.tw</c> file path.
    /// </summary>
    /// <param name="path">Path to a <c>.zip</c> package, an extracted directory, or a <c>package.tw</c> manifest file.</param>
    /// <param name="options">Verification options (trusted keys, strictness). Uses defaults when <c>null</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<VerificationResult> VerifyAsync(
        string path, VerifyOptions? options = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        options ??= new VerifyOptions();

        if (File.Exists(path) && path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return await VerifyZipAsync(path, options, ct);

        string rootDir;
        if (Directory.Exists(path))
            rootDir = path;
        else if (File.Exists(path) && Path.GetFileName(path).Equals(
            WellKnownPaths.Manifest, StringComparison.OrdinalIgnoreCase))
            rootDir = Path.GetDirectoryName(path)!;
        else
            throw new PackageException($"Not a valid package path: {path}");

        return await VerifyDirectoryAsync(rootDir, options, ct);
    }

    /// <summary>
    /// Unpacks a zip package file to a directory. By default verifies before extraction.
    /// </summary>
    /// <param name="packagePath">Path to the <c>.zip</c> package file.</param>
    /// <param name="outputDirectory">Destination directory. Replaced if it already exists.</param>
    /// <param name="options">Unpack options (verification, size limits). Uses defaults when <c>null</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task UnpackAsync(
        string packagePath, string outputDirectory,
        UnpackOptions? options = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(packagePath);
        ArgumentNullException.ThrowIfNull(outputDirectory);
        using var fs = File.OpenRead(packagePath);
        await UnpackAsync(fs, outputDirectory, options, ct);
    }

    /// <summary>
    /// Unpacks a zip package to a directory. By default verifies before extraction.
    /// </summary>
    /// <param name="package">Readable stream containing the zip package.</param>
    /// <param name="outputDirectory">Destination directory. Replaced if it already exists.</param>
    /// <param name="options">Unpack options (verification, size limits). Uses defaults when <c>null</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task UnpackAsync(
        Stream package, string outputDirectory,
        UnpackOptions? options = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(outputDirectory);
        options ??= new UnpackOptions();

        var tempDir = Path.Combine(
            Path.GetTempPath(), "tw-pkg-" + Guid.NewGuid().ToString("N")[..12]);

        try
        {
            Directory.CreateDirectory(tempDir);
            await ExtractToDirectory(package, tempDir, options, ct);

            if (options.Verify)
            {
#pragma warning disable CS0618 // Obsolete PublicKey shortcut kept for back-compat.
                var verifyOpts = new VerifyOptions
                {
                    TrustedKeys = options.TrustedKeys,
                    PublicKey = options.PublicKey,
                    RequireSignatures = options.RequireSignatures,
                    AllowIntegrityOnly = options.AllowIntegrityOnly,
                    StrictLayout = options.StrictLayout,
                };
#pragma warning restore CS0618
                var result = await VerifyDirectoryAsync(tempDir, verifyOpts, ct);
                if (!result.IsValid)
                {
                    var errors = string.Join("; ",
                        result.Issues.Where(i => i.Severity == VerificationSeverity.Error)
                            .Select(i => i.Message));
                    throw new PackageException($"Package verification failed: {errors}");
                }
            }

            if (Directory.Exists(outputDirectory))
                Directory.Delete(outputDirectory, recursive: true);

            Directory.CreateDirectory(Path.GetDirectoryName(outputDirectory)!);
            try
            {
                Directory.Move(tempDir, outputDirectory);
            }
            catch (IOException)
            {
                CopyDirectory(tempDir, outputDirectory);
                Directory.Delete(tempDir, recursive: true);
            }
        }
        catch
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
            throw;
        }
    }

    /// <summary>
    /// Re-signs an existing package file: reads all content, regenerates
    /// <c>security/signatures.tw</c> and <c>security/signature.sig</c>,
    /// and writes to a new zip file.
    /// </summary>
    /// <param name="packagePath">Path to the existing <c>.zip</c> package file to re-sign.</param>
    /// <param name="outputPath">File path for the re-signed output package.</param>
    /// <param name="privateKey">PKCS#8-encoded ECDSA P-384 private key used to produce the new signature.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task ResignAsync(
        string packagePath, string outputPath, byte[] privateKey, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(packagePath);
        ArgumentNullException.ThrowIfNull(outputPath);
        ArgumentNullException.ThrowIfNull(privateKey);
        using var input = File.OpenRead(packagePath);
        using var output = File.Create(outputPath);
        await ResignAsync(input, output, privateKey, ct);
    }

    /// <summary>
    /// Re-signs an existing package: reads all content, regenerates
    /// <c>security/signatures.tw</c> and <c>security/signature.sig</c>.
    /// </summary>
    /// <param name="package">Readable stream containing the existing zip package.</param>
    /// <param name="output">Writable stream for the re-signed package.</param>
    /// <param name="privateKey">PKCS#8-encoded ECDSA P-384 private key used to produce the new signature.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task ResignAsync(
        Stream package, Stream output, byte[] privateKey, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(privateKey);
        var entries = new List<(string relativePath, byte[] data)>();

        using (var zip = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true))
        {
            foreach (var entry in zip.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name))
                    continue;

                var path = _validator.NormalizeEntryPath(entry.FullName);

                if (path.StartsWith(WellKnownPaths.SecurityDirectory + "/",
                    StringComparison.Ordinal))
                    continue;

                if (path.Equals(WellKnownPaths.Manifest, StringComparison.Ordinal)
                    && entry.Length > MaxManifestFileSizeBytes)
                {
                    throw new PackageException(
                        $"{WellKnownPaths.Manifest} exceeds maximum allowed size " +
                        $"({entry.Length} > {MaxManifestFileSizeBytes} bytes)");
                }

                using var ms = new MemoryStream();
                using var stream = entry.Open();
                await stream.CopyToAsync(ms, ct);
                entries.Add((path, ms.ToArray()));
            }
        }

        await WritePackageZip(entries, output,
            new PackOptions { PrivateKey = privateKey, Sign = true }, ct);
    }

    private async Task WritePackageZip(
        List<(string relativePath, byte[] data)> entries,
        Stream output, PackOptions options, CancellationToken ct)
    {
        bool sign = options.Sign && options.PrivateKey is not null;

        PackageSignatures? signatures = null;
        if (sign)
        {
            var fileSignatures = entries
                .OrderBy(e => e.relativePath, StringComparer.Ordinal)
                .Select(e => new FileSignature
                {
                    Path = e.relativePath,
                    Hash = PackageHasher.ComputeHash(e.data),
                    Size = e.data.Length,
                })
                .ToList();

            signatures = new PackageSignatures
            {
                Algorithm = PackageHasher.Algorithm,
                Files = fileSignatures,
            };
        }

        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);

        foreach (var (relativePath, data) in entries)
        {
            ct.ThrowIfCancellationRequested();
            var entry = zip.CreateEntry(relativePath, CompressionLevel.Optimal);
            using var stream = entry.Open();
            await stream.WriteAsync(data, ct);
        }

        if (sign && signatures is not null)
        {
            var sigTwContent = Encoding.UTF8.GetBytes(SignaturesFormat.Write(signatures));

            var sigTwEntry = zip.CreateEntry(WellKnownPaths.Signatures, CompressionLevel.Optimal);
            using (var stream = sigTwEntry.Open())
                await stream.WriteAsync(sigTwContent, ct);

            var sigFile = PackageSigner.Sign(sigTwContent, options.PrivateKey!);
            var sigFileBytes = SignatureFileFormat.Write(sigFile);

            var sigEntry = zip.CreateEntry(WellKnownPaths.Signature, CompressionLevel.Optimal);
            using (var stream = sigEntry.Open())
                await stream.WriteAsync(sigFileBytes, ct);
        }
    }

    private async Task<VerificationResult> VerifyZipAsync(
        string zipPath, VerifyOptions options, CancellationToken ct)
    {
        var tempDir = Path.Combine(
            Path.GetTempPath(), "tw-verify-" + Guid.NewGuid().ToString("N")[..12]);

        try
        {
            Directory.CreateDirectory(tempDir);

            using var fs = File.OpenRead(zipPath);
            var unpackOpts = new UnpackOptions
            {
                Verify = false, // we'll verify the extracted content ourselves
            };
            await ExtractToDirectory(fs, tempDir, unpackOpts, ct);
            return await VerifyDirectoryAsync(tempDir, options, ct);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    private async Task<VerificationResult> VerifyDirectoryAsync(
        string rootDir, VerifyOptions options, CancellationToken ct)
    {
        var trustedKeys = CollectTrustedKeys(options);

        // Fail fast on misconfigured callers: requiring signatures without a
        // trust anchor is never safe, because an attacker who tampers with
        // content can simply rewrite signatures.tw with matching hashes. The
        // caller must either supply keys or explicitly accept integrity-only.
        if (options.RequireSignatures
            && trustedKeys.Count == 0
            && !options.AllowIntegrityOnly)
        {
            throw new ArgumentException(
                "Verification requires at least one entry in VerifyOptions.TrustedKeys, " +
                "or explicit opt-in via VerifyOptions.AllowIntegrityOnly = true. " +
                "Integrity-only verification cannot detect forged signature manifests " +
                "and must be requested deliberately.",
                nameof(options));
        }

        var issues = new List<VerificationIssue>();

        // Check manifest exists
        var manifestPath = Path.Combine(rootDir, WellKnownPaths.Manifest);
        if (!File.Exists(manifestPath))
        {
            issues.Add(new VerificationIssue(VerificationSeverity.Error,
                VerificationCodes.MissingManifest,
                $"Missing {WellKnownPaths.Manifest}"));
            return new VerificationResult(false, issues);
        }

        var manifestFileInfo = new FileInfo(manifestPath);
        if (manifestFileInfo.Length > MaxManifestFileSizeBytes)
        {
            issues.Add(new VerificationIssue(VerificationSeverity.Error,
                VerificationCodes.ParseError,
                $"{WellKnownPaths.Manifest} exceeds maximum allowed size " +
                $"({manifestFileInfo.Length} > {MaxManifestFileSizeBytes} bytes)"));
            return new VerificationResult(false, issues);
        }

        if (options.StrictLayout)
        {
            var allPaths = Directory.EnumerateFiles(rootDir, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(rootDir, f).Replace('\\', '/'))
                .ToHashSet(StringComparer.Ordinal);
            try
            {
                _validator.ValidatePackageStructure(allPaths);
            }
            catch (PackageException ex)
            {
                issues.Add(new VerificationIssue(VerificationSeverity.Error,
                    VerificationCodes.InvalidStructure, ex.Message));
                return new VerificationResult(false, issues);
            }
        }

        // Check signatures exist
        var sigTwPath = Path.Combine(rootDir, WellKnownPaths.Signatures.Replace('/', Path.DirectorySeparatorChar));
        var sigPath = Path.Combine(rootDir, WellKnownPaths.Signature.Replace('/', Path.DirectorySeparatorChar));

        bool hasSignatures = File.Exists(sigTwPath) && File.Exists(sigPath);

        if (!hasSignatures && options.RequireSignatures)
        {
            if (!File.Exists(sigTwPath))
                issues.Add(new VerificationIssue(VerificationSeverity.Error,
                    VerificationCodes.MissingSignatures,
                    $"Missing {WellKnownPaths.Signatures}"));
            if (!File.Exists(sigPath))
                issues.Add(new VerificationIssue(VerificationSeverity.Error,
                    VerificationCodes.MissingSignature,
                    $"Missing {WellKnownPaths.Signature}"));
            return new VerificationResult(false, issues);
        }

        if (!hasSignatures)
        {
            issues.Add(new VerificationIssue(VerificationSeverity.Warning,
                VerificationCodes.Unsigned,
                "Package is not signed; only structural checks were performed."));
            return new VerificationResult(true, issues);
        }

        // Parse signatures
        PackageSignatures signatures;
        try
        {
            var sigTwFileInfo = new FileInfo(sigTwPath);
            if (sigTwFileInfo.Length > MaxSignaturesFileSizeBytes)
            {
                issues.Add(new VerificationIssue(VerificationSeverity.Error,
                    VerificationCodes.ParseError,
                    $"{WellKnownPaths.Signatures} exceeds maximum allowed size " +
                    $"({sigTwFileInfo.Length} > {MaxSignaturesFileSizeBytes} bytes)"));
                return new VerificationResult(false, issues);
            }

            var signatureBinInfo = new FileInfo(sigPath);
            if (signatureBinInfo.Length > SignatureFileFormat.MaxFileSize)
            {
                issues.Add(new VerificationIssue(VerificationSeverity.Error,
                    VerificationCodes.ParseError,
                    $"{WellKnownPaths.Signature} exceeds maximum allowed size " +
                    $"({signatureBinInfo.Length} > {SignatureFileFormat.MaxFileSize} bytes)"));
                return new VerificationResult(false, issues);
            }

            var sigTwContent = await File.ReadAllBytesAsync(sigTwPath, ct);
            signatures = SignaturesFormat.Parse(Encoding.UTF8.GetString(sigTwContent));

            if (!signatures.Algorithm.Equals(PackageHasher.Algorithm, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new VerificationIssue(VerificationSeverity.Warning,
                    VerificationCodes.UnknownAlgorithm,
                    $"Signatures declare algorithm '{signatures.Algorithm}' but verification uses " +
                    $"'{PackageHasher.Algorithm}'. Hashes are verified with the built-in " +
                    "algorithm regardless of label."));
            }

            // Verify cryptographic signature
            if (trustedKeys.Count > 0)
            {
                var sigFileData = await File.ReadAllBytesAsync(sigPath, ct);
                var signatureFile = SignatureFileFormat.Parse(sigFileData);

                if (!PackageSigner.Verify(sigTwContent, signatureFile, trustedKeys))
                {
                    issues.Add(new VerificationIssue(VerificationSeverity.Error,
                        VerificationCodes.InvalidSignature,
                        "Cryptographic signature verification failed: " +
                        "no trusted key matched the package signature."));
                    return new VerificationResult(false, issues);
                }
            }
            else
            {
                // Reachable only when AllowIntegrityOnly == true (the fail-fast
                // check above rejects the unsafe default path).
                issues.Add(new VerificationIssue(VerificationSeverity.Warning,
                    VerificationCodes.IntegrityOnly,
                    "Package signature was not cryptographically verified " +
                    "(AllowIntegrityOnly=true); only file-hash integrity was checked."));
            }
        }
        catch (PackageException ex)
        {
            issues.Add(new VerificationIssue(VerificationSeverity.Error,
                VerificationCodes.ParseError, ex.Message));
            return new VerificationResult(false, issues);
        }

        // Validate canonical ordering
        var paths = signatures.Files.Select(f => f.Path).ToList();
        try { _validator.ValidateSignatureOrder(paths); }
        catch (PackageException ex)
        {
            issues.Add(new VerificationIssue(VerificationSeverity.Error,
                VerificationCodes.NonCanonicalOrder, ex.Message));
        }

        // Verify each file hash and size
        foreach (var fileSig in signatures.Files)
        {
            ct.ThrowIfCancellationRequested();
            var filePath = Path.Combine(rootDir, fileSig.Path.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(filePath))
            {
                issues.Add(new VerificationIssue(VerificationSeverity.Error,
                    VerificationCodes.MissingFile,
                    $"Signed file not found: {fileSig.Path}", fileSig.Path));
                continue;
            }

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length != fileSig.Size)
            {
                issues.Add(new VerificationIssue(VerificationSeverity.Error,
                    VerificationCodes.SizeMismatch,
                    $"Size mismatch for {fileSig.Path}: expected {fileSig.Size}, got {fileInfo.Length}",
                    fileSig.Path));
                continue;
            }

            var data = await File.ReadAllBytesAsync(filePath, ct);
            if (!PackageHasher.VerifyHash(data, fileSig.Hash))
            {
                issues.Add(new VerificationIssue(VerificationSeverity.Error,
                    VerificationCodes.HashMismatch,
                    $"Hash mismatch for {fileSig.Path}", fileSig.Path));
            }
        }

        // Completeness check
        var signedPathSet = signatures.Files.Select(f => f.Path).ToHashSet(StringComparer.Ordinal);
        var actualPaths = EnumeratePackageFiles(rootDir)
            .ToHashSet(StringComparer.Ordinal);

        var verifiablePaths = actualPaths
            .Where(p => !p.StartsWith(WellKnownPaths.SecurityDirectory + "/", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);

        try
        {
            _validator.ValidateCompleteness(signedPathSet, actualPaths);
        }
        catch (PackageException ex)
        {
            var missing = signedPathSet.Except(verifiablePaths).ToList();
            var undeclared = verifiablePaths.Except(signedPathSet).ToList();

            if (missing.Count == 0 && undeclared.Count == 0)
            {
                issues.Add(new VerificationIssue(VerificationSeverity.Error,
                    VerificationCodes.MissingFile, ex.Message));
            }
            else
            {
                foreach (var path in missing)
                {
                    issues.Add(new VerificationIssue(VerificationSeverity.Error,
                        VerificationCodes.MissingFile,
                        $"Signed file not found in package: {path}", path));
                }

                foreach (var path in undeclared)
                {
                    issues.Add(new VerificationIssue(VerificationSeverity.Error,
                        VerificationCodes.UndeclaredFile,
                        $"Undeclared file in package: {path}", path));
                }
            }
        }

        return new VerificationResult(
            !issues.Any(i => i.Severity == VerificationSeverity.Error), issues);
    }

    private async Task ExtractToDirectory(
        Stream package, string outputDir, UnpackOptions options, CancellationToken ct)
    {
        using var zip = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true);

        _validator.ValidateFileCount(zip.Entries.Count, options.MaxFileCount);

        long totalSize = 0;
        var seenPaths = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in zip.Entries)
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrEmpty(entry.Name))
                continue; // directory entry

            var normalized = _validator.NormalizeEntryPath(entry.FullName);
            _validator.ValidateEntryPath(normalized, options.MaxPathLength);

            if (!seenPaths.Add(normalized))
                throw new PackageException($"Duplicate entry: {normalized}");

            _validator.ValidateFileSize(normalized, entry.Length, options.MaxFileSize);

            _validator.AccumulateDecompressedSize(
                ref totalSize, entry.Length, options.MaxDecompressedSize);

            var targetPath = Path.Combine(outputDir, normalized.Replace('/', Path.DirectorySeparatorChar));
            var targetFull = Path.GetFullPath(targetPath);
            var rootFull = Path.GetFullPath(outputDir) + Path.DirectorySeparatorChar;

            if (!targetFull.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                throw new PackageException($"Entry escapes output directory: {normalized}");

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

            using var source = entry.Open();
            using var target = File.Create(targetPath);
            await source.CopyToAsync(target, ct);
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        foreach (var dir in Directory.EnumerateDirectories(source))
            CopyDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));
    }

    private static IReadOnlyList<byte[]> CollectTrustedKeys(VerifyOptions options)
    {
        var configured = options.TrustedKeys;

#pragma warning disable CS0618 // PublicKey is the obsolete shortcut kept for back-compat.
        var legacy = options.PublicKey;
#pragma warning restore CS0618

        var hasList = configured is not null && configured.Count > 0;
        if (!hasList && legacy is null)
            return Array.Empty<byte[]>();

        var list = new List<byte[]>(
            (configured?.Count ?? 0) + (legacy is null ? 0 : 1));

        if (hasList)
            list.AddRange(configured!);

        if (legacy is not null)
            list.Add(legacy);

        return list;
    }

    private static IEnumerable<string> EnumeratePackageFiles(string rootDir)
    {
        foreach (var file in Directory.EnumerateFiles(rootDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(rootDir, file).Replace('\\', '/');

            if (relative.StartsWith(WellKnownPaths.SecurityDirectory + "/", StringComparison.Ordinal))
                continue;

            yield return relative;
        }
    }
}
