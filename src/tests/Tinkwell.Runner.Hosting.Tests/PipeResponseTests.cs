using System.Text.Json;
using Tinkwell.Runner.Hosting;

namespace Tinkwell.Runner.Hosting.Tests;

public class PipeResponseTests
{
    [Fact]
    public void IsOk_WhenStatusIsOk_ReturnsTrue()
    {
        var response = new PipeResponse("ok", null, null);
        Assert.True(response.IsOk);
    }

    [Fact]
    public void IsOk_WhenStatusIsError_ReturnsFalse()
    {
        var response = new PipeResponse("error", "something failed", null);
        Assert.False(response.IsOk);
    }

    [Fact]
    public void EnsureSuccess_WhenOk_DoesNotThrow()
    {
        var response = new PipeResponse("ok", null, null);
        response.EnsureSuccess();
    }

    [Fact]
    public void EnsureSuccess_WhenError_ThrowsWithMessage()
    {
        var response = new PipeResponse("error", "bad request", null);
        var ex = Assert.Throws<IOException>(() => response.EnsureSuccess());
        Assert.Contains("bad request", ex.Message);
    }

    [Fact]
    public void Deserialization_FromJsonl_Works()
    {
        var json = """{"status":"ok","data":[{"name":"r1","assemblyPath":"r1.dll","settings":{"k":"v"}}]}""";
        var response = JsonSerializer.Deserialize<PipeResponse>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        })!;

        Assert.True(response.IsOk);
        Assert.NotNull(response.Data);
        Assert.Equal(JsonValueKind.Array, response.Data.Value.ValueKind);
    }

    [Fact]
    public void Deserialization_ErrorEnvelope_Works()
    {
        var json = """{"status":"error","message":"unknown runner ID"}""";
        var response = JsonSerializer.Deserialize<PipeResponse>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        })!;

        Assert.False(response.IsOk);
        Assert.Equal("unknown runner ID", response.Message);
    }
}
