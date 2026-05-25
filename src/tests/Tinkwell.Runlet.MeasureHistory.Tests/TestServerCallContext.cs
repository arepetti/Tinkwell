using System.Collections.Generic;
using Grpc.Core;

namespace Tinkwell.Runlet.MeasureHistory.Tests;

/// <summary>
/// Minimal <see cref="ServerCallContext"/> for unit tests (pattern from grpc-dotnet shared test helpers).
/// </summary>
internal sealed class TestServerCallContext : ServerCallContext
{
    private Status _status;
    private WriteOptions? _writeOptions;

    public TestServerCallContext(CancellationToken cancellationToken = default)
    {
        CancellationTokenCore = cancellationToken;
        DeadlineCore = DateTime.MaxValue;
        MethodCore = "Tests/Unary";
        HostCore = "unit.test";
        PeerCore = "127.0.0.1";
        RequestHeadersCore = Metadata.Empty;
        ResponseTrailersCore = Metadata.Empty;
        AuthContextCore = new AuthContext(null, new Dictionary<string, List<AuthProperty>>());
    }

    protected override string MethodCore { get; }
    protected override string HostCore { get; }
    protected override string PeerCore { get; }
    protected override DateTime DeadlineCore { get; }
    protected override Metadata RequestHeadersCore { get; }
    protected override CancellationToken CancellationTokenCore { get; }
    protected override Metadata ResponseTrailersCore { get; }
    protected override Status StatusCore
    {
        get => _status;
        set => _status = value;
    }

    protected override WriteOptions? WriteOptionsCore
    {
        get => _writeOptions;
        set => _writeOptions = value;
    }

    protected override AuthContext AuthContextCore { get; }

    protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) =>
        throw new NotSupportedException();

    protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
}
