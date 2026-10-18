using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Threading;
using WakeOnLan.Helpers;
using WakeOnLan.Models;
using WakeOnLan.Services;

namespace WakeOnLan.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private static readonly TimeSpan WakeGracePeriod = TimeSpan.FromMinutes(3);
    private const int PingTimeoutMs = 1500;

    private readonly DeviceStore _store = new();
    private readonly AppSettings _settings = AppSettings.Load(AppSettings.DefaultPath);
    private readonly DispatcherTimer _statusTimer = new() { Interval = TimeSpan.FromSeconds(10) };
    private readonly DispatcherTimer _toastTimer = new();
    private string _searchText = string.Empty;
    private string _toastMessage = string.Empty;
    private bool _isToastVisible;
    private bool _isToastError;
    private bool _isRefreshing;

    public MainViewModel()
    {
        foreach (var device in _store.Load())
            Devices.Add(device);

        // Lista principal: favoritos primero y después por nombre, reordenando en vivo al marcar la estrella.
        DevicesView = CollectionViewSource.GetDefaultView(Devices);
        DevicesView.Filter = Matches;
        DevicesView.SortDescriptions.Add(new SortDescription(nameof(Device.IsFavorite), ListSortDirection.Descending));
        DevicesView.SortDescriptions.Add(new SortDescription(nameof(Device.Name), ListSortDirection.Ascending));
        if (DevicesView is ICollectionViewLiveShaping liveDevices)
        {
            liveDevices.IsLiveSorting = true;
            liveDevices.LiveSortingProperties.Add(nameof(Device.IsFavorite));
            liveDevices.LiveSortingProperties.Add(nameof(Device.Name));
        }

        // Vista independiente para el panel de la bandeja (la vista por defecto es la de la lista principal).
        Favorites = new ListCollectionView(Devices)
        {
            Filter = item => item is Device { IsFavorite: true },
            IsLiveFiltering = true,
            IsLiveSorting = true,
        };
        Favorites.LiveFilteringProperties.Add(nameof(Device.IsFavorite));
        Favorites.SortDescriptions.Add(new SortDescription(nameof(Device.Name), ListSortDirection.Ascending));
        Favorites.LiveSortingProperties.Add(nameof(Device.Name));

        Devices.CollectionChanged += (_, _) => RaiseSummary();

        WakeCommand = new RelayCommand(async parameter =>
        {
            if (parameter is Device device)
                await WakeAsync(new[] { device });
        });
        RefreshCommand = new RelayCommand(async _ => await RefreshStatusAsync());
        ToggleFavoriteCommand = new RelayCommand(parameter =>
        {
            if (parameter is Device device)
                ToggleFavorite(device);
        });

        _statusTimer.Tick += async (_, _) => await RefreshStatusAsync();
        _toastTimer.Tick += (_, _) => HideToast();
    }

    public ObservableCollection<Device> Devices { get; } = new();

    public ICollectionView DevicesView { get; }

    public ListCollectionView Favorites { get; }

    public RelayCommand WakeCommand { get; }

    public RelayCommand RefreshCommand { get; }

    public RelayCommand ToggleFavoriteCommand { get; }

    /// <summary>Se lanza cuando un equipo que se estaba encendiendo responde por primera vez.</summary>
    public event EventHandler<Device>? DeviceCameOnline;

    public bool HasFavorites => Devices.Any(d => d.IsFavorite);

    public string FavoritesSummary
    {
        get
        {
            var favorites = Devices.Where(d => d.IsFavorite).ToList();
            var online = favorites.Count(d => d.Status == DeviceStatus.Online);
            return favorites.Count switch
            {
                0 => "Sin favoritos",
                1 => online == 1 ? "1 favorito · en línea" : "1 favorito · apagado",
                _ => $"{online} de {favorites.Count} favoritos en línea",
            };
        }
    }

    public bool RunAtStartup
    {
        get => StartupRegistration.IsEnabled;
        set
        {
            try
            {
                if (value)
                    StartupRegistration.Enable();
                else
                    StartupRegistration.Disable();
                ShowToast(value ? "Wake on LAN se iniciará con Windows, minimizado en la bandeja." : "Ya no se iniciará con Windows.");
            }
            catch (Exception ex)
            {
                ShowToast($"No se pudo cambiar el inicio automático: {ex.Message}", isError: true);
            }
            OnPropertyChanged();
        }
    }

    public bool CloseToTray
    {
        get => _settings.CloseToTray;
        set
        {
            _settings.CloseToTray = value;
            try
            {
                _settings.Save(AppSettings.DefaultPath);
                ShowToast(value ? "Al cerrar, Wake on LAN seguirá en la bandeja." : "Al cerrar, Wake on LAN se cerrará del todo.");
            }
            catch (Exception ex)
            {
                ShowToast($"No se pudo guardar la preferencia: {ex.Message}", isError: true);
            }
            OnPropertyChanged();
        }
    }

    public string DataFolder => _store.DataFolder;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetProperty(ref _searchText, value ?? string.Empty))
                return;
            DevicesView.Refresh();
            OnPropertyChanged(nameof(HasNoResults));
        }
    }

    public bool HasDevices => Devices.Count > 0;

    public bool HasNoResults => Devices.Count > 0 && DevicesView.IsEmpty;

    public string SummaryText
    {
        get
        {
            var online = Devices.Count(d => d.Status == DeviceStatus.Online);
            return Devices.Count switch
            {
                0 => "Añade los equipos que quieras encender a distancia",
                1 => $"1 dispositivo · {online} en línea",
                _ => $"{Devices.Count} dispositivos · {online} en línea",
            };
        }
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        private set => SetProperty(ref _isRefreshing, value);
    }

    public string ToastMessage
    {
        get => _toastMessage;
        private set => SetProperty(ref _toastMessage, value);
    }

    public bool IsToastVisible
    {
        get => _isToastVisible;
        private set => SetProperty(ref _isToastVisible, value);
    }

    public bool IsToastError
    {
        get => _isToastError;
        private set => SetProperty(ref _isToastError, value);
    }

    public void Start()
    {
        _statusTimer.Start();
        _ = RefreshStatusAsync();
    }

    public void Shutdown()
    {
        _statusTimer.Stop();
        Save();
    }

    public async Task WakeAsync(IReadOnlyList<Device> devices)
    {
        if (devices.Count == 0)
        {
            ShowToast("Selecciona al menos un dispositivo.", isError: true);
            return;
        }

        var errors = new List<string>();
        foreach (var device in devices)
        {
            try
            {
                await WakeOnLanService.WakeAsync(device);
                device.LastWake = DateTime.Now;
                if (device.Status != DeviceStatus.Online)
                    device.Status = DeviceStatus.Waking;
            }
            catch (Exception ex)
            {
                errors.Add($"{device.Name}: {ex.Message}");
            }
        }

        Save();

        if (errors.Count > 0)
            ShowToast(string.Join("\n", errors), isError: true);
        else if (devices.Count == 1)
            ShowToast($"Paquete mágico enviado a «{devices[0].Name}».");
        else
            ShowToast($"Paquete mágico enviado a {devices.Count} dispositivos.");
    }

    public async Task RefreshStatusAsync()
    {
        if (IsRefreshing)
            return;

        IsRefreshing = true;
        try
        {
            var checks = Devices.ToList().Select(async device =>
            {
                var stillWaking = device.Status == DeviceStatus.Waking &&
                                  device.LastWake is { } wakeTime &&
                                  DateTime.Now - wakeTime < WakeGracePeriod;

                if (string.IsNullOrWhiteSpace(device.IpAddress))
                {
                    device.Status = stillWaking ? DeviceStatus.Waking : DeviceStatus.Unknown;
                    return;
                }

                if (await NetworkHelper.PingAsync(device.IpAddress, PingTimeoutMs))
                {
                    var justWokeUp = device.Status == DeviceStatus.Waking;
                    device.Status = DeviceStatus.Online;
                    device.LastSeen = DateTime.Now;
                    if (justWokeUp)
                        DeviceCameOnline?.Invoke(this, device);
                }
                else if (!stillWaking)
                {
                    device.Status = DeviceStatus.Offline;
                }
            });

            await Task.WhenAll(checks);

            foreach (var device in Devices)
                device.RefreshRelativeTimes();
        }
        finally
        {
            IsRefreshing = false;
            RaiseSummary();
        }
    }

    public void AddDevices(IReadOnlyList<Device> devices)
    {
        foreach (var device in devices)
            Devices.Add(device);

        Save();
        ShowToast(devices.Count == 1 ? $"«{devices[0].Name}» añadido." : $"{devices.Count} dispositivos añadidos.");
        _ = RefreshStatusAsync();
    }

    public void UpdateDevice(Device target, Device changes)
    {
        var ipChanged = !string.Equals(target.IpAddress, changes.IpAddress, StringComparison.OrdinalIgnoreCase);
        target.CopyFrom(changes);
        if (ipChanged)
            target.Status = DeviceStatus.Unknown;

        Save();
        DevicesView.Refresh();
        ShowToast($"«{target.Name}» actualizado.");

        if (ipChanged)
            _ = RefreshStatusAsync();
    }

    public void ToggleFavorite(Device device)
    {
        device.IsFavorite = !device.IsFavorite;
        Save();
        RaiseSummary();
    }

    public void RemoveDevices(IReadOnlyList<Device> devices)
    {
        foreach (var device in devices)
            Devices.Remove(device);

        Save();
        ShowToast(devices.Count == 1 ? $"«{devices[0].Name}» eliminado." : $"{devices.Count} dispositivos eliminados.");
    }

    public void Import(string path)
    {
        try
        {
            var known = Devices.Select(d => MacAddressHelper.Normalize(d.MacAddress))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var added = 0;
            var withRemovedAddress = 0;

            foreach (var device in DeviceStore.Read(path))
            {
                if (!DeviceValidator.TrySanitizeImported(device, out var removedAddress))
                    continue;
                if (!known.Add(device.MacAddress))
                    continue; // Ya existe: no se duplica.

                Devices.Add(device);
                added++;
                if (removedAddress)
                    withRemovedAddress++;
            }

            Save();
            var message = added == 0 ? "No había dispositivos nuevos que importar." : $"Se importaron {added} dispositivos.";
            if (withRemovedAddress > 0)
            {
                message += $" Por seguridad se quitaron las direcciones fuera de tu red local en {withRemovedAddress} de ellos; " +
                           "revísalos y añádelas a mano si son de confianza.";
            }
            ShowToast(message, isError: withRemovedAddress > 0);
            _ = RefreshStatusAsync();
        }
        catch (Exception ex)
        {
            ShowToast($"No se pudo importar el archivo: {ex.Message}", isError: true);
        }
    }

    public void Export(string path)
    {
        try
        {
            DeviceStore.Write(path, Devices);
            ShowToast($"Se exportaron {Devices.Count} dispositivos.");
        }
        catch (Exception ex)
        {
            ShowToast($"No se pudo exportar: {ex.Message}", isError: true);
        }
    }

    public void ShowToast(string message, bool isError = false)
    {
        ToastMessage = message;
        IsToastError = isError;
        IsToastVisible = true;
        _toastTimer.Stop();
        _toastTimer.Interval = TimeSpan.FromSeconds(isError ? 7 : 4);
        _toastTimer.Start();
    }

    public void HideToast()
    {
        _toastTimer.Stop();
        IsToastVisible = false;
    }

    private void Save()
    {
        try
        {
            _store.Save(Devices);
        }
        catch (Exception ex)
        {
            ShowToast($"No se pudo guardar la lista: {ex.Message}", isError: true);
        }
    }

    private bool Matches(object item)
    {
        if (item is not Device device || string.IsNullOrWhiteSpace(SearchText))
            return true;

        var query = SearchText.Trim();
        return device.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               device.IpAddress.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               device.MacAddress.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void RaiseSummary()
    {
        OnPropertyChanged(nameof(HasDevices));
        OnPropertyChanged(nameof(HasNoResults));
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(HasFavorites));
        OnPropertyChanged(nameof(FavoritesSummary));
    }
}
