using static Tinkwell.Cli.Commands.Lint.Linter;

namespace Tinkwell.Cli.Commands.Lint;

interface IFileLinter
{
    string[] Exclusions { get; set; }

    Task<IResult> CheckAsync(string path, bool strict);
}

