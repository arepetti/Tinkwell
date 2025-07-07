using Spectre.Console;
using Tinkwell.Bootstrapper.Ensamble;
using Tinkwell.Cli.Commands.Lint;

namespace Tinkwell.Cli.Commands.Ensamble.Lint.Rules;

// This is not, strictly speaking, a strict rule. To have runners with the same name is
// an error and it'll throw an exception when loading the file at run-time. However runners
// might be loaded conditionally and two runners with the same name are loaded according to
// mutually exclusive conditions. In that case there will never be two runners loaded at run-time
// with the same name and we reported a false positive. Making this strict we, at least, leave this
// out from the basic rules applied by default.
[Linter.Rule(strict: true)]
sealed class RunnerNameIsUnique : Linter.Rule, IEnsambleLinterRule<IEnsambleFile>
{
    public Linter.Issue? Apply(IEnsambleFile file, object? parent, IEnsambleFile item)
    {
        var names = Enumerable.Concat(
            file.Runners.Select(x => x.Name),
            file.Runners.SelectMany(x => x.Children.Select(c => c.Name)));

        var duplicates = names
            .GroupBy(n => n)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Count != 0)
            return Critical<IEnsambleFile>("", $"These names are not unique: {string.Join(',', duplicates)}. Ignore this rule if those runners are loaded on mutually exclusive conditions.");

        return Ok();
    }
}
