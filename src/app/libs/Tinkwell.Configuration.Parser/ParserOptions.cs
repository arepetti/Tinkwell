namespace Tinkwell.Configuration.Parser;

/// <summary>
/// Options that control the behavior of <see cref="ConfigurationParser{T}"/>
/// and its derived classes.
/// </summary>
public sealed record ParserOptions
{
    /// <summary>
    /// <para>
    /// Hint for derived <see cref="ConfigurationParser{T}"/> implementations:
    /// when <see langword="true"/>, a custom <c>TransformAsync</c> may choose to
    /// tolerate or skip content it does not understand (for example, top-level
    /// blocks for another subsystem) instead of failing. The base
    /// <see cref="ConfigurationParser{T}"/> pipeline does not interpret this flag;
    /// it is not enforced during parse or preprocess because the base class has
    /// no application-specific notion of "unknown" block types.
    /// </para>
    /// <para>
    /// Use this when a single <c>.tw</c> file is consumed by several parsers, each
    /// handling a different set of block types, and a derived transform wants a
    /// non-strict mode.
    /// </para>
    /// </summary>
    public bool Lax { get; init; }

    /// <summary>
    /// The default options: <see cref="Lax"/> is <see langword="false"/>.
    /// </summary>
    public static ParserOptions Default { get; } = new();
}
