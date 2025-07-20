
using System.Diagnostics;

namespace Tinkwell.TestHelpers;

public static class AsyncTestHelper
{
    public static async Task WaitForCondition(Func<bool> condition, int timeoutMs = 10000, string? failureMessage = null)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < timeoutMs)
        {
            if (condition())
            {
                return; // Condition met, success.
            }
            await Task.Delay(50); // Poll every 50ms
        }
        throw new TimeoutException(failureMessage ?? $"Timed out after {timeoutMs}ms waiting for condition.");
    }
}
