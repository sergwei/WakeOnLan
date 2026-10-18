using System.Net;
using System.Net.Sockets;
using WakeOnLan.Models;

namespace WakeOnLan.Services;

public static class WakeOnLanService
{
    private const int Repetitions = 3;

    /// <summary>Paquete mágico: 6 bytes 0xFF seguidos de la MAC repetida 16 veces (102 bytes).</summary>
    public static byte[] BuildMagicPacket(byte[] mac)
    {
        var packet = new byte[6 + 16 * 6];
        for (var i = 0; i < 6; i++)
            packet[i] = 0xFF;
        for (var i = 1; i <= 16; i++)
            Buffer.BlockCopy(mac, 0, packet, i * 6, 6);
        return packet;
    }

    public static async Task WakeAsync(Device device)
    {
        if (!MacAddressHelper.TryParse(device.MacAddress, out var mac))
            throw new FormatException($"La dirección MAC «{device.MacAddress}» no es válida.");

        var packet = BuildMagicPacket(mac);
        var ports = new HashSet<int>
        {
            device.Port is > 0 and <= 65535 ? device.Port : Device.DefaultPort,
            Device.DefaultPort,
        };

        var sent = 0;
        if (IPAddress.TryParse(device.BroadcastAddress, out var customTarget))
        {
            // Destino fijo: el broadcast de una subred concreta (por ejemplo, la de casa a través de una VPN).
            sent += await SendAsync(null, customTarget, ports, packet).ConfigureAwait(false);
        }
        else
        {
            // Un broadcast a 255.255.255.255 solo sale por una interfaz, así que se envía por cada
            // red local tanto al broadcast general como al de la subred.
            foreach (var network in NetworkHelper.GetLocalNetworks(preferGateway: false))
            {
                sent += await SendAsync(network.Address, network.Broadcast, ports, packet).ConfigureAwait(false);
                sent += await SendAsync(network.Address, IPAddress.Broadcast, ports, packet).ConfigureAwait(false);
            }

            if (sent == 0)
                sent += await SendAsync(null, IPAddress.Broadcast, ports, packet).ConfigureAwait(false);
        }

        if (sent == 0)
            throw new InvalidOperationException("No se pudo enviar el paquete por ninguna interfaz de red.");
    }

    private static async Task<int> SendAsync(IPAddress? localAddress, IPAddress target, IEnumerable<int> ports, byte[] packet)
    {
        try
        {
            using var client = localAddress is null
                ? new UdpClient(AddressFamily.InterNetwork)
                : new UdpClient(new IPEndPoint(localAddress, 0));
            client.EnableBroadcast = true;

            var count = 0;
            for (var i = 0; i < Repetitions; i++)
            {
                foreach (var port in ports)
                {
                    await client.SendAsync(packet, packet.Length, new IPEndPoint(target, port)).ConfigureAwait(false);
                    count++;
                }
            }
            return count;
        }
        catch (SocketException)
        {
            return 0;
        }
    }
}
