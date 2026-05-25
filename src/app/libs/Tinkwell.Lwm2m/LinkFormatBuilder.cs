using System;

namespace Tinkwell.Lwm2m;

/// <summary>
/// Builds RFC 6690 CoRE Link-Format payloads for LwM2M registration.
/// </summary>
public static class LinkFormatBuilder
{
    /// <summary>
    /// Builds a registration payload from a list of object paths.
    /// Each path should be in the form <c>objectId/instanceId</c>
    /// (e.g. <c>"3/0"</c>, <c>"3303/0"</c>).
    /// </summary>
    /// <returns>
    /// A link-format string like <c>&lt;/3/0&gt;,&lt;/3303/0&gt;,&lt;/3304/0&gt;</c>.
    /// </returns>
    /// <example>
    /// <para>Form the link-format body for a registration <c>POST /rd</c> listing object instances the client supports.</para>
    /// <code language="csharp">
    /// var links = LinkFormatBuilder.BuildRegistrationPayload(
    ///     new[] { "3/0", "3303/0", "3304/0" });
    /// // e.g. "&lt;/3/0&gt;,&lt;/3303/0&gt;,&lt;/3304/0&gt;"
    /// </code>
    /// </example>
    /// <param name="objectPaths">Object/instance paths to include (e.g. <c>"3/0"</c>, <c>"3303/0"</c>). Leading slashes are trimmed automatically. Must not be <c>null</c>.</param>
    public static string BuildRegistrationPayload(IEnumerable<string> objectPaths)
    {
        ArgumentNullException.ThrowIfNull(objectPaths);
        return string.Join(",", objectPaths.Select(p =>
        {
            var trimmed = p.TrimStart('/');
            return $"</{trimmed}>";
        }));
    }
}
