namespace Tinkwell.Bootstrapper.Expressions;

public interface ITemplateRenderer
{
    string Render(string content, object? parameters);
}