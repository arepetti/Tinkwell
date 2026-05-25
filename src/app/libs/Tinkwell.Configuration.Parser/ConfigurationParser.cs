using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Tinkwell.Configuration;
using Tinkwell.Configuration.Parser.Parsing;
using Tinkwell.Expressions;
using Tinkwell.Telemetry;


namespace Tinkwell.Configuration.Parser;

/// <summary>
/// Abstract base class that orchestrates the Tinkwell configuration parsing pipeline.
/// Derived classes implement <see cref="TransformAsync"/> to convert the generic AST into
/// a domain-specific representation.
/// </summary>
/// <typeparam name="T">The type produced after transforming the parsed configuration.</typeparam>
/// <remarks>
/// <para>The parsing pipeline proceeds through four phases:</para>
/// <list type="number">
///   <item>
///     <term>Include Resolution</term>
///     <description>
///       <c>include "path"</c> directives are recursively inlined and a source map
///       is built for cross-file error reporting.
///     </description>
///   </item>
///   <item>
///     <term>Parsing</term>
///     <description>
///       The merged text (with comments stripped) is parsed by the Parlot-based grammar
///       into a raw AST, then locations are remapped from merged-line coordinates to
///       original file paths and lines (source map) before any further processing.
///     </description>
///   </item>
///   <item>
///     <term>Preprocessing</term>
///     <description>
///       <c>set</c> variables are resolved, <c>$"..."</c> interpolated strings are
///       rendered via Liquid, templates are expanded, and blocks with a failing
///       <c>if</c> condition are pruned. <see cref="System.Threading.CancellationToken"/>
///       is observed during this phase and during include resolution.
///     </description>
///   </item>
///   <item>
///     <term>Transformation</term>
///     <description>
///       The cleaned <see cref="ConfigDocument"/> is handed to the derived class's
///       <see cref="TransformAsync"/> method.
///     </description>
///   </item>
/// </list>
/// </remarks>
public abstract class ConfigurationParser<T> : IConfigurationParser<T>
{
    private readonly ILogger? _logger;
    private readonly IExpressionEvaluator _expressionEvaluator;

    /// <summary>
    /// Options controlling parser behavior (e.g., lax mode).
    /// Available to derived classes in <see cref="TransformAsync"/>.
    /// </summary>
    protected ParserOptions Options { get; }

    /// <summary>
    /// Initializes a new instance of the parser.
    /// </summary>
    /// <param name="logger">
    /// Optional logger for diagnostics. Trace-level messages include model properties,
    /// include resolution details, template expansion, and <c>if</c> evaluation results.
    /// Debug-level messages report pruned blocks.
    /// Error-level messages report missing include files and parse errors.
    /// </param>
    /// <param name="options">
    /// Optional parser options. When <see langword="null"/>,
    /// <see cref="ParserOptions.Default"/> is used.
    /// </param>
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Default ExpressionEvaluator discovers functions via reflection. Pass an explicit IExpressionEvaluator for AOT compatibility.")]
    protected ConfigurationParser(ILogger? logger = null, ParserOptions? options = null)
        : this(expressionEvaluator: null, logger, options)
    {
    }

    /// <summary>
    /// Initializes a new instance of the parser with an explicit expression evaluator.
    /// </summary>
    /// <param name="expressionEvaluator">
    /// The expression evaluator used to resolve <c>if</c> modifier conditions.
    /// </param>
    /// <param name="logger">
    /// Optional logger for diagnostics.
    /// </param>
    /// <param name="options">
    /// Optional parser options. When <see langword="null"/>,
    /// <see cref="ParserOptions.Default"/> is used.
    /// </param>
    protected ConfigurationParser(IExpressionEvaluator? expressionEvaluator, ILogger? logger = null, ParserOptions? options = null)
    {
        _expressionEvaluator = expressionEvaluator ?? CreateDefaultEvaluator();
        _logger = logger;
        Options = options ?? ParserOptions.Default;
    }

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "Fallback for when no evaluator is provided. AOT callers should pass an explicit evaluator.")]
    private static ExpressionEvaluator CreateDefaultEvaluator() => new();

    /// <inheritdoc/>
    public async Task<T> LoadAsync(
        IFileProvider fileProvider,
        string path,
        object? model = null,
        CancellationToken cancellationToken = default)
    {
        using var span = OtTraces.Source.Timed(OtTraces.Parse, OtMetrics.ParseDuration,
            (OtTraces.ConfigPath, path));

        var resolver = new IncludeResolver(fileProvider, _logger);
        var (mergedText, sourceMap, warnings) = await resolver.ResolveAsync(path, cancellationToken);

        var grammar = TwGrammar.Instance;
        var rawDocument = grammar.Parse(mergedText, path, sourceMap);

        var remappedDocument = RawAstRemapper.Remap(rawDocument, sourceMap);

        var preprocessor = new Preprocessor(model, EvaluateIfExpressionAsync, _logger);
        var document = await preprocessor.ProcessAsync(remappedDocument, warnings, cancellationToken);

        // Surface parse-time warnings to the logger as a safety net: derived
        // parsers are free to ignore ConfigDocument.Warnings, but a WARN log
        // still tells the operator that something was off (e.g. a duplicate
        // include was silently skipped).
        foreach (var warning in document.Warnings)
        {
            _logger?.LogWarning("Configuration warning: {Diagnostic}", warning);
        }

        var result = await TransformAsync(document, cancellationToken);

        OtMetrics.FilesParsed.Add(1);

        return result;
    }

    /// <summary>
    /// Transforms the fully preprocessed <see cref="ConfigDocument"/> into the
    /// target type <typeparamref name="T"/>. Called once per <see cref="LoadAsync"/>
    /// invocation after all preprocessing is complete.
    /// </summary>
    /// <param name="document">
    /// The document with all includes inlined, variables resolved,
    /// templates expanded, and conditional blocks pruned.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The domain-specific configuration object.</returns>
    protected abstract ValueTask<T> TransformAsync(
        ConfigDocument document, CancellationToken cancellationToken);

    /// <summary>
    /// Evaluates an <c>if</c> modifier expression to decide whether a block
    /// should be kept or pruned from the AST.
    /// </summary>
    /// <param name="expression">The raw expression text from the <c>if</c> modifier.</param>
    /// <param name="variables">
    /// The current set of resolved variables (from model and <c>set</c> directives).
    /// </param>
    /// <returns>
    /// <see langword="true"/> to keep the block; <see langword="false"/> to prune it.
    /// The default implementation evaluates the expression using
    /// <see cref="IExpressionEvaluator.EvaluateBooleanAsync"/>, passing all current
    /// variables as NCalc parameters.
    /// </returns>
    protected virtual Task<bool> EvaluateIfExpressionAsync(
        string expression,
        IReadOnlyDictionary<string, ConfigValue> variables)
    {
        var parameters = new Dictionary<string, object?>(variables.Count, StringComparer.Ordinal);
        foreach (var (key, value) in variables)
            parameters[key] = ConfigValueToObject(value);

        return _expressionEvaluator.EvaluateBooleanAsync(expression, parameters);
    }

    private static object? ConfigValueToObject(ConfigValue value) => value switch
    {
        StringValue s => s.Value,
        ExpressionValue e => e.Expression,
        LongValue l => l.Value,
        DoubleValue d => d.Value,
        BoolValue b => b.Value,
        InterpolatedStringValue i => i.Template,
        _ => value.ToString()
    };
}
