namespace Tinkwell.Coordinator.Configuration;

/// <summary>
/// Root configuration produced by parsing a <c>.tw</c> ensemble file.
/// Contains the ordered list of runner definitions that the coordinator
/// will launch and manage.
/// </summary>
/// <param name="Runners">
/// The runner definitions, in the order they appear in the configuration file.
/// The coordinator launches them sequentially in this order.
/// </param>
public sealed record EnsembleConfig(IReadOnlyList<RunnerConfig> Runners);
