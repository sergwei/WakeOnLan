using System.IO;
using System.Text.Json;
using WakeOnLan.Models;

namespace WakeOnLan.Services;

/// <summary>Persiste la lista de dispositivos en %AppData%\WakeOnLan\devices.json.</summary>
public sealed class DeviceStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public DeviceStore()
    {
        DataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WakeOnLan");
        FilePath = Path.Combine(DataFolder, "devices.json");
    }

    public string DataFolder { get; }

    public string FilePath { get; }

    public List<Device> Load()
    {
        if (!File.Exists(FilePath))
            return new List<Device>();

        try
        {
            return Read(FilePath);
        }
        catch (JsonException)
        {
            // Archivo dañado: se aparta una copia para no perderlo al guardar encima.
            File.Copy(FilePath, $"{FilePath}.{DateTime.Now:yyyyMMddHHmmss}.bak", overwrite: true);
            return new List<Device>();
        }
    }

    public void Save(IEnumerable<Device> devices) => Write(FilePath, devices);

    public static List<Device> Read(string path) =>
        JsonSerializer.Deserialize<List<Device>>(File.ReadAllText(path), JsonOptions) ?? new List<Device>();

    public static void Write(string path, IEnumerable<Device> devices)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        // Escritura atómica: si se corta a mitad, el archivo original sigue intacto.
        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(devices.ToList(), JsonOptions));
        File.Move(tempPath, path, overwrite: true);
    }
}
