using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using WakeOnLan.Helpers;
using WakeOnLan.Models;
using WakeOnLan.Services;

namespace WakeOnLan.Views;

public partial class DeviceDialog : Window
{
    private readonly Device? _original;
    private readonly IReadOnlyList<Device> _existing;

    public DeviceDialog(Device? device, IEnumerable<Device> existing)
    {
        InitializeComponent();
        WindowEffects.Apply(this);

        _original = device;
        _existing = existing.ToList();

        if (device is not null)
        {
            Title = HeaderText.Text = "Editar dispositivo";
            SaveButton.Content = "Guardar";
            NameBox.Text = device.Name;
            IpBox.Text = device.IpAddress;
            MacBox.Text = device.MacAddress;
            BroadcastBox.Text = device.BroadcastAddress;
        }

        PortBox.Text = (device?.Port ?? Device.DefaultPort).ToString();

        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    public Device Result { get; private set; } = new();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        var ip = IpBox.Text.Trim();
        var broadcast = BroadcastBox.Text.Trim();

        if (name.Length == 0)
        {
            ShowError("Escribe un nombre para el dispositivo.", NameBox);
            return;
        }

        if (!MacAddressHelper.TryParse(MacBox.Text, out var macBytes))
        {
            ShowError("La dirección MAC no es válida. Usa el formato AA:BB:CC:DD:EE:FF.", MacBox);
            return;
        }

        var mac = MacAddressHelper.Format(macBytes);
        var duplicate = _existing.FirstOrDefault(d =>
            !ReferenceEquals(d, _original) &&
            string.Equals(MacAddressHelper.Normalize(d.MacAddress), mac, StringComparison.OrdinalIgnoreCase));
        if (duplicate is not null)
        {
            ShowError($"«{duplicate.Name}» ya usa la MAC {mac}.", MacBox);
            return;
        }

        if (ip.Length > 0 && !NetworkHelper.IsValidHost(ip))
        {
            ShowError("La dirección IP o el nombre de host no son válidos.", IpBox);
            return;
        }

        if (broadcast.Length > 0 &&
            !(IPAddress.TryParse(broadcast, out var target) && target.AddressFamily == AddressFamily.InterNetwork))
        {
            ShowError("La dirección de envío debe ser una IPv4, por ejemplo 192.168.1.255.", BroadcastBox);
            return;
        }

        if (!int.TryParse(PortBox.Text.Trim(), out var port) || port is < 1 or > 65535)
        {
            ShowError("El puerto debe ser un número entre 1 y 65535 (normalmente 9).", PortBox);
            return;
        }

        Result = new Device
        {
            Id = _original?.Id ?? Guid.NewGuid(),
            Name = name,
            MacAddress = mac,
            IpAddress = ip,
            BroadcastAddress = broadcast,
            Port = port,
            LastSeen = _original?.LastSeen,
            LastWake = _original?.LastWake,
        };
        DialogResult = true;
    }

    private async void DetectMac_Click(object sender, RoutedEventArgs e)
    {
        var host = IpBox.Text.Trim();
        if (host.Length == 0)
        {
            ShowError("Escribe primero la IP o el nombre del equipo.", IpBox);
            return;
        }

        ErrorPanel.Visibility = Visibility.Collapsed;
        DetectMacButton.IsEnabled = false;
        DetectMacText.Text = "Buscando…";
        try
        {
            var mac = await NetworkHelper.ResolveMacAsync(host);
            if (mac is not null)
                MacBox.Text = mac;
            else
                ShowError("No se pudo obtener la MAC. El equipo tiene que estar encendido y en tu misma red local.", IpBox);
        }
        finally
        {
            DetectMacButton.IsEnabled = true;
            DetectMacText.Text = "Obtener desde IP";
        }
    }

    private void ShowError(string message, Control? focus)
    {
        ErrorText.Text = message;
        ErrorPanel.Visibility = Visibility.Visible;
        focus?.Focus();
    }
}
