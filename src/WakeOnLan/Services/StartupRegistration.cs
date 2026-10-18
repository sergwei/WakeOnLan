using Microsoft.Win32;

namespace WakeOnLan.Services;

/// <summary>Arranque automático con Windows (clave Run del usuario, sin permisos de administrador).</summary>
public static class StartupRegistration
{
    public const string MinimizedArgument = "/minimized";

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "WakeOnLan";

    public static string BuildCommand(string executablePath) => $"\"{executablePath}\" {MinimizedArgument}";

    /// <summary>
    /// Solo cuenta como activo si apunta a este ejecutable: si se movió el portable, la entrada antigua no sirve.
    /// </summary>
    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string command &&
                   Environment.ProcessPath is { } path &&
                   string.Equals(command, BuildCommand(path), StringComparison.OrdinalIgnoreCase);
        }
    }

    public static void Enable()
    {
        var path = Environment.ProcessPath ?? throw new InvalidOperationException("No se pudo determinar la ruta del ejecutable.");
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        key.SetValue(ValueName, BuildCommand(path));
    }

    public static void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
