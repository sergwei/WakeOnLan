using System.Text.Json.Serialization;
using WakeOnLan.Helpers;

namespace WakeOnLan.Models;

public enum DeviceStatus
{
    Unknown,
    Online,
    Offline,
    Waking,
}

public sealed class Device : ObservableObject
{
    public const int DefaultPort = 9;

    private string _name = string.Empty;
    private string _macAddress = string.Empty;
    private string _ipAddress = string.Empty;
    private string _broadcastAddress = string.Empty;
    private int _port = DefaultPort;
    private DateTime? _lastSeen;
    private DateTime? _lastWake;
    private DeviceStatus _status;

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value ?? string.Empty);
    }

    public string MacAddress
    {
        get => _macAddress;
        set
        {
            if (SetProperty(ref _macAddress, value ?? string.Empty))
                OnPropertyChanged(nameof(Subtitle));
        }
    }

    /// <summary>IP o nombre de host. Opcional: solo se usa para saber si el equipo está encendido.</summary>
    public string IpAddress
    {
        get => _ipAddress;
        set
        {
            if (!SetProperty(ref _ipAddress, value ?? string.Empty))
                return;
            OnPropertyChanged(nameof(Subtitle));
            OnPropertyChanged(nameof(StatusText));
        }
    }

    /// <summary>Dirección de destino del paquete. Vacía = broadcast automático en todas las redes.</summary>
    public string BroadcastAddress
    {
        get => _broadcastAddress;
        set => SetProperty(ref _broadcastAddress, value ?? string.Empty);
    }

    public int Port
    {
        get => _port;
        set => SetProperty(ref _port, value);
    }

    public DateTime? LastSeen
    {
        get => _lastSeen;
        set => SetProperty(ref _lastSeen, value);
    }

    public DateTime? LastWake
    {
        get => _lastWake;
        set => SetProperty(ref _lastWake, value);
    }

    [JsonIgnore]
    public DeviceStatus Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
                OnPropertyChanged(nameof(StatusText));
        }
    }

    [JsonIgnore]
    public string StatusText => Status switch
    {
        DeviceStatus.Online => "En línea",
        DeviceStatus.Offline => "Apagado",
        DeviceStatus.Waking => "Encendiendo…",
        _ => string.IsNullOrWhiteSpace(IpAddress) ? "Sin IP" : "Comprobando…",
    };

    [JsonIgnore]
    public string Subtitle => string.Join("  ·  ",
        new[] { IpAddress, MacAddress }.Where(part => !string.IsNullOrWhiteSpace(part)));

    public void CopyFrom(Device other)
    {
        Name = other.Name;
        MacAddress = other.MacAddress;
        IpAddress = other.IpAddress;
        BroadcastAddress = other.BroadcastAddress;
        Port = other.Port;
    }

    /// <summary>Fuerza a la interfaz a recalcular los textos relativos ("visto hace 5 min").</summary>
    public void RefreshRelativeTimes() => OnPropertyChanged(nameof(LastSeen));
}
