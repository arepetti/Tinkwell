return args switch
{
    [] => PrintUsage(),
    ["pack", .. var rest] => await PackCommand.RunAsync(rest),
    [var verb, ..] => Error($"Unknown command '{verb}'. Run without arguments for usage."),
};

static int PrintUsage()
{
    Console.WriteLine("Usage: tinkwell-ci-package <command> [options]");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  pack <dir> -o <output.twpkg> [--sign] [--key-env <VAR>]");
    return 1;
}

static int Error(string message)
{
    Console.Error.WriteLine($"error: {message}");
    return 1;
}
