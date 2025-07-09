using Tinkwell.Actions.Configuration.Parser;
using Tinkwell.Cli.Commands.Lint;

namespace Tinkwell.Cli.Commands.Ensamble.Lint;

interface ITwaLinterRule<T>
{
    IEnumerable<Linter.Issue?> Apply(ITwaFile file, object? parent, T item);
}
