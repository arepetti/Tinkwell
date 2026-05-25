using System.Net;

namespace Tinkwell.Lwm2m.Registration;

/// <summary>
/// Parses LwM2M registration request parameters from CoAP query strings
/// and link-format payloads (OMA-TS-LightweightM2M_Transport-V1_1, Section 5.3.1).
/// </summary>
public static class RegistrationParser
{
    /// <summary>
    /// Default registration lifetime in seconds
    /// (OMA-TS-LightweightM2M_Core-V1_1, Section 5.3, Table 6.1).
    /// </summary>
    public const int DefaultLifetimeSeconds = 86400;

    /// <summary>
    /// Parses a registration POST to /rd.
    /// Query parameters: ep, lt, lwm2m, b.
    /// Payload: RFC 6690 link-format listing supported objects.
    /// </summary>
    /// <example>
    /// <para>Turn the CoAP query and link-format body into a <see cref="Lwm2mRegistration"/> before calling <see cref="RegistrationDirectory.Register"/>.</para>
    /// <code language="csharp">
    /// var remote = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 5683);
    /// var links = Tinkwell.Lwm2m.LinkFormatBuilder.BuildRegistrationPayload(new[] { "3/0", "3303/0" });
    /// Lwm2mRegistration reg = RegistrationParser.Parse("ep=dev-1&amp;lt=600", links, remote);
    /// // reg.Lifetime 600, reg.Objects from links, reg.Endpoint "dev-1"
    /// </code>
    /// </example>
    /// <param name="query">CoAP query string from the registration POST (e.g. <c>"ep=dev-1&amp;lt=600"</c>). May be <c>null</c> if no query options were present.</param>
    /// <param name="payload">RFC 6690 link-format body listing the objects the client supports (e.g. <c>"&lt;/3/0&gt;,&lt;/3303/0&gt;"</c>). May be <c>null</c> or empty.</param>
    /// <param name="remoteEndpoint">Network address of the registering client; used as fallback endpoint name when <c>ep=</c> is absent.</param>
    public static Lwm2mRegistration Parse(
        string? query, string? payload, IPEndPoint remoteEndpoint)
    {
        var queryParams = ParseQueryParameters(query);

        var endpoint = queryParams.GetValueOrDefault("ep")
            ?? remoteEndpoint.ToString();

        int lifetime = DefaultLifetimeSeconds;
        if (queryParams.TryGetValue("lt", out var ltStr) && int.TryParse(ltStr, out var lt))
            lifetime = lt;

        var version = queryParams.GetValueOrDefault("lwm2m");
        var bindingMode = queryParams.GetValueOrDefault("b");

        var objects = ParseLinkFormat(payload);

        return new Lwm2mRegistration
        {
            Endpoint = endpoint,
            Address = remoteEndpoint,
            RegisteredAt = DateTimeOffset.UtcNow,
            Lifetime = lifetime,
            LwM2MVersion = version,
            BindingMode = bindingMode,
            Objects = objects,
            Location = "",
        };
    }

    /// <summary>
    /// Parses query string of the form "ep=device1&amp;lt=300".
    /// CoAP query options are already split by '&amp;' at the protocol level,
    /// but we also handle the combined form.
    /// </summary>
    /// <example>
    /// <para>Read <c>ep=</c> and <c>lt=</c> from a raw query or combined option string (Section 5.3.1).</para>
    /// <code language="csharp">
    /// var q = RegistrationParser.ParseQueryParameters("ep=lab-sensor&amp;lt=300");
    /// string? ep = q.GetValueOrDefault("ep");
    /// string? lifetime = q.GetValueOrDefault("lt");
    /// </code>
    /// </example>
    /// <param name="query">Raw query string such as <c>"ep=device1&amp;lt=300"</c>. Returns an empty dictionary when <c>null</c> or whitespace-only.</param>
    public static Dictionary<string, string> ParseQueryParameters(string? query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(query))
            return result;

        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');
            if (eq > 0)
                result[part[..eq].Trim()] = Uri.UnescapeDataString(part[(eq + 1)..].Trim());
        }

        return result;
    }

    /// <summary>
    /// Parses an RFC 6690 link-format payload to extract object/instance paths.
    /// Example payload: <c>&lt;/3/0&gt;,&lt;/3303/0&gt;,&lt;/3304/0&gt;</c>
    /// </summary>
    internal static List<Lwm2mPath> ParseLinkFormat(string? payload)
    {
        var result = new List<Lwm2mPath>();
        if (string.IsNullOrWhiteSpace(payload))
            return result;

        foreach (var link in payload.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = link.Trim();
            var startAngle = trimmed.IndexOf('<');
            var endAngle = trimmed.IndexOf('>');
            if (startAngle < 0 || endAngle <= startAngle)
                continue;

            var path = trimmed[(startAngle + 1)..endAngle];
            if (Lwm2mPath.TryParse(path, out var parsed))
                result.Add(parsed);
        }

        return result;
    }
}
