using Tinkwell.Measures.History.TimescaleDb;

namespace Tinkwell.Measures.History.TimescaleDb.Tests;

public sealed class TimescaleDbOptionsTests
{
    [Fact]
    public void ConnectionString_is_required_for_string_based_store_constructor()
    {
        Assert.Throws<ArgumentNullException>(() => new TimescaleDbMeasureHistoryStore((string)null!));
    }

    [Fact]
    public void AutoCreateSchema_defaults_to_true()
    {
        var o = new TimescaleDbOptions { ConnectionString = "Host=localhost" };

        Assert.True(o.AutoCreateSchema);
    }

    [Fact]
    public void Options_with_connection_string_can_be_constructed()
    {
        var o = new TimescaleDbOptions { ConnectionString = "Host=127.0.0.1;Port=5432;Database=db;Username=u;Password=p" };

        Assert.Equal("Host=127.0.0.1;Port=5432;Database=db;Username=u;Password=p", o.ConnectionString);
        Assert.True(o.AutoCreateSchema);
    }

    [Fact]
    public void AutoCreateSchema_can_be_set_false_without_touching_connection_string()
    {
        var o = new TimescaleDbOptions
        {
            ConnectionString = "Host=localhost",
            AutoCreateSchema = false,
        };

        Assert.False(o.AutoCreateSchema);
    }
}
