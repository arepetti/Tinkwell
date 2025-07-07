using Tinkwell.Cli.Commands.Lint;
using Tinkwell.Measures.Configuration.Parser;

namespace Tinkwell.Cli.Commands.Measures.Lint;

interface ITwmLinterRule<T>
{
    Linter.Issue? Apply(ITwmFile file, object? parent, T item);
}
