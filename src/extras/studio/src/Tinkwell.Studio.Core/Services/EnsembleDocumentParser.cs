using Microsoft.Extensions.FileProviders;
using Tinkwell.Configuration.Parser;

namespace Tinkwell.Studio.Services;

/// <summary>
/// Pass-through configuration parser: returns the fully preprocessed
/// <see cref="ConfigDocument"/> unchanged.
/// </summary>
/// <remarks>
/// <para>
/// The Ensemble category in Tinkwell.Studio is a generic, read-only viewer of
/// the ensemble file. It does not know (nor care) whether a block represents
/// a runner, a measure, a CoAP server, or anything else &#8212; it only needs
/// names, modifiers, properties, and children. <see cref="ConfigDocument"/>
/// already has that shape, so the "transform" phase is just the identity.
/// </para>
/// <para>
/// Using the base <see cref="ConfigurationParser{T}"/> pipeline (rather than a
/// hand-rolled text parser) means Studio benefits automatically from include
/// resolution, <c>$"..."</c> interpolation, template expansion, and <c>if</c>
/// pruning &#8212; identical to what the coordinator sees at startup.
/// </para>
/// </remarks>
internal sealed class EnsembleDocumentParser : ConfigurationParser<ConfigDocument>
{
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(
        "Default ExpressionEvaluator discovers functions via reflection.")]
    public EnsembleDocumentParser() : base(logger: null)
    {
    }

    /// <summary>
    /// Convenience: parses <paramref name="path"/> using a
    /// <see cref="PhysicalFileProvider"/> rooted at its containing directory.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(
        "Relies on the reflection-based default expression evaluator.")]
    public static Task<ConfigDocument> LoadFileAsync(
        string path, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException(
                $"Cannot resolve directory for '{path}'.");
        var fileName = Path.GetFileName(fullPath);

        var provider = new PhysicalFileProvider(root);
        var parser = new EnsembleDocumentParser();
        return parser.LoadAsync(provider, fileName, model: null, cancellationToken);
    }

    /// <inheritdoc/>
    protected override ValueTask<ConfigDocument> TransformAsync(
        ConfigDocument document, CancellationToken cancellationToken) =>
        ValueTask.FromResult(document);
}
