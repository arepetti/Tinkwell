namespace Tinkwell.Lwm2m;

/// <summary>
/// Parses and represents an LwM2M URI path. Paths have the form
/// <c>/{objectId}[/{instanceId}[/{resourceId}]]</c>
/// (OMA-TS-LightweightM2M_Core-V1_1, Section 6.3.2).
/// </summary>
public readonly record struct Lwm2mPath
{
    /// <summary>The object ID segment (e.g. <c>3</c> for the Device object).</summary>
    public int ObjectId { get; }
    /// <summary>The instance ID when the path is at instance or resource depth; null for object-level paths.</summary>
    public int? InstanceId { get; }
    /// <summary>The resource ID when the path is at resource depth; null otherwise.</summary>
    public int? ResourceId { get; }

    /// <summary>Creates a path with the given object, instance, and optional resource ID.</summary>
    /// <example>
    /// <para>Build a resource path for a temperature object instance 0, resource 5700 (IPSO sensor value) without parsing a string.</para>
    /// <code language="csharp">
    /// var path = new Lwm2mPath(3303, 0, 5700);
    /// string uri = path.ToString(); // "/3303/0/5700"
    /// </code>
    /// </example>
    /// <param name="objectId">OMA object identifier (e.g. <c>3303</c> for Temperature).</param>
    /// <param name="instanceId">Instance index within the object, or <c>null</c> for an object-level path.</param>
    /// <param name="resourceId">Resource identifier within the instance, or <c>null</c> for an instance-level path.</param>
    public Lwm2mPath(int objectId, int? instanceId = null, int? resourceId = null)
    {
        ObjectId = objectId;
        InstanceId = instanceId;
        ResourceId = resourceId;
    }

    /// <summary>True when the path is object-level only (no instance or resource segment).</summary>
    public bool IsObject => InstanceId is null;
    /// <summary>True when the path names an object instance (has instance, no resource).</summary>
    public bool IsInstance => InstanceId is not null && ResourceId is null;
    /// <summary>True when the path names a resource (instance and resource are present).</summary>
    public bool IsResource => ResourceId is not null;

    /// <summary>
    /// Parses an LwM2M URI path string. Returns false if the path is not
    /// a valid LwM2M path (one to three numeric segments; a leading slash is optional
    /// and ignored after splitting on <c>'/'</c>).
    /// </summary>
    /// <example>
    /// <para>Read paths from CoAP URIs, e.g. object 3303, instance 0, resource 5700 (sensor value URI).</para>
    /// <code language="csharp">
    /// if (Lwm2mPath.TryParse("/3303/0/5700", out var p) &amp;&amp; p.IsResource)
    /// {
    ///     int? valueResource = p.ResourceId; // 5700
    /// }
    /// </code>
    /// </example>
    /// <param name="path">LwM2M URI path to parse (e.g. <c>"/3303/0/5700"</c>). A leading slash is optional.</param>
    /// <param name="result">When the method returns <c>true</c>, the parsed path; otherwise <c>default</c>.</param>
    public static bool TryParse(string? path, out Lwm2mPath result)
    {
        result = default;
        if (string.IsNullOrEmpty(path))
            return false;

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length is < 1 or > 3)
            return false;

        if (!int.TryParse(segments[0], out var objectId))
            return false;

        int? instanceId = null;
        int? resourceId = null;

        if (segments.Length >= 2)
        {
            if (!int.TryParse(segments[1], out var inst))
                return false;
            instanceId = inst;
        }

        if (segments.Length == 3)
        {
            if (!int.TryParse(segments[2], out var res))
                return false;
            resourceId = res;
        }

        result = new Lwm2mPath(objectId, instanceId, resourceId);
        return true;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        if (ResourceId.HasValue)
            return $"/{ObjectId}/{InstanceId}/{ResourceId}";
        if (InstanceId.HasValue)
            return $"/{ObjectId}/{InstanceId}";
        return $"/{ObjectId}";
    }
}
