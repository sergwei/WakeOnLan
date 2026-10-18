using System.Buffers.Binary;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Numerics;
using System.Runtime.InteropServices;

namespace WakeOnLan.Services;

/// <summary>Una dirección IPv4 local con su máscara (una "red" a la que está conectado este equipo).</summary>
public sealed record LocalNetwork(string InterfaceName, IPAddress Address, IPAddress Mask, byte[]? PhysicalAddress)
{
    private uint AddressValue => NetworkHelper.ToUInt32(Address);
    private uint MaskValue => NetworkHelper.ToUInt32(Mask);

    public int PrefixLength => BitOperations.PopCount(MaskValue);

    public IPAddress Broadcast => NetworkHelper.FromUInt32(AddressValue | ~MaskValue);

    // Las redes más grandes que /22 se limitan al /24 propio para que el escaneo no dure minutos.
    private uint ScanMask => PrefixLength < 22 ? 0xFFFFFF00u : MaskValue;

    public string ScanRange => $"{NetworkHelper.FromUInt32(AddressValue & ScanMask)}/{BitOperations.PopCount(ScanMask)}";

    public IEnumerable<IPAddress> ScanHosts()
    {
        if (PrefixLength > 30)
            yield break;

        var network = AddressValue & ScanMask;
        var broadcast = network | ~ScanMask;
        for (var ip = network + 1; ip < broadcast; ip++)
        {
            if (ip != AddressValue)
                yield return NetworkHelper.FromUInt32(ip);
        }
    }
}

public static class NetworkHelper
{
    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern int SendARP(uint destIp, uint srcIp, byte[] macAddress, ref uint macAddressLength);

    public static uint ToUInt32(IPAddress ip) => BinaryPrimitives.ReadUInt32BigEndian(ip.GetAddressBytes());

    public static IPAddress FromUInt32(uint value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return new IPAddress(bytes);
    }

    public static bool IsValidHost(string host) =>
        Uri.CheckHostName(host) is UriHostNameType.IPv4 or UriHostNameType.Dns;

    /// <param name="preferGateway">
    /// Si es true y hay interfaces con puerta de enlace, devuelve solo esas (descarta adaptadores
    /// virtuales como Hyper-V o WSL, que no interesan al escanear).
    /// </param>
    public static IReadOnlyList<LocalNetwork> GetLocalNetworks(bool preferGateway = true)
    {
        var networks = new List<(LocalNetwork Network, bool HasGateway)>();

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up ||
                nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                continue;

            IPInterfaceProperties properties;
            try
            {
                properties = nic.GetIPProperties();
            }
            catch (NetworkInformationException)
            {
                continue;
            }

            var hasGateway = properties.GatewayAddresses.Any(g =>
                g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any));
            var mac = nic.GetPhysicalAddress().GetAddressBytes();

            foreach (var unicast in properties.UnicastAddresses)
            {
                if (unicast.Address.AddressFamily != AddressFamily.InterNetwork)
                    continue;

                var bytes = unicast.Address.GetAddressBytes();
                if (bytes[0] == 169 && bytes[1] == 254)
                    continue; // APIPA: sin DHCP, no es una red real.

                if (unicast.IPv4Mask is null || unicast.IPv4Mask.Equals(IPAddress.Any))
                    continue;

                var network = new LocalNetwork(nic.Name, unicast.Address, unicast.IPv4Mask, mac.Length == 6 ? mac : null);
                networks.Add((network, hasGateway));
            }
        }

        if (preferGateway && networks.Any(n => n.HasGateway))
            return networks.Where(n => n.HasGateway).Select(n => n.Network).ToList();

        return networks.Select(n => n.Network).ToList();
    }

    public static async Task<bool> PingAsync(string host, int timeoutMs)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, timeoutMs).ConfigureAwait(false);
            return reply.Status == IPStatus.Success;
        }
        catch (Exception ex) when (ex is PingException or SocketException or ArgumentException)
        {
            return false;
        }
    }

    public static async Task<string?> ResolveHostNameAsync(IPAddress ip, int timeoutMs)
    {
        try
        {
            var entry = await Dns.GetHostEntryAsync(ip).WaitAsync(TimeSpan.FromMilliseconds(timeoutMs)).ConfigureAwait(false);
            var name = entry.HostName;
            return string.IsNullOrWhiteSpace(name) || name == ip.ToString() ? null : name;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Pregunta por ARP la MAC de una IP de la red local. Bloquea hasta ~3 s si nadie responde,
    /// por eso se llama siempre desde un hilo de fondo.
    /// </summary>
    public static string? GetMacFromArp(IPAddress ip)
    {
        try
        {
            var destination = BitConverter.ToUInt32(ip.GetAddressBytes(), 0);
            var mac = new byte[8]; // La API pide al menos dos ULONG.
            var length = (uint)mac.Length;

            if (SendARP(destination, 0, mac, ref length) != 0 || length != 6)
                return null;

            var bytes = mac.Take(6).ToArray();
            return bytes.All(b => b == 0) ? null : MacAddressHelper.Format(bytes);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Obtiene la MAC de un equipo encendido a partir de su IP o nombre de host.</summary>
    public static async Task<string?> ResolveMacAsync(string host)
    {
        try
        {
            if (!IPAddress.TryParse(host, out var ip))
            {
                var addresses = await Dns.GetHostAddressesAsync(host).ConfigureAwait(false);
                ip = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
            }

            if (ip is null)
                return null;

            await PingAsync(ip.ToString(), 1000).ConfigureAwait(false); // Rellena la caché ARP.
            return await Task.Run(() => GetMacFromArp(ip)).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }
}
