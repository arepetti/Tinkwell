using Tinkwell.Package;

static class PackCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        string? inputDir = null;
        string? output = null;
        bool sign = false;
        string keyEnv = "TW_SIGNING_KEY";

        for (int i=0; i < args.Length; ++i)
        {
            switch (args[i])
            {
                case "-o" or "--output":
                    output = NextArg(args, ref i, "--output");
                    break;
                case "--sign":
                    sign = true;
                    break;
                case "--key-env":
                    keyEnv = NextArg(args, ref i, "--key-env");
                    break;
                default:
                    if (args[i].StartsWith('-'))
                        return Fail($"Unknown option '{args[i]}'.");
                    if (inputDir is not null)
                        return Fail("Only one input directory is allowed.");
                    inputDir = args[i];
                    break;
            }
        }

        if (inputDir is null)
            return Fail("Missing input directory. Usage: pack <dir> -o <output.twpkg>");
        if (output is null)
            return Fail("Missing -o / --output path.");
        if (!Directory.Exists(inputDir))
            return Fail($"Directory not found: {inputDir}");

        var manifestPath = Path.Combine(inputDir, WellKnownPaths.Manifest);
        if (!File.Exists(manifestPath))
            return Fail($"No {WellKnownPaths.Manifest} found in {inputDir}");

        var manifest = ManifestFormat.Parse(await File.ReadAllTextAsync(manifestPath));

        var options = new PackOptions();
        if (sign)
        {
            var keyBase64 = Environment.GetEnvironmentVariable(keyEnv);
            if (string.IsNullOrWhiteSpace(keyBase64))
                return Fail($"--sign requires the {keyEnv} environment variable to contain a base64-encoded PKCS#8 private key.");

            try
            {
                options.PrivateKey = Convert.FromBase64String(keyBase64);
            }
            catch (FormatException)
            {
                return Fail($"The {keyEnv} environment variable is not valid base64.");
            }
        }

        var entries = Directory.EnumerateFiles(inputDir, "*", SearchOption.AllDirectories)
            .Where(f => !Path.GetFileName(f).Equals(WellKnownPaths.Manifest, StringComparison.OrdinalIgnoreCase))
            .Select(f => new PackageEntry(
                Path.GetRelativePath(inputDir, f).Replace('\\', '/'),
                File.OpenRead(f)))
            .ToList();

        var outputDir = Path.GetDirectoryName(Path.GetFullPath(output));
        if (outputDir is not null && !Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        await new TwPackage().PackAsync(manifest, entries, output, options);

        foreach (var entry in entries)
            entry.Content.Dispose();

        Console.WriteLine(Path.GetFullPath(output));
        return 0;
    }

    private static string NextArg(string[] args, ref int i, string name)
    {
        if (++i >= args.Length)
            throw new InvalidOperationException($"{name} requires a value.");
        return args[i];
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine($"error: {message}");
        return 1;
    }
}
