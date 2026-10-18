using WakeOnLan.Models;
using WakeOnLan.Services;

namespace WakeOnLan.Tests;

public class WakeOnLanServiceTests
{
    private static readonly byte[] Mac = { 0x01, 0x23, 0x45, 0x67, 0x89, 0xAB };

    [Fact]
    public void BuildMagicPacket_Is102Bytes()
    {
        Assert.Equal(102, WakeOnLanService.BuildMagicPacket(Mac).Length);
    }

    [Fact]
    public void BuildMagicPacket_StartsWithSixFFBytes()
    {
        var packet = WakeOnLanService.BuildMagicPacket(Mac);
        Assert.All(packet.Take(6), b => Assert.Equal(0xFF, b));
    }

    [Fact]
    public void BuildMagicPacket_RepeatsTheMacSixteenTimes()
    {
        var packet = WakeOnLanService.BuildMagicPacket(Mac);
        for (var i = 1; i <= 16; i++)
            Assert.Equal(Mac, packet.Skip(i * 6).Take(6).ToArray());
    }

    [Fact]
    public async Task WakeAsync_RejectsInvalidMacWithoutSending()
    {
        var device = new Device { Name = "PC", MacAddress = "no-válida" };
        await Assert.ThrowsAsync<FormatException>(() => WakeOnLanService.WakeAsync(device));
    }
}
