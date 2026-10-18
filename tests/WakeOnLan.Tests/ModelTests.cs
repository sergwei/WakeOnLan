using System.Globalization;
using WakeOnLan.Converters;
using WakeOnLan.Models;

namespace WakeOnLan.Tests;

public class DeviceTests
{
    [Fact]
    public void Subtitle_JoinsAvailableIpAndMac()
    {
        Assert.Equal("192.168.1.2  ·  AA:BB:CC:DD:EE:FF",
            new Device { IpAddress = "192.168.1.2", MacAddress = "AA:BB:CC:DD:EE:FF" }.Subtitle);
        Assert.Equal("AA:BB:CC:DD:EE:FF", new Device { MacAddress = "AA:BB:CC:DD:EE:FF" }.Subtitle);
    }

    [Theory]
    [InlineData(DeviceStatus.Online, "", "En línea")]
    [InlineData(DeviceStatus.Offline, "", "Apagado")]
    [InlineData(DeviceStatus.Waking, "", "Encendiendo…")]
    [InlineData(DeviceStatus.Unknown, "", "Sin IP")]
    [InlineData(DeviceStatus.Unknown, "192.168.1.2", "Comprobando…")]
    public void StatusText_DescribesStatus(DeviceStatus status, string ip, string expected)
    {
        Assert.Equal(expected, new Device { IpAddress = ip, Status = status }.StatusText);
    }

    [Fact]
    public void ChangingIp_NotifiesSubtitleAndStatusText()
    {
        var device = new Device();
        var changed = new List<string?>();
        device.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        device.IpAddress = "192.168.1.2";

        Assert.Contains(nameof(Device.IpAddress), changed);
        Assert.Contains(nameof(Device.Subtitle), changed);
        Assert.Contains(nameof(Device.StatusText), changed);
    }

    [Fact]
    public void SettingSameValue_DoesNotNotify()
    {
        var device = new Device { Name = "PC" };
        var notified = false;
        device.PropertyChanged += (_, _) => notified = true;

        device.Name = "PC";

        Assert.False(notified);
    }

    [Fact]
    public void CopyFrom_CopiesEditableFieldsButKeepsIdentity()
    {
        var target = new Device { Name = "Antes" };
        var id = target.Id;
        var source = new Device
        {
            Name = "Después",
            MacAddress = "AA:BB:CC:DD:EE:FF",
            IpAddress = "pc",
            BroadcastAddress = "192.168.1.255",
            Port = 7,
        };

        target.CopyFrom(source);

        Assert.Equal(id, target.Id);
        Assert.Equal("Después", target.Name);
        Assert.Equal("AA:BB:CC:DD:EE:FF", target.MacAddress);
        Assert.Equal("pc", target.IpAddress);
        Assert.Equal("192.168.1.255", target.BroadcastAddress);
        Assert.Equal(7, target.Port);
    }
}

public class ScanResultTests
{
    [Fact]
    public void ToDevice_UsesShortHostName()
    {
        var device = new ScanResult("192.168.1.5", "AA:BB:CC:DD:EE:FF", "pc-salon.home", isThisPc: false).ToDevice();

        Assert.Equal("pc-salon", device.Name);
        Assert.Equal("192.168.1.5", device.IpAddress);
        Assert.Equal("AA:BB:CC:DD:EE:FF", device.MacAddress);
    }

    [Fact]
    public void ToDevice_FallsBackToIpWithoutHostName()
    {
        Assert.Equal("192.168.1.5", new ScanResult("192.168.1.5", "AA:BB:CC:DD:EE:FF", "", false).ToDevice().Name);
    }

    [Fact]
    public void Badge_PrioritizesAlreadyAdded()
    {
        var result = new ScanResult("192.168.1.5", "AA:BB:CC:DD:EE:FF", "", isThisPc: true);
        Assert.Equal("Este equipo", result.Badge);

        result.AlreadyAdded = true;
        Assert.Equal("Ya añadido", result.Badge);
        Assert.False(result.CanSelect);
    }

    [Fact]
    public void SortKey_OrdersNumerically()
    {
        var low = new ScanResult("192.168.1.9", "", "", false);
        var high = new ScanResult("192.168.1.10", "", "", false);
        Assert.True(low.SortKey < high.SortKey);
    }
}

public class LastSeenConverterTests
{
    private static string Convert(object? value) =>
        (string)new LastSeenConverter().Convert(value!, typeof(string), null!, CultureInfo.InvariantCulture);

    [Fact]
    public void NullMeansNeverSeen() => Assert.Equal("Nunca visto en línea", Convert(null));

    [Fact]
    public void RecentMeansNow() => Assert.Equal("Visto ahora mismo", Convert(DateTime.Now.AddSeconds(-10)));

    [Fact]
    public void MinutesAgo() => Assert.Equal("Visto hace 5 min", Convert(DateTime.Now.AddMinutes(-5).AddSeconds(-5)));

    [Fact]
    public void HoursAgo() => Assert.Equal("Visto hace 3 h", Convert(DateTime.Now.AddHours(-3).AddMinutes(-1)));

    [Fact]
    public void OldDatesShowTheDate()
    {
        var date = new DateTime(2025, 1, 2);
        Assert.Equal("Visto el 02/01/2025", Convert(date));
    }
}
