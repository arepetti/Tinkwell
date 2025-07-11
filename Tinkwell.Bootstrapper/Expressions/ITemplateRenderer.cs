namespace Tinkwell.Bootstrapper.Expressions;

/// <summary>
/// Defines a contract for rendering templates with parameters.
/// </summary>
public interface ITemplateRenderer
{
    /// <summary>
    /// Renders the specified content using the provided parameters.
    /// </summary>
    /// <param name="content">The template content to render.</param>
    /// <param name="parameters">The parameters to use for rendering.</param>
    /// <returns>The rendered string.</returns>
    string Render(string content, object? parameters);
}