using Tinkwell.Measures.History.TimescaleDb;

namespace Tinkwell.Measures.History.TimescaleDb.Tests;

public sealed class SchemaManagerTests
{
    [SkippableFact]
    public void SchemaManager_is_internal_and_visible_to_tests_via_InternalsVisibleTo()
    {
        var t = typeof(SchemaManager);

        Assert.Equal("SchemaManager", t.Name);
        Assert.True(t.IsNotPublic);
        Assert.Equal("Tinkwell.Measures.History.TimescaleDb", t.Namespace);
    }
}
