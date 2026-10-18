using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;

namespace WakeOnLan.Helpers;

/// <summary>
/// Aplica el aspecto de Windows 11: fondo Mica, barra de título oscura y botones de sistema nativos.
/// En versiones anteriores se queda con la ventana estándar y un fondo sólido.
/// </summary>
internal static class WindowEffects
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwcpRound = 2;
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmsbtMica = 2;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    // DWMWA_SYSTEMBACKDROP_TYPE existe desde Windows 11 22H2.
    public static bool SupportsMica => Environment.OSVersion.Version.Build >= 22621;

    public static void Apply(Window window)
    {
        if (SupportsMica)
        {
            WindowChrome.SetWindowChrome(window, new WindowChrome
            {
                CaptionHeight = 40,
                GlassFrameThickness = new Thickness(-1),
                ResizeBorderThickness = new Thickness(6),
                CornerRadius = new CornerRadius(0),
                UseAeroCaptionButtons = true,
            });
            window.Background = Brushes.Transparent;
            window.StateChanged += (_, _) => AdjustForMaximized(window);
        }
        else if (window.FindName("TitleBar") is UIElement titleBar)
        {
            // Sin Mica se usa la barra de título del sistema, así que la propia sobra.
            titleBar.Visibility = Visibility.Collapsed;
        }

        window.SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            var darkMode = 1;
            DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref darkMode, sizeof(int));

            if (!SupportsMica)
                return;

            if (HwndSource.FromHwnd(hwnd) is { CompositionTarget: { } target })
                target.BackgroundColor = Colors.Transparent;

            var backdrop = DwmsbtMica;
            DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref backdrop, sizeof(int));
        };
    }

    /// <summary>Ventana flotante sin bordes (panel de la bandeja): tema oscuro y esquinas redondeadas de Windows 11.</summary>
    public static void ApplyFlyout(Window window)
    {
        window.SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            var darkMode = 1;
            DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref darkMode, sizeof(int));
            var corners = DwmwcpRound;
            DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref corners, sizeof(int));
        };
    }

    // Con WindowChrome, al maximizar la ventana se sale unos píxeles de la pantalla.
    private static void AdjustForMaximized(Window window)
    {
        if (window.Content is FrameworkElement root)
            root.Margin = window.WindowState == WindowState.Maximized ? new Thickness(8) : new Thickness(0);
    }
}
