using System.IO;
using System.Text.Json;
using WakeOnLan.Models;

namespace WakeOnLan.Services;

/// <summary>Persiste la lista de dispositivos en %AppData%\WakeOnLan\devices.json.</summary>
public sealed class DeviceStore
{
    public const long MaxFileBytes = 2 * 1024 * 1024;
    public const int MaxDevices = 1000;

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

    public static List<Device> Read(string path)
    {
        // Una lista real ocupa pocos KB; el límite evita que un archivo enorme agote la memoria.
        if (new FileInfo(path).Length > MaxFileBytes)
            throw new InvalidDataException($"El archivo supera el tamaño máximo permitido ({MaxFileBytes / (1024 * 1024)} MB).");

        var devices = JsonSerializer.Deserialize<List<Device>>(File.ReadAllText(path), JsonOptions) ?? new List<Device>();
        if (devices.Count > MaxDevices)
            throw new InvalidDataException($"El archivo contiene más de {MaxDevices} dispositivos.");

        return devices;
    }

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
