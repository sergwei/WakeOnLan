using WakeOnLan.Models;

namespace WakeOnLan.Services;

public static class NetworkScanner
{
    private const int MaxParallelism = 64;

    /// <summary>
    /// Recorre las redes locales preguntando por ARP cada dirección. ARP encuentra también equipos
    /// con el firewall bloqueando el ping, y además nos da la MAC que necesita Wake-on-LAN.
    /// </summary>
    public static async Task ScanAsync(
        IReadOnlyList<LocalNetwork> networks,
        IProgress<ScanResult> found,
        IProgress<(int Done, int Total)> progress,
        CancellationToken cancellationToken)
    {
        foreach (var network in networks.Where(n => n.PhysicalAddress is not null))
        {
            found.Report(new ScanResult(network.Address.ToString(), MacAddressHelper.Format(network.PhysicalAddress!),
                Environment.MachineName, isThisPc: true));
        }

        var hosts = networks.SelectMany(n => n.ScanHosts()).Distinct().ToList();
        var total = hosts.Count;
        var done = 0;
        progress.Report((0, total));

        using var gate = new SemaphoreSlim(MaxParallelism);
        var tasks = hosts.Select(async ip =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // SendARP bloquea su hilo; LongRunning evita agotar el pool de hilos.
                var mac = await Task.Factory.StartNew(() => NetworkHelper.GetMacFromArp(ip), cancellationToken,
                    TaskCreationOptions.LongRunning, TaskScheduler.Default).ConfigureAwait(false);
                if (mac is null)
                    return;

                var hostName = await NetworkHelper.ResolveHostNameAsync(ip, 1500).ConfigureAwait(false);
                found.Report(new ScanResult(ip.ToString(), mac, hostName ?? string.Empty, isThisPc: false));
            }
            finally
            {
                gate.Release();
                progress.Report((Interlocked.Increment(ref done), total));
            }
        }).ToList();

        await Task.WhenAll(tasks).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }
}
