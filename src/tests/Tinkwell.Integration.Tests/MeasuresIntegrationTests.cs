using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Tinkwell.Measures;
using Tinkwell.Runlet.Measures.Registry;

namespace Tinkwell.Integration.Tests;

[Collection("Store")]
[Trait("Category", "Integration")]
public class MeasuresIntegrationTests
{
    private readonly StoreFixture _fixture;

    public MeasuresIntegrationTests(StoreFixture fixture)
    {
        _fixture = fixture;
    }

    private MeasureRegistry CreateRegistry(string? bucketId = null)
        => new(_fixture.Client, bucketId ?? $"measures-{Guid.NewGuid():N}",
            NullLogger<MeasureRegistry>.Instance);

    // -----------------------------------------------------------------------
    // Register + Find round-trip
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Register_And_Find_RoundTrips()
    {
        var registry = CreateRegistry();

        var def = new MeasureDefinition
        {
            Name = "temperature",
            Type = MeasureType.Number,
            QuantityType = "Temperature",
            Unit = "DegreeCelsius",
            Minimum = -40,
            Maximum = 85,
            Precision = 2,
        };

        var meta = new MeasureMetadata
        {
            Description = "Room temperature",
            Category = "environment",
            Tags = ["indoor"],
        };

        await registry.RegisterAsync(def, meta);

        var found = await registry.FindAsync("temperature");

        Assert.NotNull(found);
        Assert.Equal("temperature", found!.Definition.Name);
        Assert.Equal(MeasureType.Number, found.Definition.Type);
        Assert.Equal("Temperature", found.Definition.QuantityType);
        Assert.Equal("DegreeCelsius", found.Definition.Unit);
        Assert.Equal(-40, found.Definition.Minimum);
        Assert.Equal(85, found.Definition.Maximum);
        Assert.Equal(2, found.Definition.Precision);
        Assert.Equal("Room temperature", found.Metadata.Description);
        Assert.Equal("environment", found.Metadata.Category);
        Assert.Contains("indoor", found.Metadata.Tags);
        Assert.Equal(MeasureValueType.Undefined, found.Value.Type);
    }

    // -----------------------------------------------------------------------
    // Register + Update + Find
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Update_And_Find_ReturnsValue()
    {
        var registry = CreateRegistry();

        var def = new MeasureDefinition
        {
            Name = "pressure",
            Type = MeasureType.Number,
            QuantityType = "Pressure",
            Unit = "Pascal",
        };

        await registry.RegisterAsync(def);
        await registry.UpdateAsync("pressure",
            MeasureValue.FromValue(def, 101325.0, DateTime.UtcNow));

        var found = await registry.FindAsync("pressure");

        Assert.NotNull(found);
        Assert.Equal(MeasureValueType.Number, found!.Value.Type);
        Assert.Equal(101325.0, found.Value.AsDouble(), 1.0);
    }

    // -----------------------------------------------------------------------
    // String measure
    // -----------------------------------------------------------------------

    [Fact]
    public async Task String_Measure_RoundTrips()
    {
        var registry = CreateRegistry();

        var def = new MeasureDefinition
        {
            Name = "label",
            Type = MeasureType.String,
        };

        await registry.RegisterAsync(def);
        await registry.UpdateAsync("label", new MeasureValue("hello world", DateTime.UtcNow));

        var found = await registry.FindAsync("label");

        Assert.NotNull(found);
        Assert.Equal(MeasureValueType.String, found!.Value.Type);
        Assert.Equal("hello world", found.Value.AsString());
    }

    // -----------------------------------------------------------------------
    // Precision rounding
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Update_RoundsToPrecision()
    {
        var registry = CreateRegistry();

        var def = new MeasureDefinition
        {
            Name = "temp",
            Type = MeasureType.Number,
            QuantityType = "Temperature",
            Unit = "DegreeCelsius",
            Precision = 1,
        };

        await registry.RegisterAsync(def);
        await registry.UpdateAsync("temp",
            MeasureValue.FromValue(def, 23.456, DateTime.UtcNow));

        var found = await registry.FindAsync("temp");

        Assert.NotNull(found);
        Assert.Equal(23.5, found!.Value.AsDouble(), 0.01);
    }

    // -----------------------------------------------------------------------
    // Range validation
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Update_BelowMinimum_Throws()
    {
        var registry = CreateRegistry();

        var def = new MeasureDefinition
        {
            Name = "bounded",
            Type = MeasureType.Number,
            QuantityType = "Scalar",
            Unit = "Amount",
            Minimum = 0,
            Maximum = 100,
        };

        await registry.RegisterAsync(def);

        await Assert.ThrowsAsync<MeasureException>(() =>
            registry.UpdateAsync("bounded",
                MeasureValue.FromValue(def, -1.0, DateTime.UtcNow)));
    }

    [Fact]
    public async Task Update_AboveMaximum_Throws()
    {
        var registry = CreateRegistry();

        var def = new MeasureDefinition
        {
            Name = "bounded",
            Type = MeasureType.Number,
            QuantityType = "Scalar",
            Unit = "Amount",
            Minimum = 0,
            Maximum = 100,
        };

        await registry.RegisterAsync(def);

        await Assert.ThrowsAsync<MeasureException>(() =>
            registry.UpdateAsync("bounded",
                MeasureValue.FromValue(def, 200.0, DateTime.UtcNow)));
    }

    // -----------------------------------------------------------------------
    // Constant protection
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Update_ConstantMeasure_Throws()
    {
        var registry = CreateRegistry();

        var def = new MeasureDefinition
        {
            Name = "version",
            Type = MeasureType.Number,
            Attributes = MeasureAttributes.Constant,
            QuantityType = "Scalar",
            Unit = "Amount",
        };

        await registry.RegisterAsync(def);

        await Assert.ThrowsAsync<MeasureException>(() =>
            registry.UpdateAsync("version",
                MeasureValue.FromValue(def, 1.0, DateTime.UtcNow)));
    }

    // -----------------------------------------------------------------------
    // Constant with initial value
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Register_ConstantWithInitialValue_IsReadable()
    {
        var registry = CreateRegistry();

        var def = new MeasureDefinition
        {
            Name = "firmware-version",
            Type = MeasureType.Number,
            Attributes = MeasureAttributes.Constant,
            QuantityType = "Scalar",
            Unit = "Amount",
        };

        var initialValue = MeasureValue.FromValue(def, 42.0, DateTime.UtcNow);
        await registry.RegisterAsync(def, initialValue: initialValue);

        var found = await registry.FindAsync("firmware-version");

        Assert.NotNull(found);
        Assert.Equal(MeasureValueType.Number, found!.Value.Type);
        Assert.Equal(42.0, found.Value.AsDouble(), 0.01);
    }

    [Fact]
    public async Task Register_ConstantWithInitialValue_RejectsSubsequentUpdate()
    {
        var registry = CreateRegistry();

        var def = new MeasureDefinition
        {
            Name = "build-number",
            Type = MeasureType.Number,
            Attributes = MeasureAttributes.Constant,
            QuantityType = "Scalar",
            Unit = "Amount",
        };

        var initialValue = MeasureValue.FromValue(def, 100.0, DateTime.UtcNow);
        await registry.RegisterAsync(def, initialValue: initialValue);

        await Assert.ThrowsAsync<MeasureException>(() =>
            registry.UpdateAsync("build-number",
                MeasureValue.FromValue(def, 200.0, DateTime.UtcNow)));
    }

    // -----------------------------------------------------------------------
    // Type mismatch
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Update_TypeMismatch_Throws()
    {
        var registry = CreateRegistry();

        var def = new MeasureDefinition
        {
            Name = "typed",
            Type = MeasureType.Number,
            QuantityType = "Scalar",
            Unit = "Amount",
        };

        await registry.RegisterAsync(def);

        await Assert.ThrowsAsync<MeasureException>(() =>
            registry.UpdateAsync("typed",
                new MeasureValue("not a number", DateTime.UtcNow)));
    }

    // -----------------------------------------------------------------------
    // FindAll
    // -----------------------------------------------------------------------

    [Fact]
    public async Task FindAll_ReturnsRegisteredMeasures()
    {
        var registry = CreateRegistry();

        await registry.RegisterAsync(new MeasureDefinition
        {
            Name = "a",
            Type = MeasureType.Number,
            QuantityType = "Scalar",
            Unit = "Amount",
        });

        await registry.RegisterAsync(new MeasureDefinition
        {
            Name = "b",
            Type = MeasureType.String,
        });

        var all = await registry.FindAllAsync();

        Assert.True(all.Count >= 2);
        Assert.Contains(all, m => m.Definition.Name == "a");
        Assert.Contains(all, m => m.Definition.Name == "b");
    }

    // -----------------------------------------------------------------------
    // FindDefinition
    // -----------------------------------------------------------------------

    [Fact]
    public async Task FindDefinition_ReturnsNull_WhenNotRegistered()
    {
        var registry = CreateRegistry();

        var def = await registry.FindDefinitionAsync("nonexistent");
        Assert.Null(def);
    }

    // -----------------------------------------------------------------------
    // Reserved name
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Register_ReservedName_Throws()
    {
        var registry = CreateRegistry();

        await Assert.ThrowsAsync<MeasureException>(() =>
            registry.RegisterAsync(new MeasureDefinition
            {
                Name = "_internal",
                Type = MeasureType.Number,
            }));
    }

    // -----------------------------------------------------------------------
    // Not registered
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Update_NotRegistered_Throws()
    {
        var registry = CreateRegistry();

        await Assert.ThrowsAsync<MeasureException>(() =>
            registry.UpdateAsync("ghost",
                new MeasureValue("value", DateTime.UtcNow)));
    }

    // -----------------------------------------------------------------------
    // Watch notifications
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Watch_RaisesValueChanged()
    {
        var registry = CreateRegistry();

        var def = new MeasureDefinition
        {
            Name = "watched",
            Type = MeasureType.Number,
            QuantityType = "Scalar",
            Unit = "Amount",
        };

        await registry.RegisterAsync(def);

        var events = new List<ValueChangedEventArgs>();
        registry.ValueChanged += (_, e) => events.Add(e);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var watchTask = registry.WatchAsync(cts.Token);

        await Task.Delay(500);

        await registry.UpdateAsync("watched",
            MeasureValue.FromValue(def, 42.0, DateTime.UtcNow));

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (events.Count == 0 && DateTime.UtcNow < deadline)
            await Task.Delay(200);

        cts.Cancel();

        try { await watchTask; }
        catch (OperationCanceledException)
        {
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
        {
        }

        Assert.NotEmpty(events);
        Assert.Equal("watched", events[0].Name);
        Assert.Equal(MeasureValueType.Number, events[0].NewValue.Type);
    }

    // -----------------------------------------------------------------------
    // TTL expiration
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Measure_Expires_AfterTtl()
    {
        var registry = CreateRegistry();

        var def = new MeasureDefinition
        {
            Name = "ephemeral",
            Type = MeasureType.Number,
            QuantityType = "Scalar",
            Unit = "Amount",
            Ttl = TimeSpan.FromSeconds(2),
        };

        await registry.RegisterAsync(def);
        await registry.UpdateAsync("ephemeral",
            MeasureValue.FromValue(def, 1.0, DateTime.UtcNow));

        var before = await registry.FindAsync("ephemeral");
        Assert.NotNull(before);
        Assert.Equal(MeasureValueType.Number, before!.Value.Type);

        await Task.Delay(TimeSpan.FromSeconds(3));

        var after = await registry.FindAsync("ephemeral");

        // After TTL, the store should have expired the entry
        Assert.True(
            after is null ||
            after.Value.Type == MeasureValueType.Undefined ||
            after.IsExpired);
    }

    // -----------------------------------------------------------------------
    // UpdateMany
    // -----------------------------------------------------------------------

    [Fact]
    public async Task UpdateMany_UpdatesMultipleValues()
    {
        var registry = CreateRegistry();

        var defs = new[]
        {
            new MeasureDefinition { Name = "m1", Type = MeasureType.Number, QuantityType = "Scalar", Unit = "Amount" },
            new MeasureDefinition { Name = "m2", Type = MeasureType.Number, QuantityType = "Scalar", Unit = "Amount" },
        };

        foreach (var d in defs)
            await registry.RegisterAsync(d);

        await registry.UpdateManyAsync([
            ("m1", MeasureValue.FromValue(defs[0], 10.0, DateTime.UtcNow)),
            ("m2", MeasureValue.FromValue(defs[1], 20.0, DateTime.UtcNow)),
        ]);

        var m1 = await registry.FindAsync("m1");
        var m2 = await registry.FindAsync("m2");

        Assert.NotNull(m1);
        Assert.NotNull(m2);
        Assert.Equal(10.0, m1!.Value.AsDouble(), 0.01);
        Assert.Equal(20.0, m2!.Value.AsDouble(), 0.01);
    }
}
