using System.Text;
using Tinkwell.Package;

namespace Tinkwell.Package.Tests;

public class PackageHasherTests
{
    [Fact]
    public void VerifyHash_CorrectHash_ReturnsTrue()
    {
        var data = Encoding.UTF8.GetBytes("payload");
        var expected = PackageHasher.ComputeHash(data);

        Assert.True(PackageHasher.VerifyHash(data, expected));
    }

    [Fact]
    public void VerifyHash_NullOrEmptyArgument_ReturnsFalse()
    {
        var data = new byte[1];
        var good = PackageHasher.ComputeHash(data);

        Assert.False(PackageHasher.VerifyHash(data, null!));
        Assert.False(PackageHasher.VerifyHash(data, ""));
    }

    [Fact]
    public void VerifyHash_WrongLength_ReturnsFalse()
    {
        var data = Encoding.UTF8.GetBytes("a");
        var good = PackageHasher.ComputeHash(data);

        Assert.False(PackageHasher.VerifyHash(data, good[..^1]));
        Assert.False(PackageHasher.VerifyHash(data, good + "0"));
    }

    [Fact]
    public void VerifyHash_InvalidHexCharacters_ReturnsFalse()
    {
        var data = new byte[1];
        var good = PackageHasher.ComputeHash(data);
        // Same length, invalid char 'g' (FormatException in FromHex is swallowed).
        var mangled = good[..(good.Length - 1)] + "g";

        Assert.False(PackageHasher.VerifyHash(data, mangled));
    }
}
