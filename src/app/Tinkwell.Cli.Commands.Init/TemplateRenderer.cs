using Fluid;

namespace Tinkwell.Cli.Commands.Init;

/// <summary>
/// Renders Liquid templates using Fluid against an <see cref="AnswerBag"/>.
/// Templates use native <c>==</c>/<c>!=</c> comparisons in conditionals:
/// <c>{% if topology == "balanced" %}</c>.
/// </summary>
internal sealed class TemplateRenderer
{
    private static readonly FluidParser Parser = new();
    private static readonly TemplateOptions SharedOptions = CreateOptions();

    private static TemplateOptions CreateOptions()
    {
        var options = new TemplateOptions();
        options.MemberAccessStrategy.Register<Dictionary<string, object>>();
        return options;
    }

    /// <summary>
    /// Loads and renders a <c>.liquid</c> template file with the given answers.
    /// </summary>
    public static async Task<string> RenderAsync(
        string templatePath, AnswerBag answers, CancellationToken cancellationToken = default)
    {
        var source = await File.ReadAllTextAsync(templatePath, cancellationToken);
        return Render(source, answers);
    }

    /// <summary>
    /// Renders a Liquid template string with the given answers.
    /// </summary>
    public static string Render(string templateSource, AnswerBag answers)
    {
        if (!Parser.TryParse(templateSource, out var template, out var error))
            throw new InvalidOperationException($"Liquid template parse error: {error}");

        var context = answers.ToTemplateContext(SharedOptions);
        return template.Render(context).TrimEnd();
    }
}
