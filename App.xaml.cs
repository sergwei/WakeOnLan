using System.Windows;
using System.Windows.Threading;
using WakeOnLan.Views;

namespace WakeOnLan;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandledException;

        // Modo línea de comandos: enciende y sale sin abrir la ventana.
        if (e.Args.Length > 0 && CommandLine.TryRun(e.Args) is { } exitCode)
        {
            Shutdown(exitCode);
            return;
        }

        new MainWindow().Show();
    }

    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.Exception.Message, "Wake on LAN", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
