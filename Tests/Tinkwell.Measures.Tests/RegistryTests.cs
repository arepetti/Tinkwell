using Tinkwell.Measures.Storage;
using UnitsNet;

namespace Tinkwell.Measures.Tests;

public class RegistryTests
{
    [Fact]
    public async Task RegisterMeasure_SuccessfullyRegistersMeasure()
    {
        // Arrange
        var storage = new InMemoryStorage();
        var registry = new Registry(storage);
        var definition = new MeasureDefinition { Name = "TestMeasure", Type = MeasureType.Number };
        var metadata = new MeasureMetadata(DateTime.UtcNow);

        // Act
        await registry.RegisterAsync(definition, metadata, CancellationToken.None);

        // Assert
        var registeredMeasure = registry.Find("TestMeasure");
        Assert.NotNull(registeredMeasure);
        Assert.Equal("TestMeasure", registeredMeasure.Name);
    }

    [Fact]
    public async Task RegisterMeasure_ThrowsExceptionForDuplicateMeasure()
    {
        // Arrange
        var storage = new InMemoryStorage();
        var registry = new Registry(storage);
        var definition = new MeasureDefinition { Name = "DuplicateMeasure", Type = MeasureType.Number };
        var metadata = new MeasureMetadata(DateTime.UtcNow);

        await registry.RegisterAsync(definition, metadata, CancellationToken.None);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () => await registry.RegisterAsync(definition, metadata, CancellationToken.None));
        Assert.Contains("is already registered", exception.Message);
    }

    [Theory]
    [InlineData(null, typeof(ArgumentNullException), "Value cannot be null. (Parameter 'Name')")]
    [InlineData("", typeof(ArgumentException), "The value cannot be an empty string or composed entirely of whitespace. (Parameter 'Name')")]
    [InlineData(" ", typeof(ArgumentException), "The value cannot be an empty string or composed entirely of whitespace. (Parameter 'Name')")]
    public async Task RegisterMeasure_ThrowsExceptionForInvalidMeasureName(string? invalidName, Type expectedExceptionType, string expectedMessage)
    {
        // Arrange
        var storage = new InMemoryStorage();
        var registry = new Registry(storage);
        
        // Act & Assert
        var exception = await Assert.ThrowsAsync(expectedExceptionType,
            async () => new MeasureDefinition { Name = invalidName!, Type = MeasureType.Number });
        Assert.Equal(expectedMessage, exception.Message);
    }

    [Fact]
    public async Task RegisterMeasure_ThrowsExceptionForStringMeasureWithUnit()
    {
        // Arrange
        var storage = new InMemoryStorage();
        var registry = new Registry(storage);
        var definition = new MeasureDefinition { Name = "StringMeasureWithUnit", Type = MeasureType.String, QuantityType = "Length", Unit = "Meter" };
        var metadata = new MeasureMetadata(DateTime.UtcNow);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () => await registry.RegisterAsync(definition, metadata, CancellationToken.None));
        Assert.Contains("cannot have a unit of measure because it's a string", exception.Message);
    }

    [Fact]
    public async Task RegisterMeasure_ThrowsExceptionForInvalidUnitCombination()
    {
        // Arrange
        var storage = new InMemoryStorage();
        var registry = new Registry(storage);
        var definition = new MeasureDefinition { Name = "InvalidUnitMeasure", Type = MeasureType.Number, QuantityType = "InvalidQuantity", Unit = "InvalidUnit" };
        var metadata = new MeasureMetadata(DateTime.UtcNow);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () => await registry.RegisterAsync(definition, metadata, CancellationToken.None));
        Assert.Contains("are not a valid combination", exception.Message);
    }

    [Fact]
    public async Task RegisterUpdateFind_FlowsCorrectly()
    {
        // Arrange
        var storage = new InMemoryStorage();
        var registry = new Registry(storage);
        var definition = new MeasureDefinition { Name = "Temperature", Type = MeasureType.Number };
        var metadata = new MeasureMetadata(DateTime.UtcNow);

        // Act - Register
        await registry.RegisterAsync(definition, metadata, CancellationToken.None);

        // Act - Update
        var initialValue = new MeasureValue(Length.FromMeters(25.5));
        await registry.UpdateAsync("Temperature", initialValue, CancellationToken.None);

        // Act - Find
        var measure = registry.Find("Temperature");

        // Assert
        Assert.NotNull(measure);
        Assert.Equal(initialValue, measure.Value);

        // Act - Update again
        var updatedValue = new MeasureValue(Length.FromMeters(26.0));
        await registry.UpdateAsync("Temperature", updatedValue, CancellationToken.None);

        // Act - Find again
        measure = registry.Find("Temperature");

        // Assert
        Assert.NotNull(measure);
        Assert.Equal(updatedValue, measure.Value);
    }

    [Fact]
    public async Task Update_ThrowsExceptionForConstantMeasure()
    {
        // Arrange
        var storage = new InMemoryStorage();
        var registry = new Registry(storage);
        var definition = new MeasureDefinition { Name = "ConstantMeasure", Type = MeasureType.Number, Attributes = MeasureAttributes.Constant };
        var metadata = new MeasureMetadata(DateTime.UtcNow);
        await registry.RegisterAsync(definition, metadata, CancellationToken.None);
        await registry.UpdateAsync("ConstantMeasure", new MeasureValue(Length.FromMeters(10.0)), CancellationToken.None);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () => await registry.UpdateAsync("ConstantMeasure", new MeasureValue(Length.FromMeters(20.0)), CancellationToken.None));
        Assert.Contains("Cannot update a constant measure", exception.Message);
    }

    [Fact]
    public async Task Update_ThrowsExceptionForIncompatibleValueType()
    {
        // Arrange
        var storage = new InMemoryStorage();
        var registry = new Registry(storage);
        var definition = new MeasureDefinition { Name = "NumericMeasure", Type = MeasureType.Number };
        var metadata = new MeasureMetadata(DateTime.UtcNow);
        await registry.RegisterAsync(definition, metadata, CancellationToken.None);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () => await registry.UpdateAsync("NumericMeasure", new MeasureValue("hello"), CancellationToken.None));
        Assert.Contains("is not compatible with the registered measure", exception.Message);
    }

    [Fact]
    public async Task Update_ThrowsExceptionForOutOfRangeValue()
    {
        // Arrange
        var storage = new InMemoryStorage();
        var registry = new Registry(storage);
        var definition = new MeasureDefinition { Name = "RangedMeasure", Type = MeasureType.Number, Minimum = 0.0, Maximum = 100.0 };
        var metadata = new MeasureMetadata(DateTime.UtcNow);
        await registry.RegisterAsync(definition, metadata, CancellationToken.None);

        // Act & Assert - Below minimum
        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await registry.UpdateAsync("RangedMeasure", new MeasureValue(Length.FromMeters(-10.0)), CancellationToken.None));
        Assert.Contains("is less than the registered minimum", exception.Message);

        // Act & Assert - Above maximum
        exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await registry.UpdateAsync("RangedMeasure", new MeasureValue(Length.FromMeters(110.0)), CancellationToken.None));
        Assert.Contains("is greater than the registered maximum", exception.Message);
    }

    [Fact]
    public async Task FindMethods_FlowsCorrectly()
    {
        // Arrange
        var storage = new InMemoryStorage();
        var registry = new Registry(storage);
        var definition = new MeasureDefinition { Name = "MeasureToFind", Type = MeasureType.Number };
        var metadata = new MeasureMetadata(DateTime.UtcNow);
        await registry.RegisterAsync(definition, metadata, CancellationToken.None);

        // Act - FindAsync
        var foundMeasureAsync = await registry.FindAsync("MeasureToFind", CancellationToken.None);
        Assert.NotNull(foundMeasureAsync);
        Assert.Equal("MeasureToFind", foundMeasureAsync.Name);

        // Act - FindAllAsync()
        var allMeasures = await registry.FindAllAsync(CancellationToken.None);
        Assert.Contains(allMeasures, m => m.Name == "MeasureToFind");

        // Act - FindDefinition
        var foundDefinition = registry.FindDefinition("MeasureToFind");
        Assert.NotNull(foundDefinition);
        Assert.Equal("MeasureToFind", foundDefinition.Name);

        // Act - FindAllDefinitionsAsync()
        var allDefinitions = await registry.FindAllDefinitionsAsync(CancellationToken.None);
        Assert.Contains(allDefinitions, d => d.Name == "MeasureToFind");
    }

    [Fact]
    public async Task RegisterMany_FindAllByName_FlowsCorrectly()
    {
        // Arrange
        var storage = new InMemoryStorage();
        var registry = new Registry(storage);

        var measuresToRegister = new List<(MeasureDefinition Definition, MeasureMetadata Metadata)>
        {
            (new MeasureDefinition { Name = "Measure1", Type = MeasureType.Number }, new MeasureMetadata(DateTime.UtcNow)),
            (new MeasureDefinition { Name = "Measure2", Type = MeasureType.String, QuantityType = "", Unit = "" }, new MeasureMetadata(DateTime.UtcNow)),
            (new MeasureDefinition { Name = "Measure3", Type = MeasureType.Number }, new MeasureMetadata(DateTime.UtcNow))
        };

        // Act - RegisterManyAsync
        await registry.RegisterManyAsync(measuresToRegister, CancellationToken.None);

        // Act - FindAllAsync(names)
        var foundMeasures = await registry.FindAllAsync(new[] { "Measure1", "Measure3" }, CancellationToken.None);

        // Assert
        Assert.Equal(2, foundMeasures.Count());
        Assert.Contains(foundMeasures, m => m.Name == "Measure1");
        Assert.Contains(foundMeasures, m => m.Name == "Measure3");
        Assert.DoesNotContain(foundMeasures, m => m.Name == "Measure2");

        // Act - FindAllDefinitionsAsync(names)
        var foundDefinitions = await registry.FindAllDefinitionsAsync(new[] { "Measure1", "Measure2" }, CancellationToken.None);

        // Assert
        Assert.Equal(2, foundDefinitions.Count());
        Assert.Contains(foundDefinitions, d => d.Name == "Measure1");
        Assert.Contains(foundDefinitions, d => d.Name == "Measure2");
        Assert.DoesNotContain(foundDefinitions, d => d.Name == "Measure3");
    }

    [Fact]
    public async Task UpdateMany_FlowsCorrectly()
    {
        // Arrange
        var storage = new InMemoryStorage();
        var registry = new Registry(storage);

        var measuresToRegister = new List<(MeasureDefinition Definition, MeasureMetadata Metadata)>
        {
            (new MeasureDefinition { Name = "MeasureA", Type = MeasureType.Number }, new MeasureMetadata(DateTime.UtcNow)),
            (new MeasureDefinition { Name = "MeasureB", Type = MeasureType.String, QuantityType = "", Unit = "" }, new MeasureMetadata(DateTime.UtcNow))
        };
        await registry.RegisterManyAsync(measuresToRegister, CancellationToken.None);

        // Act - UpdateManyAsync
        var updates = new List<(string Name, MeasureValue Value)>
        {
            ("MeasureA", new MeasureValue(Length.FromMeters(123.45))),
            ("MeasureB", new MeasureValue("new string value"))
        };
        await registry.UpdateManyAsync(updates, CancellationToken.None);

        // Assert
        var measureA = registry.Find("MeasureA");
        var measureB = registry.Find("MeasureB");

        Assert.NotNull(measureA);
        Assert.NotNull(measureB);
        Assert.Equal(new MeasureValue(Length.FromMeters(123.45)), measureA.Value);
        Assert.Equal(new MeasureValue("new string value"), measureB.Value);
    }
}
