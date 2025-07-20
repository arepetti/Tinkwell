namespace Tinkwell.Bootstrapper.Tests;

public class AsyncExtensionsTests
{
    [Fact]
    public async Task ConsumeAllAsync_ConsumesAllItems()
    {
        var source = new List<int> { 1, 2, 3 }.ToAsyncEnumerable();
        var result = await source.ConsumeAllAsync();
        Assert.Equal([1, 2, 3], result);
    }
}

file static class AsyncEnumerableHelper
{
    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            await Task.Yield();
            yield return item;
        }
    }
}
