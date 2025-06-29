using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using Tinkwell.Bootstrapper.Expressions;

namespace Tinkwell.EventsGateway.Services;

sealed class RegexCache
{
    public bool IsMatch(string input, string? pattern)
    {
        if (string.IsNullOrEmpty(pattern))
            return true;

        return Get(TextHelpers.GitLikeWildcardToRegex(pattern)).IsMatch(input);
    }

    private Regex Get(string pattern, RegexOptions options = RegexOptions.None)
    {
        // Clear cache if it exceeds a certain size to prevent memory bloat. OK, it's a bit arbitrary and
        // an naive, let's see if we need to switch to something better.
        if (_cache.Count > 50)
            _cache.Clear(); 

        var key = $"{options}|{pattern}";
        return _cache.GetOrAdd(key, _ => new Regex(pattern, options | RegexOptions.Compiled));
    }

    private readonly ConcurrentDictionary<string, Regex> _cache = new();
}
