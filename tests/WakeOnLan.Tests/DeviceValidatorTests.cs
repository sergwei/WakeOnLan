using System.Net;
using WakeOnLan.Models;
using WakeOnLan.Services;

namespace WakeOnLan.Tests;

public class DeviceValidatorTests
{
    [Theory]
    [InlineData("10.0.0.5", true)]
    [InlineData("172.16.0.1", true)]
    [InlineData("172.31.255.255", true)]
    [InlineData("192.168.1.20", true)]
    [InlineData("169.254.10.10", true)]   // enlace local
    [InlineData("100.64.0.1", true)]      // CGNAT / Tailscale
    [InlineData("100.127.255.254", true)]
    [InlineData("127.0.0.1", true)]
    [InlineData("255.255.255.255", true)] // broadcast limitado
    [InlineData("172.15.0.1", false)]
    [InlineData("172.32.0.1", false)]
    [InlineData("100.128.0.1", false)]
    [InlineData("8.8.8.8", false)]
    [InlineData("1.1.1.1", false)]
    [InlineData("::1", false)]            // solo IPv4
    public void IsLocalAddress_ClassifiesAddresses(string address, bool expected)
    {
        Assert.Equal(expected, DeviceValidator.IsLocalAddress(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("pc-salon", true)]
    [InlineData("pc-salon.local", true)]
    [InlineData("nas.lan", true)]
    [InlineData("router.home.arpa", true)]
    [InlineData("server.INTERNAL", true)]
    [InlineData("192.168.1.50", true)]
    [InlineData("tracker.atacante.com", false)]
    [InlineData("example.org", false)]
    [InlineData("8.8.8.8", false)]
    public void IsLocalHost_OnlyAcceptsLocalNames(string host, bool expected)
    {
        Assert.Equal(expected, DeviceValidator.IsLocalHost(host));
    }

    [Theory]
    [InlineData("192.168.1.20", true)]
    [InlineData("pc-salon", true)]
    [InlineData("pc.example.com", true)]
    [InlineData("no válido", false)]
    [InlineData("a/b", false)]
    public void IsValidHost_ChecksFormat(string host, bool expected)
    {
        Assert.Equal(expected, DeviceValidator.IsValidHost(host));
    }

    [Fact]
    public void IsValidHost_RejectsTooLongNames()
    {
        Assert.False(DeviceValidator.IsValidHost(new string('a', DeviceValidator.MaxHostLength + 1)));
    }

    [Theory]
    [InlineData("192.168.1.255", true)]
    [InlineData("255.255.255.255", true)]
    [InlineData("fe80::1", false)]
    [InlineData("pc-salon", false)]
    public void IsValidTarget_RequiresIPv4(string address, bool expected)
    {
        Assert.Equal(expected, DeviceValidator.IsValidTarget(address));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(9, true)]
    [InlineData(65535, true)]
    [InlineData(65536, false)]
    public void IsValidPort_ChecksRange(int port, bool expected)
    {
        Assert.Equal(expected, DeviceValidator.IsValidPort(port));
    }

    [Fact]
    public void ValidateName_RequiresNonEmptyNameWithinLimit()
    {
        Assert.NotNull(DeviceValidator.ValidateName(""));
        Assert.NotNull(DeviceValidator.ValidateName(new string('x', DeviceValidator.MaxNameLength + 1)));
        Assert.Null(DeviceValidator.ValidateName("PC del salón"));
    }

    [Fact]
    public void TrySanitizeImported_RemovesBeaconHostAndPublicTarget()
    {
        var device = new Device
        {
            Name = "Trampa",
            MacAddress = "aa-bb-cc-dd-ee-ff",
            IpAddress = "tracker.atacante.com",
            BroadcastAddress = "203.0.113.10",
        };

        Assert.True(DeviceValidator.TrySanitizeImported(device, out var removed));
        Assert.True(removed);
        Assert.Equal(string.Empty, device.IpAddress);
        Assert.Equal(string.Empty, device.BroadcastAddress);
        Assert.Equal("AA:BB:CC:DD:EE:FF", device.MacAddress);
    }

    [Fact]
    public void TrySanitizeImported_KeepsLocalAddresses()
    {
        var device = new Device
        {
            Name = "NAS",
            MacAddress = "AA:BB:CC:DD:EE:FF",
            IpAddress = "192.168.1.10",
            BroadcastAddress = "192.168.1.255",
            Port = 7,
        };

        Assert.True(DeviceValidator.TrySanitizeImported(device, out var removed));
        Assert.False(removed);
        Assert.Equal("192.168.1.10", device.IpAddress);
        Assert.Equal("192.168.1.255", device.BroadcastAddress);
        Assert.Equal(7, device.Port);
    }

    [Fact]
    public void TrySanitizeImported_RejectsInvalidMac()
    {
        var device = new Device { Name = "Sin MAC", MacAddress = "xyz" };
        Assert.False(DeviceValidator.TrySanitizeImported(device, out _));
    }

    [Fact]
    public void TrySanitizeImported_FixesNameAndPort()
    {
        var unnamed = new Device { Name = "   ", MacAddress = "AABBCCDDEEFF", Port = 99999 };
        Assert.True(DeviceValidator.TrySanitizeImported(unnamed, out _));
        Assert.Equal("AA:BB:CC:DD:EE:FF", unnamed.Name);
        Assert.Equal(Device.DefaultPort, unnamed.Port);

        var longName = new Device { Name = new string('x', 500), MacAddress = "AABBCCDDEEFF" };
        Assert.True(DeviceValidator.TrySanitizeImported(longName, out _));
        Assert.Equal(DeviceValidator.MaxNameLength, longName.Name.Length);
    }

    [Fact]
    public void TrySanitizeImported_AssignsNewIdAndResetsStatus()
    {
        var id = Guid.NewGuid();
        var device = new Device { Id = id, MacAddress = "AABBCCDDEEFF", Status = DeviceStatus.Online };

        Assert.True(DeviceValidator.TrySanitizeImported(device, out _));
        Assert.NotEqual(id, device.Id);
        Assert.Equal(DeviceStatus.Unknown, device.Status);
    }
}
