using System.Windows;
using System.Windows.Threading;
using WakeOnLan.Services;
using WakeOnLan.Views;

namespace WakeOnLan;

public partial class App : Application
{
    // "Local\" = por sesión de usuario: cada usuario de Windows tiene su propia instancia.
    private const string InstanceName = @"Local\WakeOnLan.SingleInstance";

    private Mutex? _instanceMutex;
    private bool _ownsMutex;
    private EventWaitHandle? _showSignal;
    private RegisteredWaitHandle? _showWait;

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

        // Una sola instancia: si ya hay una (quizá en la bandeja), se le pide que se muestre y esta sale.
        _instanceMutex = new Mutex(initiallyOwned: true, InstanceName, out _ownsMutex);
        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, InstanceName + ".Show");
        if (!_ownsMutex)
        {
            _showSignal.Set();
            Shutdown();
            return;
        }

        var window = new MainWindow();
        _showWait = ThreadPool.RegisterWaitForSingleObject(_showSignal,
            (_, _) => Dispatcher.BeginInvoke(new Action(window.RestoreFromTray)),
            null, Timeout.Infinite, executeOnlyOnce: false);

        var startInTray = e.Args.Any(a => string.Equals(a, StartupRegistration.MinimizedArgument, StringComparison.OrdinalIgnoreCase));
        if (!startInTray)
            window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _showWait?.Unregister(null);
        _showSignal?.Dispose();
        if (_instanceMutex is not null)
        {
            if (_ownsMutex)
                _instanceMutex.ReleaseMutex();
            _instanceMutex.Dispose();
        }
        base.OnExit(e);
    }

    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.Exception.Message, "Wake on LAN", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
