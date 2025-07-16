
using System.Text.Json;

namespace Tinkwell.Bootstrapper.Tests;

public class ObjectJsonConverter_Basic
{
    private class SourceClass { public int A { get; set; } public string B { get; set; } }
    private class TargetClass { public int A { get; set; } public string B { get; set; } }

    [Fact]
    public void ObjectJsonConverter_DeserializesIntoDictionaryCorrectly()
    {
        var json = "{\"A\": 1, \"B\": \"test\", \"C\": true, \"D\": { \"E\": 3.14 }}";
        var options = new JsonSerializerOptions();
        options.Converters.Add(new ObjectJsonConverter());

        var result = JsonSerializer.Deserialize<Dictionary<string, object>>(json, options);

        Assert.NotNull(result);
        Assert.Equal(1, Convert.ToInt32(result["A"]));
        Assert.Equal("test", result["B"]);
        Assert.Equal(true, result["C"]);
        Assert.IsType<Dictionary<string, object>>(result["D"]);
        var nested = (Dictionary<string, object>)result["D"];
        Assert.Equal(3.14, Convert.ToDouble(nested["E"]));
    }
}
