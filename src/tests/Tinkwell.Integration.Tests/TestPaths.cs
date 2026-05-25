using System.Reflection;

namespace Tinkwell.Integration.Tests;

/// <summary>
/// Resolves paths to the shared artifacts directory and the executables
/// built there, using the <c>ArtifactsDir</c> assembly metadata embedded
/// at build time.
/// </summary>
internal static class TestPaths
{
    public static string ArtifactsDir { get; } =
        typeof(TestPaths).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(a => a.Key == "ArtifactsDir").Value
        ?? throw new InvalidOperationException("ArtifactsDir metadata not found");

    public static string CoordinatorDll =>
        Path.Combine(ArtifactsDir, "Tinkwell.Coordinator.dll");

    public static string HeadlessRunnerDll =>
        Path.Combine(ArtifactsDir, "Tinkwell.Runner.Headless.dll");

    public static string TestRunletDll =>
        Path.Combine(ArtifactsDir, "Tinkwell.Runlet.Test.dll");

    public static string GrpcRunnerDll =>
        Path.Combine(ArtifactsDir, "Tinkwell.Runner.Grpc.dll");

    public static string StoreRunletDll =>
        Path.Combine(ArtifactsDir, "Tinkwell.Runlet.Store.dll");

    public static string CliDll =>
        Path.Combine(ArtifactsDir, "tw.dll");
}
