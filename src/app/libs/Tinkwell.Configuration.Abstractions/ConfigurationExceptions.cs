namespace Tinkwell.Configuration;

/// <summary>
/// Base exception for all errors originating from the Tinkwell configuration
/// parsing pipeline. Every instance carries the source file name and line
/// number where the error was detected.
/// </summary>
public class ConfigurationException : TinkwellException
{
    /// <summary>
    /// The path of the configuration file where the error occurred.
    /// </summary>
    public string FileName { get; }

    /// <summary>
    /// The 1-based line number in <see cref="FileName"/> where the error was detected.
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// Creates a new <see cref="ConfigurationException"/>.
    /// </summary>
    /// <param name="message">A human-readable description of the error.</param>
    /// <param name="fileName">Path to the source file.</param>
    /// <param name="line">1-based line number.</param>
    /// <param name="innerException">Optional inner exception.</param>
    public ConfigurationException(
        string message, string fileName, int line, Exception? innerException = null)
        : base($"{fileName}:{line}: {message}", innerException)
    {
        FileName = fileName;
        Line = line;
    }
}

/// <summary>
/// Describes a single diagnostic produced during parsing or preprocessing.
/// Used by <see cref="ConfigurationSyntaxException"/> to report multiple
/// errors from a single parsing pass.
/// </summary>
/// <param name="Message">A human-readable description of the error.</param>
/// <param name="FileName">Path to the source file.</param>
/// <param name="Line">1-based line number.</param>
/// <param name="Column">1-based column number.</param>
public sealed record ConfigurationDiagnostic(
    string Message, string FileName, int Line, int Column)
{
    /// <inheritdoc/>
    public override string ToString() => $"{FileName}:{Line}:{Column}: {Message}";
}

/// <summary>
/// Thrown when the configuration text contains syntax errors or semantic
/// errors detected during preprocessing (undefined templates, duplicate
/// definitions, invalid interpolation, etc.).
/// </summary>
/// <remarks>
/// When the preprocessor detects multiple errors in a single pass, all of
/// them are collected in <see cref="Diagnostics"/>. The exception message
/// reflects the first diagnostic.
/// </remarks>
public class ConfigurationSyntaxException : ConfigurationException
{
    /// <summary>
    /// The 1-based column number of the primary error.
    /// </summary>
    public int Column { get; }

    /// <summary>
    /// All diagnostics collected during the parsing or preprocessing pass.
    /// Contains at least one entry.
    /// </summary>
    public IReadOnlyList<ConfigurationDiagnostic> Diagnostics { get; }

    /// <summary>
    /// Creates a syntax exception from a single error.
    /// </summary>
    public ConfigurationSyntaxException(
        string message, string fileName, int line, int column)
        : base(message, fileName, line)
    {
        Column = column;
        Diagnostics = [new ConfigurationDiagnostic(message, fileName, line, column)];
    }

    /// <summary>
    /// Creates a syntax exception from multiple diagnostics.
    /// </summary>
    /// <param name="diagnostics">One or more diagnostics. Must not be empty.</param>
    /// <exception cref="ArgumentException"><paramref name="diagnostics"/> is empty.</exception>
    public ConfigurationSyntaxException(IReadOnlyList<ConfigurationDiagnostic> diagnostics)
        : base(
            FormatMessage(diagnostics),
            diagnostics.Count > 0 ? diagnostics[0].FileName : "",
            diagnostics.Count > 0 ? diagnostics[0].Line : 0)
    {
        Column = diagnostics.Count > 0 ? diagnostics[0].Column : 0;
        Diagnostics = diagnostics;
    }

    private static string FormatMessage(IReadOnlyList<ConfigurationDiagnostic> diagnostics)
    {
        if (diagnostics.Count == 0)
            return "Configuration parsing failed.";
        if (diagnostics.Count == 1)
            return diagnostics[0].Message;
        return $"{diagnostics[0].Message} (+{diagnostics.Count - 1} more error(s))";
    }
}

/// <summary>
/// Thrown when an <c>include</c> directive references a file that cannot
/// be found by the configured file provider.
/// </summary>
public class ConfigurationFileNotFoundException : ConfigurationException
{
    /// <summary>
    /// The path that was requested in the <c>include</c> directive.
    /// </summary>
    public string IncludePath { get; }

    /// <summary>
    /// Creates a new <see cref="ConfigurationFileNotFoundException"/>.
    /// </summary>
    /// <param name="includePath">The unresolved include path.</param>
    /// <param name="referencedFromFile">
    /// The file containing the <c>include</c> directive, or the include
    /// path itself if it is the root file.
    /// </param>
    /// <param name="referencedFromLine">Line number of the directive.</param>
    public ConfigurationFileNotFoundException(
        string includePath, string referencedFromFile, int referencedFromLine)
        : base($"Include file '{includePath}' not found", referencedFromFile, referencedFromLine)
    {
        IncludePath = includePath;
    }
}

/// <summary>
/// Thrown when a configuration value (the <c>ConfigValue</c> hierarchy in
/// <c>Tinkwell.Configuration.Parser</c>) cannot be converted to the
/// requested CLR type (unsupported conversion, data loss, or overflow).
/// </summary>
public class ConfigurationConversionException : ConfigurationException
{
    /// <summary>
    /// The CLR type that was requested.
    /// </summary>
    public Type TargetType { get; }

    /// <summary>
    /// Creates a new <see cref="ConfigurationConversionException"/>.
    /// </summary>
    /// <param name="message">Description of the conversion failure.</param>
    /// <param name="fileName">Path to the source file containing the value.</param>
    /// <param name="line">Line number of the value.</param>
    /// <param name="targetType">The CLR type that was requested.</param>
    /// <param name="innerException">The underlying exception, if any.</param>
    public ConfigurationConversionException(
        string message, string fileName, int line, Type targetType,
        Exception? innerException = null)
        : base(message, fileName, line, innerException)
    {
        TargetType = targetType;
    }
}
