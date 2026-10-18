using System.Net;
using WakeOnLan.Services;

namespace WakeOnLan.Tests;

public class LocalNetworkTests
{
    private static LocalNetwork Network(string address, string mask) =>
        new("Ethernet", IPAddress.Parse(address), IPAddress.Parse(mask), null);

    [Theory]
    [InlineData("192.168.1.20", "255.255.255.0", "192.168.1.255", 24)]
    [InlineData("10.0.5.7", "255.255.0.0", "10.0.255.255", 16)]
    [InlineData("172.16.3.4", "255.255.252.0", "172.16.3.255", 22)]
    public void ComputesBroadcastAndPrefix(string address, string mask, string broadcast, int prefix)
    {
        var network = Network(address, mask);
        Assert.Equal(IPAddress.Parse(broadcast), network.Broadcast);
        Assert.Equal(prefix, network.PrefixLength);
    }

    [Fact]
    public void ScanHosts_CoversTheSubnetExceptNetworkBroadcastAndSelf()
    {
        var hosts = Network("192.168.1.20", "255.255.255.0").ScanHosts().ToList();

        Assert.Equal(253, hosts.Count);
        Assert.DoesNotContain(IPAddress.Parse("192.168.1.0"), hosts);
        Assert.DoesNotContain(IPAddress.Parse("192.168.1.255"), hosts);
        Assert.DoesNotContain(IPAddress.Parse("192.168.1.20"), hosts);
        Assert.Contains(IPAddress.Parse("192.168.1.1"), hosts);
        Assert.Contains(IPAddress.Parse("192.168.1.254"), hosts);
    }

    [Fact]
    public void ScanHosts_LimitsLargeNetworksToTheOwnSlash24()
    {
        var network = Network("10.0.5.7", "255.255.0.0");

        Assert.Equal("10.0.5.0/24", network.ScanRange);
        Assert.Equal(253, network.ScanHosts().Count());
    }

    [Fact]
    public void ScanHosts_KeepsSlash22Networks()
    {
        var network = Network("172.16.3.4", "255.255.252.0");

        Assert.Equal("172.16.0.0/22", network.ScanRange);
        Assert.Equal(1021, network.ScanHosts().Count()); // 1024 - red - broadcast - propia
    }

    [Theory]
    [InlineData("255.255.255.254")]
    [InlineData("255.255.255.255")]
    public void ScanHosts_SkipsPointToPointNetworks(string mask)
    {
        Assert.Empty(Network("10.1.1.1", mask).ScanHosts());
    }

    [Theory]
    [InlineData("0.0.0.0", 0u)]
    [InlineData("192.168.1.1", 0xC0A80101u)]
    [InlineData("255.255.255.255", 0xFFFFFFFFu)]
    public void UInt32Conversion_RoundTrips(string address, uint value)
    {
        Assert.Equal(value, NetworkHelper.ToUInt32(IPAddress.Parse(address)));
        Assert.Equal(IPAddress.Parse(address), NetworkHelper.FromUInt32(value));
    }
}
