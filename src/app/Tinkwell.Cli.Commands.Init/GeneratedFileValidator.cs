using Microsoft.Extensions.FileProviders;
using Tinkwell.Configuration.Parser;
using Tinkwell.Coordinator.Configuration;

namespace Tinkwell.Cli.Commands.Init;

/// <summary>
/// Optional post-generation validators that check whether the output
/// file is well-formed. Validators are selected by the <c>validator</c>
/// property in <c>outputs.tw</c>.
/// </summary>
internal static class GeneratedFileValidator
{
    /// <summary>
    /// Validates <paramref name="content"/> using the validator identified
    /// by <paramref name="validatorName"/>. Returns a list of diagnostics
    /// (empty if valid).
    /// </summary>
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(
        "Default ExpressionEvaluator discovers functions via reflection.")]
    public static async Task<IReadOnlyList<string>> ValidateAsync(
        string? validatorName, string content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(validatorName))
            return [];

        return validatorName switch
        {
            "tinkwell-ensemble" => await ValidateEnsembleAsync(content, cancellationToken),
            _ => []
        };
    }

    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(
        "Default ExpressionEvaluator discovers functions via reflection.")]
    private static async Task<IReadOnlyList<string>> ValidateEnsembleAsync(
        string content, CancellationToken cancellationToken)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "tw-init-validate-" + Guid.NewGuid().ToString("N")[..8]);
        var tempFile = Path.Combine(tempDir, "ensemble.tw");

        try
        {
            Directory.CreateDirectory(tempDir);
            await File.WriteAllTextAsync(tempFile, content, cancellationToken);

            var provider = new PhysicalFileProvider(tempDir);
            var parser = new EnsembleParser(
                options: new ParserOptions { Lax = true });
            await parser.LoadAsync(provider, "ensemble.tw", cancellationToken: cancellationToken);
            return [];
        }
        catch (Exception ex)
        {
            return [ex.Message];
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }
}
