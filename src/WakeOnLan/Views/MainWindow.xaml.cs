using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using WakeOnLan.Helpers;
using WakeOnLan.Models;
using WakeOnLan.ViewModels;

namespace WakeOnLan.Views;

public partial class MainWindow : Window
{
    private const string FileFilter = "Lista de dispositivos (*.json)|*.json|Todos los archivos (*.*)|*.*";

    private readonly MainViewModel _viewModel = new();
    private readonly TrayIcon _tray = new();
    private readonly TrayFlyout _flyout;
    private WindowState _restoreState = WindowState.Normal;
    private bool _trayHintShown;
    private bool _exiting;

    public MainWindow()
    {
        InitializeComponent();
        WindowEffects.Apply(this);
        DataContext = _viewModel;

        _flyout = new TrayFlyout(_viewModel);
        _flyout.OpenRequested += (_, _) => RestoreFromTray();
        _flyout.ExitRequested += (_, _) =>
        {
            _exiting = true;
            Application.Current.Shutdown();
        };
        _tray.Clicked += (_, _) => _flyout.Toggle();

        _viewModel.DeviceCameOnline += (_, device) =>
            _tray.ShowNotification($"«{device.Name}» está en línea", "El equipo ha arrancado correctamente.");
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.FavoritesSummary))
                UpdateTrayText();
        };

        StateChanged += OnStateChanged;
        Closing += OnClosing;
        // Al apagar Windows o cerrar sesión (o si el instalador cierra la app para actualizarla) hay que salir de verdad.
        Application.Current.SessionEnding += (_, _) => _exiting = true;

        // La app puede arrancar directamente en la bandeja (sin mostrar nunca la ventana), así que
        // la monitorización empieza aquí y la limpieza se hace al salir de la aplicación, no al cerrar la ventana.
        Application.Current.Exit += (_, _) =>
        {
            _tray.Dispose();
            _viewModel.Shutdown();
        };
        _viewModel.Start();
        UpdateTrayText();
    }

    // ----- Bandeja del sistema -----

    /// <summary>Muestra la ventana principal desde la bandeja (o desde una segunda instancia de la app).</summary>
    public void RestoreFromTray()
    {
        _flyout.HideFlyout();
        Show();
        if (WindowState == WindowState.Minimized)
            WindowState = _restoreState;
        Activate();
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState != WindowState.Minimized)
        {
            _restoreState = WindowState;
            return;
        }

        // Minimizar = ir a la bandeja: la app sigue vigilando el estado de los equipos.
        HideToTray();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // La ✕ también deja la app en la bandeja (si así está configurado); para salir del todo, "Salir" en la bandeja.
        if (_exiting || !_viewModel.CloseToTray)
            return;

        e.Cancel = true;
        HideToTray();
    }

    private void HideToTray()
    {
        Hide();
        if (_trayHintShown)
            return;

        _trayHintShown = true;
        _tray.ShowNotification("Wake on LAN sigue activo",
            "Haz clic en el icono de la bandeja para ver y encender tus favoritos, o en Salir para cerrarla.");
    }

    private void UpdateTrayText() => _tray.Text = $"Wake on LAN · {_viewModel.FavoritesSummary}";

    private List<Device> SelectedDevices => DeviceList.SelectedItems.Cast<Device>().ToList();

    // ----- Acciones principales -----

    private void Add_Click(object sender, RoutedEventArgs e) => AddDevice();

    private void Scan_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ScanDialog(_viewModel.Devices) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.SelectedDevices.Count > 0)
            _viewModel.AddDevices(dialog.SelectedDevices);
    }

    private async void WakeSelected_Click(object sender, RoutedEventArgs e) => await _viewModel.WakeAsync(SelectedDevices);

    private async void WakeAll_Click(object sender, RoutedEventArgs e) => await _viewModel.WakeAsync(_viewModel.Devices.ToList());

    private void More_Click(object sender, RoutedEventArgs e) => OpenMenu(sender, "MoreMenu");

    private void RowMore_Click(object sender, RoutedEventArgs e) => OpenMenu(sender, "DeviceMenu");

    private void CloseToast_Click(object sender, RoutedEventArgs e) => _viewModel.HideToast();

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Importar dispositivos", Filter = FileFilter };
        if (dialog.ShowDialog(this) == true)
            _viewModel.Import(dialog.FileName);
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Title = "Exportar dispositivos", Filter = FileFilter, FileName = "dispositivos-wol.json" };
        if (dialog.ShowDialog(this) == true)
            _viewModel.Export(dialog.FileName);
    }

    private void OpenDataFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_viewModel.DataFolder);
        // Se abre la carpeta por asociación del shell, sin lanzar un ejecutable por nombre.
        Process.Start(new ProcessStartInfo(_viewModel.DataFolder) { UseShellExecute = true });
    }

    // ----- Menú de cada dispositivo -----

    private async void MenuWake_Click(object sender, RoutedEventArgs e)
    {
        if (DeviceFrom(sender) is { } device)
            await _viewModel.WakeAsync(TargetsFor(device));
    }

    private void MenuFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (DeviceFrom(sender) is { } device)
            _viewModel.ToggleFavorite(device);
    }

    private void ToggleStartup_Click(object sender, RoutedEventArgs e) => _viewModel.RunAtStartup = !_viewModel.RunAtStartup;

    private void ToggleCloseToTray_Click(object sender, RoutedEventArgs e) => _viewModel.CloseToTray = !_viewModel.CloseToTray;

    private void MenuEdit_Click(object sender, RoutedEventArgs e)
    {
        if (DeviceFrom(sender) is { } device)
            EditDevice(device);
    }

    private void MenuCopyMac_Click(object sender, RoutedEventArgs e)
    {
        if (DeviceFrom(sender) is not { } device)
            return;

        try
        {
            Clipboard.SetText(device.MacAddress);
            _viewModel.ShowToast("Dirección MAC copiada al portapapeles.");
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            _viewModel.ShowToast("El portapapeles está ocupado. Inténtalo de nuevo.", isError: true);
        }
    }

    private void MenuDelete_Click(object sender, RoutedEventArgs e)
    {
        if (DeviceFrom(sender) is { } device)
            DeleteDevices(TargetsFor(device));
    }

    // ----- Lista -----

    private void DeviceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var count = DeviceList.SelectedItems.Count;
        WakeSelectedButton.IsEnabled = count > 0;
        WakeSelectedText.Text = count > 1 ? $"Encender ({count})" : "Encender";
    }

    private void DeviceList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source || IsInsideButton(source))
            return;

        if (ItemsControl.ContainerFromElement(DeviceList, source) is ListBoxItem { DataContext: Device device })
            EditDevice(device);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var ctrl = Keyboard.Modifiers == ModifierKeys.Control;

        if (ctrl && e.Key == Key.N)
        {
            AddDevice();
        }
        else if (ctrl && e.Key == Key.F)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
        }
        else if (e.Key == Key.F5)
        {
            _viewModel.RefreshCommand.Execute(null);
        }
        else if (e.Key == Key.Delete && Keyboard.FocusedElement is not TextBox && DeviceList.SelectedItems.Count > 0)
        {
            DeleteDevices(SelectedDevices);
        }
        else
        {
            return;
        }

        e.Handled = true;
    }

    // ----- Utilidades -----

    private void AddDevice()
    {
        var dialog = new DeviceDialog(null, _viewModel.Devices) { Owner = this };
        if (dialog.ShowDialog() == true)
            _viewModel.AddDevices(new[] { dialog.Result });
    }

    private void EditDevice(Device device)
    {
        var dialog = new DeviceDialog(device, _viewModel.Devices) { Owner = this };
        if (dialog.ShowDialog() == true)
            _viewModel.UpdateDevice(device, dialog.Result);
    }

    private void DeleteDevices(IReadOnlyList<Device> devices)
    {
        if (devices.Count == 0)
            return;

        var single = devices.Count == 1;
        var title = single ? "¿Eliminar dispositivo?" : "¿Eliminar dispositivos?";
        var message = single
            ? $"Se quitará «{devices[0].Name}» de la lista. Esta acción no se puede deshacer."
            : $"Se quitarán {devices.Count} dispositivos de la lista. Esta acción no se puede deshacer.";

        if (ConfirmDialog.Ask(this, title, message, "Eliminar"))
            _viewModel.RemoveDevices(devices);
    }

    /// <summary>Si el dispositivo forma parte de una selección múltiple, la acción se aplica a toda la selección.</summary>
    private List<Device> TargetsFor(Device device) =>
        DeviceList.SelectedItems.Count > 1 && DeviceList.SelectedItems.Contains(device)
            ? SelectedDevices
            : new List<Device> { device };

    private void OpenMenu(object sender, string resourceKey)
    {
        if (sender is not FrameworkElement target || FindResource(resourceKey) is not ContextMenu menu)
            return;

        menu.PlacementTarget = target;
        menu.Placement = PlacementMode.Bottom;
        menu.DataContext = target.DataContext;
        menu.IsOpen = true;
    }

    private static Device? DeviceFrom(object sender) => (sender as FrameworkElement)?.DataContext as Device;

    private static bool IsInsideButton(DependencyObject? element)
    {
        while (element is not null and not ListBoxItem)
        {
            if (element is ButtonBase)
                return true;
            element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
        }
        return false;
    }
}
