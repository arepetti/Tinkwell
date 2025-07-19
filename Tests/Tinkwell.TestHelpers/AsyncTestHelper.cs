
using System.Diagnostics;

namespace Tinkwell.TestHelpers;

public static class AsyncTestHelper
{
    public static async Task WaitForCondition(Func<bool> condition, int timeoutMs = 2000, string? failureMessage = null)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < timeoutMs)
        {
            if (condition())
            {
                return; // Condition met, success.
            }
            await Task.Delay(100); // Poll every 100ms
        }
        throw new TimeoutException(failureMessage ?? $"Timed out after {timeoutMs}ms waiting for condition.");
    }
}
