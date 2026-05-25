using System.Net;
using Tinkwell.Lwm2m;
using Tinkwell.Lwm2m.Registration;

namespace Tinkwell.Lwm2m.Tests;

public class RegistrationDirectoryTests
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
    public void Register_AssignsLocation()
    {
        var dir = new RegistrationDirectory();
        var result = dir.Register(MakeRegistration());

        Assert.StartsWith("/rd/", result.Location);
        Assert.Single(dir.All);
    }

    [Fact]
    public void Register_SameEndpoint_ReplacesOld()
    {
        var dir = new RegistrationDirectory();
        var first = dir.Register(MakeRegistration("device1"));
        var second = dir.Register(MakeRegistration("device1"));

        Assert.NotEqual(first.Location, second.Location);
        Assert.Single(dir.All);
        Assert.Null(dir.FindByLocation(first.Location));
    }

    [Fact]
    public void Register_DifferentEndpoints_CoExist()
    {
        var dir = new RegistrationDirectory();
        dir.Register(MakeRegistration("device1"));
        dir.Register(MakeRegistration("device2"));

        Assert.Equal(2, dir.All.Count);
    }

    [Fact]
    public void FindByEndpoint_ReturnsCorrectRegistration()
    {
        var dir = new RegistrationDirectory();
        var reg = dir.Register(MakeRegistration("device1"));

        var found = dir.FindByEndpoint("device1");
        Assert.NotNull(found);
        Assert.Equal(reg.Location, found.Location);
    }

    [Fact]
    public void FindByEndpoint_UnknownEndpoint_ReturnsNull()
    {
        var dir = new RegistrationDirectory();
        Assert.Null(dir.FindByEndpoint("unknown"));
    }

    [Fact]
    public void Update_ExistingLocation_RefreshesTimestamp()
    {
        var dir = new RegistrationDirectory();
        var reg = dir.Register(MakeRegistration("device1", lifetime: 100));

        Assert.True(dir.Update(reg.Location, newLifetime: 200));

        var updated = dir.FindByLocation(reg.Location)!;
        Assert.Equal(200, updated.Lifetime);
    }

    [Fact]
    public void Update_UnknownLocation_ReturnsFalse()
    {
        var dir = new RegistrationDirectory();
        Assert.False(dir.Update("/rd/nonexistent"));
    }

    [Fact]
    public void Deregister_RemovesFromBothIndices()
    {
        var dir = new RegistrationDirectory();
        var reg = dir.Register(MakeRegistration("device1"));

        Assert.True(dir.Deregister(reg.Location));
        Assert.Null(dir.FindByLocation(reg.Location));
        Assert.Null(dir.FindByEndpoint("device1"));
        Assert.Empty(dir.All);
    }

    [Fact]
    public void Deregister_UnknownLocation_ReturnsFalse()
    {
        var dir = new RegistrationDirectory();
        Assert.False(dir.Deregister("/rd/nonexistent"));
    }

    [Fact]
    public void PurgeExpired_RemovesOnlyExpired()
    {
        var dir = new RegistrationDirectory();
        dir.Register(MakeRegistration("expired", lifetime: 1,
            registeredAt: DateTimeOffset.UtcNow.AddSeconds(-10)));
        dir.Register(MakeRegistration("active", lifetime: 86400));

        var purged = dir.PurgeExpired();
        Assert.Equal(1, purged);
        Assert.Single(dir.All);
        Assert.NotNull(dir.FindByEndpoint("active"));
        Assert.Null(dir.FindByEndpoint("expired"));
    }

    [Fact]
    public void PurgeExpired_AfterUpdateWithLongLifetimeFromExpired_KeepsRegistration()
    {
        var dir = new RegistrationDirectory();
        var reg = dir.Register(MakeRegistration("device1", lifetime: 1,
            registeredAt: DateTimeOffset.UtcNow.AddSeconds(-10)));
        Assert.True(reg.IsExpired);

        Assert.True(dir.Update(reg.Location, newLifetime: 86_400));

        var afterUpdate = dir.FindByLocation(reg.Location)!;
        Assert.False(afterUpdate.IsExpired);

        var purged = dir.PurgeExpired();
        Assert.Equal(0, purged);
        Assert.NotNull(dir.FindByEndpoint("device1"));
        Assert.Single(dir.All);
    }

    [Fact]
    public void IsExpired_ReturnsCorrectly()
    {
        var expired = MakeRegistration(lifetime: 1,
            registeredAt: DateTimeOffset.UtcNow.AddSeconds(-10));
        Assert.True(expired.IsExpired);

        var active = MakeRegistration(lifetime: 86400);
        Assert.False(active.IsExpired);
    }
}
