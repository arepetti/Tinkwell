namespace Tinkwell.Runlet.Lwm2m.Configuration;

/// <summary>
/// Root configuration for all LwM2M server definitions parsed from <c>.tw</c> files.
/// </summary>
public sealed record Lwm2mConfig(IReadOnlyList<Lwm2mServerDefinition> Servers);
