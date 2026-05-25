using Tinkwell.Encoding;

namespace Tinkwell.Lwm2m;

/// <summary>
/// LwM2M resource access operations used in this library. The full specification defines
/// additional operations (OMA-TS-LightweightM2M_Core-V1_1, Section 6.1, Table 7); this
/// enum covers the most common <c>rw</c>-style and execute flags for object definitions
/// in <see cref="Lwm2mResourceDefinition"/> and the IPSO registry.
/// </summary>
[Flags]
public enum Lwm2mOperations
{
    /// <summary>No operation.</summary>
    None = 0,
    /// <summary>Read (GET).</summary>
    Read = 1,
    /// <summary>Write (PUT/POST to resource value).</summary>
    Write = 2,
    /// <summary>Execute (POST to resource without a value).</summary>
    Execute = 4,
    /// <summary>Read and write combined.</summary>
    ReadWrite = Read | Write,
}

/// <summary>
/// Definition of an LwM2M resource within an object.
/// </summary>
public sealed record Lwm2mResourceDefinition(
    int ResourceId,
    string Name,
    PayloadType Type,
    Lwm2mOperations Operations,
    bool Mandatory = false,
    bool Multiple = false);

/// <summary>
/// Definition of an LwM2M object.
/// </summary>
public sealed record Lwm2mObjectDefinition(
    int ObjectId,
    string Name,
    bool Multiple = true,
    bool Mandatory = false,
    IReadOnlyList<Lwm2mResourceDefinition>? Resources = null);
