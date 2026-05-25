using Microsoft.Extensions.Logging;

namespace Tinkwell.Configuration.Parser.Tests;

/// <summary>
/// Concrete parser for testing: returns ConfigDocument directly,
/// relies on the base class EvaluateIfExpression implementation.
/// </summary>
public class TestParser : ConfigurationParser<ConfigDocument>
{
    public TestParser(ILogger? logger = null) : base(logger: logger) { }

    protected override ValueTask<ConfigDocument> TransformAsync(
        ConfigDocument document, CancellationToken cancellationToken) =>
        ValueTask.FromResult(document);
}
