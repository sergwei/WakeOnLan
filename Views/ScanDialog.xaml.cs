using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using WakeOnLan.Helpers;
using WakeOnLan.Models;
using WakeOnLan.Services;

namespace WakeOnLan.Views;

public partial class ScanDialog : Window
{
    private readonly HashSet<string> _knownMacs;
    private readonly ObservableCollection<ScanResult> _results = new();
    private CancellationTokenSource? _cancellation;

    public ScanDialog(IEnumerable<Device> existing)
    {
        InitializeComponent();
        WindowEffects.Apply(this);

        _knownMacs = existing.Select(d => MacAddressHelper.Normalize(d.MacAddress))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        ResultsList.ItemsSource = _results;

        Loaded += async (_, _) => await ScanAsync();
        Closing += (_, _) => _cancellation?.Cancel();
    }

    public IReadOnlyList<Device> SelectedDevices { get; private set; } = Array.Empty<Device>();

    private async Task ScanAsync()
    {
        _cancellation?.Cancel();
        _cancellation = new CancellationTokenSource();
        var token = _cancellation.Token;

        _results.Clear();
        UpdateSelectionState();

        var networks = NetworkHelper.GetLocalNetworks();
        if (networks.Count == 0)
        {
            NetworksText.Text = "No hay ninguna red local activa.";
            StatusText.Text = "Conéctate a una red e inténtalo de nuevo.";
            RescanButton.IsEnabled = true;
            return;
        }

        NetworksText.Text = "Redes: " + string.Join(", ", networks.Select(n => $"{n.ScanRange} ({n.InterfaceName})"));
        RescanButton.IsEnabled = false;
        ScanProgress.Value = 0;
        StatusText.Text = "Escaneando…";

        var found = new Progress<ScanResult>(AddResult);
        var progress = new Progress<(int Done, int Total)>(p =>
        {
            if (token.IsCancellationRequested)
                return;
            ScanProgress.Maximum = Math.Max(1, p.Total);
            ScanProgress.Value = p.Done;
            StatusText.Text = $"Escaneando… {p.Done} de {p.Total} direcciones";
        });

        try
        {
            await NetworkScanner.ScanAsync(networks, found, progress, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        // Progress<T> publica de forma asíncrona: se vacía la cola antes de escribir el resumen final.
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);

        RescanButton.IsEnabled = true;
        ScanProgress.Value = ScanProgress.Maximum;
        StatusText.Text = _results.Count == 0
            ? "No se encontraron dispositivos."
            : $"Escaneo completado: {_results.Count} dispositivos encontrados.";
    }

    private void AddResult(ScanResult result)
    {
        if (_results.Any(r => r.IpAddress == result.IpAddress))
            return;

        result.AlreadyAdded = _knownMacs.Contains(result.MacAddress);
        result.PropertyChanged += (_, _) => UpdateSelectionState();

        var index = 0;
        while (index < _results.Count && _results[index].SortKey < result.SortKey)
            index++;
        _results.Insert(index, result);

        UpdateSelectionState();
    }

    private void UpdateSelectionState()
    {
        var selectable = _results.Where(r => r.CanSelect).ToList();
        var selected = selectable.Count(r => r.IsSelected);

        AddButton.IsEnabled = selected > 0;
        AddButtonText.Text = selected > 0 ? $"Añadir ({selected})" : "Añadir";
        SelectAllBox.IsEnabled = selectable.Count > 0;
        SelectAllBox.IsChecked = selectable.Count > 0 && selected == selectable.Count;
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        var check = SelectAllBox.IsChecked == true;
        foreach (var result in _results.Where(r => r.CanSelect))
            result.IsSelected = check;
    }

    private async void Rescan_Click(object sender, RoutedEventArgs e) => await ScanAsync();

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        SelectedDevices = _results.Where(r => r.IsSelected && r.CanSelect).Select(r => r.ToDevice()).ToList();
        DialogResult = true;
    }
}
