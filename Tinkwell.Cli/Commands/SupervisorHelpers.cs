using System.Text.RegularExpressions;
using Spectre.Console;
using Tinkwell.Bootstrapper.Expressions;
using Tinkwell.Bootstrapper.Ipc;

namespace Tinkwell.Cli.Commands;

static class SupervisorHelpers
{
    public static async Task<(int ExitCode, string[] Runners)> FindAllRunners(NamedPipeClient client)
    {
        var reply = await client.SendCommandAndWaitReplyAsync("runners list") ?? "";

        if (reply.StartsWith("Error:"))
        {
            Consoles.Error.MarkupLineInterpolated($"The Supervisor replied with: [red]{reply}[/]");
            return (ExitCode.Error, []);
        }

        return (ExitCode.Ok, reply.Split(','));
    }

    // name => address
    public static async Task<(int ExitCode, string Address)> QueryAddressAsync(NamedPipeClient client, string name)
    {
        var runners = await FindAllRunners(client);

        var runner = runners.Runners.FirstOrDefault(x => x.Equals(name, StringComparison.Ordinal));
        if (runner is null)
        {
            Consoles.Error.MarkupLineInterpolated($"[red]Error[/]: runner [cyan]{name}[/] does not exist.");
            return (ExitCode.InvalidArgument, "");
        }

        string address = await client.SendCommandAndWaitReplyAsync($"endpoints query \"{name}\"") ?? "";
        if (address.StartsWith("Error:"))
            address = "";

        return (ExitCode.Ok, address);
    }

    // partial name => name
    public static async Task<(int ExitCode, string FullName)> FindNameByGlobNameAsync(NamedPipeClient client, string search)
    {
        var runners = await FindAllRunners(client);

        var regex = new Regex(TextHelpers.GitLikeWildcardToRegex(search), RegexOptions.IgnoreCase);
        var runner = runners.Runners.SingleOrDefault(regex.IsMatch);
        if (runner is null)
            return (ExitCode.NoResults, "");

        return (ExitCode.Ok, runner);
    }

    // role => name
    public static async Task<(int ExitCode, string Value)> FindNameByRoleAsync(NamedPipeClient client, string role)
    {
        string address = await client.SendCommandAndWaitReplyAsync($"roles query \"{role}\"") ?? "";
        if (address.StartsWith("Error:"))
            return (ExitCode.NoResults, "");

        return await FindNameByHostAsync(client, address);
    }

    // host => name
    public static async Task<(int ExitCode, string Value)> FindNameByHostAsync(NamedPipeClient client, string address)
    {
        var pool = await FindAllRunners(client);
        if (pool.ExitCode != ExitCode.Ok)
            return (ExitCode.NoResults, "");

        foreach (var candidate in pool.Runners)
        {
            var (_, candidateAddress) = await QueryAddressAsync(client, candidate);
            if (string.Equals(candidateAddress, address, StringComparison.OrdinalIgnoreCase))
                return (ExitCode.Ok, candidate);
        }

        return (ExitCode.NoResults, "");
    }
}