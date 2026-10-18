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

    public MainWindow()
    {
        InitializeComponent();
        WindowEffects.Apply(this);
        DataContext = _viewModel;
        Loaded += (_, _) => _viewModel.Start();
        Closing += (_, _) => _viewModel.Shutdown();
    }

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
