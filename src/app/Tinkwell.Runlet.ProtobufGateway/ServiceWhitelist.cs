using Tinkwell.Configuration.Parser;
using Tinkwell.Runlet.ProtobufGateway.Configuration;

namespace Tinkwell.Runlet.ProtobufGateway;

/// <summary>
/// Evaluates whether a proto service name is permitted by a set of glob-style
/// allow rules. An empty rule set denies all services; <c>"*"</c> allows all.
/// </summary>
internal sealed class ServiceWhitelist
{
    private readonly string[] _patterns;

    public ServiceWhitelist(IEnumerable<AllowRuleConfig> rules)
    {
        _patterns = rules.Select(r => r.ServicePattern).ToArray();
    }

    /// <summary>
    /// Returns <see langword="true"/> if the service name matches any allow pattern.
    /// Returns <see langword="false"/> when the rule set is empty (deny-all).
    /// </summary>
    public bool IsAllowed(string serviceName)
    {
        foreach (var pattern in _patterns)
        {
            if (Matches(pattern, serviceName))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Creates a new whitelist containing the union of this and <paramref name="other"/>.
    /// </summary>
    public ServiceWhitelist Merge(ServiceWhitelist other)
    {
        var combined = _patterns.Concat(other._patterns)
            .Distinct(StringComparer.Ordinal)
            .Select(p => new AllowRuleConfig(p, new SourceLocation("", 0, 0)));
        return new ServiceWhitelist(combined);
    }

    /// <summary>
    /// Matches a service name against a glob pattern.
    /// <c>"*"</c> matches everything. A trailing <c>.*</c> matches any
    /// name starting with the prefix (e.g. <c>"tinkwell.store.*"</c>
    /// matches <c>"tinkwell.store.v1.StateStore"</c>).
    /// An exact match is also supported.
    /// </summary>
    private static bool Matches(string pattern, string serviceName)
    {
        if (pattern == "*")
            return true;

        if (pattern.EndsWith(".*", StringComparison.Ordinal))
        {
            var prefix = pattern[..^1];
            return serviceName.StartsWith(prefix, StringComparison.Ordinal);
        }

        return string.Equals(pattern, serviceName, StringComparison.Ordinal);
    }
}
