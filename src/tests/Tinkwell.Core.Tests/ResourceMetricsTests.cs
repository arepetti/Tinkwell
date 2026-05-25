using System.Diagnostics;
using Tinkwell.Diagnostics;

namespace Tinkwell.Core.Tests;

public class ResourceMetricsTests
{
    [Fact]
    public void GetCpuPercent_ZeroElapsed_ReturnsZero()
    {
        using var p = Process.GetCurrentProcess();
        Assert.Equal(0, ResourceMetrics.GetCpuPercent(p, p.TotalProcessorTime, TimeSpan.Zero));
    }

    [Fact]
    public void GetCpuPercent_SameCpuTime_ReturnsZero()
    {
        using var p = Process.GetCurrentProcess();
        var cpu = p.TotalProcessorTime;
        Assert.Equal(0, ResourceMetrics.GetCpuPercent(p, cpu, TimeSpan.FromMilliseconds(100)));
    }

    [Fact]
    public void GetWorkingSetBytes_CurrentProcess_IsPositive()
    {
        using var p = Process.GetCurrentProcess();
        Assert.True(ResourceMetrics.GetWorkingSetBytes(p) > 0);
    }

    [Fact]
    public void GetThreadCount_CurrentProcess_IsPositive()
    {
        using var p = Process.GetCurrentProcess();
        Assert.True(ResourceMetrics.GetThreadCount(p) > 0);
    }
}
