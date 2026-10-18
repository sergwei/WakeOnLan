using System.Text;
using WakeOnLan.Models;
using WakeOnLan.Services;

namespace WakeOnLan.Tests;

public sealed class DeviceStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "WakeOnLan.Tests", Guid.NewGuid().ToString("N"));

    public DeviceStoreTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private string FilePath(string name = "devices.json") => Path.Combine(_folder, name);

    [Fact]
    public void WriteThenRead_RoundTripsPersistentFields()
    {
        var lastSeen = new DateTime(2026, 10, 18, 12, 0, 0);
        var original = new Device
        {
            Name = "PC del salón",
            MacAddress = "AA:BB:CC:DD:EE:FF",
            IpAddress = "192.168.1.20",
            BroadcastAddress = "192.168.1.255",
            Port = 7,
            LastSeen = lastSeen,
        };

        DeviceStore.Write(FilePath(), new[] { original });
        var loaded = Assert.Single(DeviceStore.Read(FilePath()));

        Assert.Equal(original.Id, loaded.Id);
        Assert.Equal(original.Name, loaded.Name);
        Assert.Equal(original.MacAddress, loaded.MacAddress);
        Assert.Equal(original.IpAddress, loaded.IpAddress);
        Assert.Equal(original.BroadcastAddress, loaded.BroadcastAddress);
        Assert.Equal(7, loaded.Port);
        Assert.Equal(lastSeen, loaded.LastSeen);
    }

    [Fact]
    public void Write_DoesNotPersistRuntimeStatus()
    {
        DeviceStore.Write(FilePath(), new[] { new Device { Name = "PC", Status = DeviceStatus.Online } });

        var json = File.ReadAllText(FilePath());
        Assert.DoesNotContain("Status", json);
        Assert.DoesNotContain("Subtitle", json);
        Assert.Equal(DeviceStatus.Unknown, Assert.Single(DeviceStore.Read(FilePath())).Status);
    }

    [Fact]
    public void Write_LeavesNoTemporaryFile()
    {
        DeviceStore.Write(FilePath(), Array.Empty<Device>());
        Assert.False(File.Exists(FilePath() + ".tmp"));
    }

    [Fact]
    public void Read_RejectsFilesOverTheSizeLimit()
    {
        File.WriteAllText(FilePath(), new string(' ', (int)DeviceStore.MaxFileBytes + 1), Encoding.ASCII);
        Assert.Throws<InvalidDataException>(() => DeviceStore.Read(FilePath()));
    }

    [Fact]
    public void Read_RejectsTooManyDevices()
    {
        File.WriteAllText(FilePath(), "[" + string.Join(",", Enumerable.Repeat("{}", DeviceStore.MaxDevices + 1)) + "]");
        Assert.Throws<InvalidDataException>(() => DeviceStore.Read(FilePath()));
    }

    [Fact]
    public void Read_IsCaseInsensitive()
    {
        File.WriteAllText(FilePath(), """[{"name":"NAS","macaddress":"AA:BB:CC:DD:EE:FF"}]""");
        var device = Assert.Single(DeviceStore.Read(FilePath()));
        Assert.Equal("NAS", device.Name);
        Assert.Equal("AA:BB:CC:DD:EE:FF", device.MacAddress);
    }
}
