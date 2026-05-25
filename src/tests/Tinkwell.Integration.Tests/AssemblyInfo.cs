using Xunit;

// Coordinators bind fixed/default ports; run integration tests sequentially to avoid conflicts.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
