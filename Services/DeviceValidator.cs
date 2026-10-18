using System.Net;
using System.Net.Sockets;
using WakeOnLan.Models;

namespace WakeOnLan.Services;

/// <summary>
/// Reglas únicas de validación de dispositivos, compartidas por el diálogo de edición y la importación.
/// </summary>
public static class DeviceValidator
{
    public const int MaxNameLength = 100;
    public const int MaxHostLength = 253;

    // Sufijos de nombres de host que solo se resuelven dentro de la red local.
    private static readonly string[] LocalHostSuffixes = { ".local", ".lan", ".home", ".home.arpa", ".internal", ".localdomain" };

    public static string? ValidateName(string name) =>
        name.Length == 0 ? "Escribe un nombre para el dispositivo."
        : name.Length > MaxNameLength ? $"El nombre no puede superar los {MaxNameLength} caracteres."
        : null;

    public static bool IsValidHost(string host) =>
        host.Length <= MaxHostLength && Uri.CheckHostName(host) is UriHostNameType.IPv4 or UriHostNameType.Dns;

    public static bool IsValidTarget(string address) =>
        IPAddress.TryParse(address, out var ip) && ip.AddressFamily == AddressFamily.InterNetwork;

    public static bool IsValidPort(int port) => port is >= 1 and <= 65535;

    /// <summary>IPv4 privada, de enlace local, CGNAT (Tailscale), loopback o broadcast limitado.</summary>
    public static bool IsLocalAddress(IPAddress ip)
    {
        if (ip.AddressFamily != AddressFamily.InterNetwork)
            return false;

        var b = ip.GetAddressBytes();
        return b[0] == 10
            || b[0] == 127
            || (b[0] == 172 && b[1] is >= 16 and <= 31)
            || (b[0] == 192 && b[1] == 168)
            || (b[0] == 169 && b[1] == 254)
            || (b[0] == 100 && b[1] is >= 64 and <= 127)
            || ip.Equals(IPAddress.Broadcast);
    }

    /// <summary>Una IP local, un nombre sin dominio ("pc-salon") o con un sufijo de red local.</summary>
    public static bool IsLocalHost(string host)
    {
        if (IPAddress.TryParse(host, out var ip))
            return IsLocalAddress(ip);

        return !host.Contains('.') ||
               LocalHostSuffixes.Any(suffix => host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Limpia un dispositivo procedente de un archivo de terceros. Devuelve false si debe descartarse.
    /// Las direcciones que no son de la red local se eliminan: un archivo malicioso podría usarlas como
    /// baliza (la app haría ping a ese host cada pocos segundos) o para enviar tu MAC fuera de tu red.
    /// </summary>
    public static bool TrySanitizeImported(Device device, out bool removedAddress)
    {
        removedAddress = false;
        if (!MacAddressHelper.TryParse(device.MacAddress, out var mac))
            return false;

        device.MacAddress = MacAddressHelper.Format(mac);

        var name = device.Name.Trim();
        device.Name = name.Length == 0 ? device.MacAddress
            : name.Length > MaxNameLength ? name[..MaxNameLength]
            : name;

        var host = device.IpAddress.Trim();
        if (host.Length > 0 && !(IsValidHost(host) && IsLocalHost(host)))
        {
            host = string.Empty;
            removedAddress = true;
        }
        device.IpAddress = host;

        var target = device.BroadcastAddress.Trim();
        if (target.Length > 0 && !(IPAddress.TryParse(target, out var targetIp) &&
                                   targetIp.AddressFamily == AddressFamily.InterNetwork && IsLocalAddress(targetIp)))
        {
            target = string.Empty;
            removedAddress = true;
        }
        device.BroadcastAddress = target;

        if (!IsValidPort(device.Port))
            device.Port = Device.DefaultPort;

        device.Id = Guid.NewGuid();
        device.Status = DeviceStatus.Unknown;
        return true;
    }
}
