using Tinkwell.Cli.Commands.Lint;
using Tinkwell.Measures.Configuration.Parser;

namespace Tinkwell.Cli.Commands.Measures.Lint.Rules;

sealed class MeasureNameIsUnique : Linter.Rule, ITwmLinterRule<ITwmFile>
{
    public Linter.Issue? Apply(ITwmFile file, object? parent, ITwmFile item)
    {
        var duplicates = file.Measures
            .Select(x => x.Name)
            .GroupBy(n => n)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Count != 0)
            return Critical<ITwmFile>("", $"These names are not unique: {string.Join(',', duplicates)}");

        return Ok();
    }
}