using Tinkwell.Coordinator.ProcessManagement;

namespace Tinkwell.Coordinator.Tests;

public class RestartPolicyOptionsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var options = new RestartPolicyOptions();

        Assert.Equal(3, options.MaxRestartsInWindow);
        Assert.Equal(60, options.RestartWindowInSeconds);
        Assert.False(options.QuitOnRunnerCrash);
    }

    [Fact]
    public void RestartWindow_ReturnsTimeSpanFromSeconds()
    {
        var options = new RestartPolicyOptions { RestartWindowInSeconds = 120 };
        Assert.Equal(TimeSpan.FromSeconds(120), options.RestartWindow);
    }
}
