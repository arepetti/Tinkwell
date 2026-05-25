using Tinkwell.Encoding;

namespace Tinkwell.Lwm2m.Tests;

public class Lwm2mTypesTests
{
    [Fact]
    public void Lwm2mObjectDefinition_Default_ResourcesIsNull()
    {
        var def = new Lwm2mObjectDefinition(3, "Device");
        Assert.Null(def.Resources);
    }

    [Fact]
    public void Lwm2mResourceDefinition_Defaults_MandatoryAndMultipleFalse()
    {
        var def = new Lwm2mResourceDefinition(0, "Sensor Value", PayloadType.Float, Lwm2mOperations.Read);
        Assert.False(def.Mandatory);
        Assert.False(def.Multiple);
    }

    [Fact]
    public void Lwm2mOperations_ReadWrite_CombinesReadAndWrite()
    {
        Assert.Equal(Lwm2mOperations.Read | Lwm2mOperations.Write, Lwm2mOperations.ReadWrite);
        var combined = Lwm2mOperations.Read | Lwm2mOperations.Write;
        Assert.True(combined.HasFlag(Lwm2mOperations.Read));
        Assert.True(combined.HasFlag(Lwm2mOperations.Write));
        Assert.False(combined.HasFlag(Lwm2mOperations.Execute));
    }

    [Fact]
    public void Lwm2mObjectDefinition_Equal_SameValues()
    {
        var a = new Lwm2mObjectDefinition(10, "Light", true, true, null);
        var b = new Lwm2mObjectDefinition(10, "Light", true, true, null);
        Assert.Equal(a, b);

        var resources = new Lwm2mResourceDefinition[] { new(5700, "State", PayloadType.Boolean, Lwm2mOperations.ReadWrite) };
        var c = new Lwm2mObjectDefinition(10, "Light", true, true, resources);
        var d = new Lwm2mObjectDefinition(10, "Light", true, true, resources);
        Assert.Equal(c, d);
    }

    [Fact]
    public void Lwm2mObjectDefinition_NotEqual_DifferentIdOrName()
    {
        var baseDef = new Lwm2mObjectDefinition(10, "Light", false, false, null);
        Assert.NotEqual(baseDef, baseDef with { ObjectId = 11 });
        Assert.NotEqual(baseDef, baseDef with { Name = "Dimmer" });
    }

    [Fact]
    public void Lwm2mResourceDefinition_Equal_SameValues()
    {
        var a = new Lwm2mResourceDefinition(1, "Name", PayloadType.String, Lwm2mOperations.Read, false, true);
        var b = new Lwm2mResourceDefinition(1, "Name", PayloadType.String, Lwm2mOperations.Read, false, true);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Lwm2mResourceDefinition_NotEqual_DifferentField()
    {
        var a = new Lwm2mResourceDefinition(1, "Name", PayloadType.String, Lwm2mOperations.Read, false, false);
        Assert.NotEqual(a, a with { ResourceId = 2 });
        Assert.NotEqual(a, a with { Operations = Lwm2mOperations.Write });
    }
}
