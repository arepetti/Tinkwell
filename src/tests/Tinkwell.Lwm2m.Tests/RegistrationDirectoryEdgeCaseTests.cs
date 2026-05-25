using System.Net;
using Tinkwell.Lwm2m;
using Tinkwell.Lwm2m.Registration;

namespace Tinkwell.Lwm2m.Tests;

public class RegistrationDirectoryEdgeCaseTests
{
    private static readonly IPEndPoint TestEndpoint = new(IPAddress.Loopback, 5683);

    private static Lwm2mRegistration MakeRegistration(
        string endpoint = "device1",
        int lifetime = 86400,
        DateTimeOffset? registeredAt = null)
    {
        return new Lwm2mRegistration
        {
            Endpoint = endpoint,
            Address = TestEndpoint,
            RegisteredAt = registeredAt ?? DateTimeOffset.UtcNow,
            Lifetime = lifetime,
            Objects = [new Lwm2mPath(3303, 0)],
            Location = "",
        };
    }

    [Fact]
    public void Register_MultipleDevices_EachGetsUniqueLocation()
    {
        var dir = new RegistrationDirectory();
        var locations = new HashSet<string>();

        for (int i=0; i < 50; ++i)
        {
            var reg = dir.Register(MakeRegistration($"device{i}"));
            Assert.True(locations.Add(reg.Location),
                $"Duplicate location: {reg.Location}");
        }

        Assert.Equal(50, dir.All.Count);
    }

    [Fact]
    public void Update_WithoutLifetime_OnlyRefreshesTime()
    {
        var dir = new RegistrationDirectory();
        var reg = dir.Register(MakeRegistration("device1", lifetime: 300));

        Assert.True(dir.Update(reg.Location));

        var updated = dir.FindByLocation(reg.Location)!;
        Assert.Equal(300, updated.Lifetime);
    }

    [Fact]
    public void PurgeExpired_AllExpired_RemovesAll()
    {
        var dir = new RegistrationDirectory();
        for (int i=0; i < 10; ++i)
            dir.Register(MakeRegistration($"dev{i}", lifetime: 1,
                registeredAt: DateTimeOffset.UtcNow.AddSeconds(-10)));

        var purged = dir.PurgeExpired();
        Assert.Equal(10, purged);
        Assert.Empty(dir.All);
    }

    [Fact]
    public void PurgeExpired_NoneExpired_RemovesNone()
    {
        var dir = new RegistrationDirectory();
        for (int i=0; i < 5; ++i)
            dir.Register(MakeRegistration($"dev{i}", lifetime: 86400));

        var purged = dir.PurgeExpired();
        Assert.Equal(0, purged);
        Assert.Equal(5, dir.All.Count);
    }

    [Fact]
    public void PurgeExpired_EmptyDirectory_Returns0()
    {
        var dir = new RegistrationDirectory();
        Assert.Equal(0, dir.PurgeExpired());
    }

    [Fact]
    public void FindByLocation_CaseSensitive()
    {
        var dir = new RegistrationDirectory();
        var reg = dir.Register(MakeRegistration("device1"));

        Assert.NotNull(dir.FindByLocation(reg.Location));
        Assert.Null(dir.FindByLocation(reg.Location.ToUpper()));
    }

    [Fact]
    public void Registration_Objects_PreservedAfterRegister()
    {
        var dir = new RegistrationDirectory();
        var objects = new List<Lwm2mPath>
        {
            new(3, 0), new(3303, 0), new(3304, 0)
        };

        var source = new Lwm2mRegistration
        {
            Endpoint = "device1",
            Address = TestEndpoint,
            RegisteredAt = DateTimeOffset.UtcNow,
            Lifetime = 86400,
            Objects = objects,
            Location = "",
        };

        var reg = dir.Register(source);
        var found = dir.FindByLocation(reg.Location)!;

        Assert.Equal(3, found.Objects.Count);
    }

    [Fact]
    public void Deregister_SameLocationTwice_SecondReturnsFalse()
    {
        var dir = new RegistrationDirectory();
        var reg = dir.Register(MakeRegistration("device1"));

        Assert.True(dir.Deregister(reg.Location));
        Assert.False(dir.Deregister(reg.Location));
    }
}
