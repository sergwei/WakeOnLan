using System.Windows;
using System.Windows.Input;
using WakeOnLan.Helpers;
using WakeOnLan.ViewModels;

namespace WakeOnLan.Views;

/// <summary>Panel flotante junto a la bandeja del sistema con el estado de los favoritos.</summary>
public partial class TrayFlyout : Window
{
    private const double ScreenMargin = 12;

    // Clic en el icono con el panel abierto: Windows lo desactiva (se oculta) justo antes del clic,
    // y sin esta ventana de tiempo el mismo clic lo volvería a abrir.
    private static readonly TimeSpan ReopenGuard = TimeSpan.FromMilliseconds(300);

    private readonly MainViewModel _viewModel;
    private DateTime _hiddenAt = DateTime.MinValue;

    public TrayFlyout(MainViewModel viewModel)
    {
        InitializeComponent();
        WindowEffects.ApplyFlyout(this);
        _viewModel = viewModel;
        DataContext = viewModel;

        Deactivated += (_, _) => HideFlyout();
        SizeChanged += (_, _) => PlaceNearTray();
    }

    public event EventHandler? OpenRequested;

    public event EventHandler? ExitRequested;

    public void Toggle()
    {
        if (IsVisible)
        {
            HideFlyout();
            return;
        }

        if (DateTime.UtcNow - _hiddenAt < ReopenGuard)
            return;

        Show();
        PlaceNearTray();
        Activate();
        _ = _viewModel.RefreshStatusAsync();
    }

    public void HideFlyout()
    {
        if (!IsVisible)
            return;

        Hide();
        _hiddenAt = DateTime.UtcNow;
    }

    /// <summary>Esquina del área de trabajo más cercana a la barra de tareas (abajo a la derecha por defecto).</summary>
    private void PlaceNearTray()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Left > 0 ? area.Left + ScreenMargin : area.Right - ActualWidth - ScreenMargin;
        Top = area.Top > 0 ? area.Top + ScreenMargin : area.Bottom - ActualHeight - ScreenMargin;
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        HideFlyout();
        OpenRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => ExitRequested?.Invoke(this, EventArgs.Empty);

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            HideFlyout();
    }
}
