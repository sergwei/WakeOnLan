using System.IO;
using System.Text.Json;

namespace WakeOnLan.Services;

/// <summary>Preferencias de la aplicación, en %AppData%\WakeOnLan\settings.json.</summary>
public sealed class AppSettings
{
    private const long MaxFileBytes = 64 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Al cerrar la ventana (✕), la app sigue en la bandeja en vez de terminar.</summary>
    public bool CloseToTray { get; set; } = true;

    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WakeOnLan", "settings.json");

    /// <summary>Si el archivo no existe o no se puede leer, se usan los valores por defecto.</summary>
    public static AppSettings Load(string path)
    {
        try
        {
            if (File.Exists(path) && new FileInfo(path).Length <= MaxFileBytes)
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOptions) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
        }
        return new AppSettings();
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, JsonOptions));
        File.Move(temp, path, overwrite: true);
    }
}
