using System.Windows;
using WakeOnLan.Models;
using WakeOnLan.Services;

namespace WakeOnLan;

/// <summary>
/// Permite encender equipos sin abrir la interfaz (accesos directos, tareas programadas, scripts).
/// </summary>
internal static class CommandLine
{
    private const string Usage =
        "Uso:\n\n" +
        "  WakeOnLan.exe /wake <nombre | MAC | IP> [...]\n" +
        "  WakeOnLan.exe /wakeall\n\n" +
        "Sin argumentos se abre la aplicación.";

    /// <summary>Devuelve el código de salida, o null si los argumentos no son un comando reconocido.</summary>
    public static int? TryRun(string[] args)
    {
        var command = args[0].TrimStart('/', '-').ToLowerInvariant();
        return command switch
        {
            "wake" => Wake(args.Skip(1).ToList(), wakeAll: false),
            "wakeall" => Wake(Array.Empty<string>(), wakeAll: true),
            "help" or "h" or "?" => ShowUsage(0),
            _ => null,
        };
    }

    private static int Wake(IReadOnlyList<string> queries, bool wakeAll)
    {
        if (queries.Count == 0 && !wakeAll)
            return ShowUsage(2);

        var store = new DeviceStore();
        var devices = store.Load();
        var targets = new List<Device>();
        var errors = new List<string>();

        if (wakeAll)
            targets.AddRange(devices);

        foreach (var query in queries)
        {
            var match = devices.FirstOrDefault(d => Matches(d, query));
            if (match is not null)
                targets.Add(match);
            else if (MacAddressHelper.TryParse(query, out _))
                targets.Add(new Device { Name = query, MacAddress = query });
            else
                errors.Add($"No se encontró el dispositivo «{query}».");
        }

        foreach (var device in targets.Distinct())
        {
            try
            {
                // Task.Run evita el interbloqueo con el contexto de sincronización de WPF.
                Task.Run(() => WakeOnLanService.WakeAsync(device)).GetAwaiter().GetResult();
                device.LastWake = DateTime.Now;
            }
            catch (Exception ex)
            {
                errors.Add($"{device.Name}: {ex.Message}");
            }
        }

        try
        {
            store.Save(devices);
        }
        catch (Exception ex)
        {
            errors.Add($"No se pudo guardar la lista: {ex.Message}");
        }

        if (errors.Count == 0)
            return 0;

        MessageBox.Show(string.Join("\n", errors), "Wake on LAN", MessageBoxButton.OK, MessageBoxImage.Warning);
        return 1;
    }

    private static bool Matches(Device device, string query)
    {
        if (string.Equals(device.Name, query, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(device.IpAddress, query, StringComparison.OrdinalIgnoreCase))
            return true;

        return MacAddressHelper.TryParse(query, out var mac) &&
               string.Equals(MacAddressHelper.Normalize(device.MacAddress), MacAddressHelper.Format(mac), StringComparison.OrdinalIgnoreCase);
    }

    private static int ShowUsage(int exitCode)
    {
        MessageBox.Show(Usage, "Wake on LAN", MessageBoxButton.OK, MessageBoxImage.Information);
        return exitCode;
    }
}
