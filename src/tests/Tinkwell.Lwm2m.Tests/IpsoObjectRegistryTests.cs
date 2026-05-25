using Tinkwell.Lwm2m;

namespace Tinkwell.Lwm2m.Tests;

public class IpsoObjectRegistryTests
{
    [Theory]
    [InlineData(3303, "Temperature")]
    [InlineData(3304, "Humidity")]
    [InlineData(3316, "Voltage")]
    [InlineData(3306, "Actuation")]
    public void Find_KnownObject_ReturnsDefinition(int objectId, string expectedName)
    {
        var def = IpsoObjectRegistry.Find(objectId);
        Assert.NotNull(def);
        Assert.Equal(expectedName, def.Name);
        Assert.Equal(objectId, def.ObjectId);
    }

    [Fact]
    public void Find_UnknownObject_ReturnsNull()
    {
        Assert.Null(IpsoObjectRegistry.Find(99999));
    }

    [Fact]
    public void IsKnown_Temperature_ReturnsTrue()
    {
        Assert.True(IpsoObjectRegistry.IsKnown(3303));
    }

    [Fact]
    public void IsKnown_Unknown_ReturnsFalse()
    {
        Assert.False(IpsoObjectRegistry.IsKnown(99999));
    }

    [Fact]
    public void All_ContainsMultipleObjects()
    {
        Assert.True(IpsoObjectRegistry.All.Count > 10);
    }

    [Fact]
    public void StandardSensorObjects_HaveSensorValueResource()
    {
        var sensorObjects = new[] { 3303, 3304, 3315, 3316, 3317, 3318 };
        foreach (var id in sensorObjects)
        {
            var def = IpsoObjectRegistry.Find(id);
            Assert.NotNull(def?.Resources);
            Assert.Contains(def.Resources,
                r => r.ResourceId == IpsoObjectRegistry.CommonResources.SensorValue);
        }
    }

    [Fact]
    public void ActuationObject_HasOnOffResource()
    {
        var def = IpsoObjectRegistry.Find(3306);
        Assert.NotNull(def?.Resources);
        Assert.Contains(def.Resources, r => r.ResourceId == 5850 && r.Mandatory);
    }

    [Fact]
    public void LightControl_HasDimmerResource()
    {
        var def = IpsoObjectRegistry.Find(3311);
        Assert.NotNull(def?.Resources);
        Assert.Contains(def.Resources, r => r.ResourceId == 5851);
    }
}
