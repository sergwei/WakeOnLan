using System.Buffers.Binary;
using System.Net;
using WakeOnLan.Helpers;

namespace WakeOnLan.Models;

public sealed class ScanResult : ObservableObject
{
    private bool _isSelected;

    public ScanResult(string ipAddress, string macAddress, string hostName, bool isThisPc)
    {
        IpAddress = ipAddress;
        MacAddress = macAddress;
        HostName = hostName;
        IsThisPc = isThisPc;
        SortKey = IPAddress.TryParse(ipAddress, out var ip)
            ? BinaryPrimitives.ReadUInt32BigEndian(ip.GetAddressBytes())
            : uint.MaxValue;
    }

    public string IpAddress { get; }
    public string MacAddress { get; }
    public string HostName { get; }
    public bool IsThisPc { get; }
    public uint SortKey { get; }

    public bool AlreadyAdded { get; set; }

    public bool CanSelect => !AlreadyAdded;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string DisplayName => string.IsNullOrEmpty(HostName) ? IpAddress : HostName;

    public string Details => $"{IpAddress}  ·  {MacAddress}";

    public string Badge => AlreadyAdded ? "Ya añadido" : IsThisPc ? "Este equipo" : string.Empty;

    public Device ToDevice() => new()
    {
        // "pc-salon.home" -> "pc-salon"
        Name = string.IsNullOrEmpty(HostName) ? IpAddress : HostName.Split('.')[0],
        MacAddress = MacAddress,
        IpAddress = IpAddress,
        LastSeen = DateTime.Now,
    };
}
