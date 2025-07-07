using Tinkwell.Bootstrapper.Ensamble;
using Tinkwell.Cli.Commands.Lint;

namespace Tinkwell.Cli.Commands.Ensamble.Lint;

interface IEnsambleLinterRule<T>
{
    Linter.Issue? Apply(IEnsambleFile file, object? parent, T item);
}
