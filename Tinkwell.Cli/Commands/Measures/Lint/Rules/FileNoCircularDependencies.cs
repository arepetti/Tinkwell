using Tinkwell.Bootstrapper.Reflection;
using Tinkwell.Cli.Commands.Lint;
using Tinkwell.Measures.Configuration.Parser;

namespace Tinkwell.Cli.Commands.Measures.Lint.Rules;

sealed class FileNoCircularDependencies : Linter.Rule, ITwmLinterRule<ITwmFile>
{
    public Linter.Issue? Apply(ITwmFile file, object? parent, ITwmFile item)
    {
        var measures = file.Measures.Select(x => ShallowCloner.CopyAllPublicProperties(x, new Measure()));
        var dependencyWalker = new DependencyWalker<Measure>();
        if (!dependencyWalker.Analyze(measures))
            return new Linter.Issue(Id, Linter.IssueSeverity.Critical, "File", "", "Circular dependency detected.");

        return Ok();
    }


    sealed class Measure : MeasureDefinition, IMeasureDependent
    {
        public IList<string> Dependencies { get; set; } = [];
    }
}
