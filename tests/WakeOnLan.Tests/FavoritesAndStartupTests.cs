using WakeOnLan.Models;
using WakeOnLan.Services;

namespace WakeOnLan.Tests;

public sealed class FavoritesTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), "WakeOnLan.Tests", Guid.NewGuid().ToString("N") + ".json");

    public void Dispose()
    {
        if (File.Exists(_file))
            File.Delete(_file);
    }

    [Fact]
    public void IsFavorite_IsPersisted()
    {
        DeviceStore.Write(_file, new[]
        {
            new Device { Name = "Favorito", IsFavorite = true },
            new Device { Name = "Normal" },
        });

        var loaded = DeviceStore.Read(_file);

        Assert.True(loaded.Single(d => d.Name == "Favorito").IsFavorite);
        Assert.False(loaded.Single(d => d.Name == "Normal").IsFavorite);
    }

    [Fact]
    public void IsFavorite_DefaultsToFalseForOlderFiles()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        File.WriteAllText(_file, """[{"Name":"Antiguo","MacAddress":"AA:BB:CC:DD:EE:FF"}]""");

        Assert.False(Assert.Single(DeviceStore.Read(_file)).IsFavorite);
    }

    [Fact]
    public void IsFavorite_NotifiesChanges()
    {
        var device = new Device();
        string? changed = null;
        device.PropertyChanged += (_, e) => changed = e.PropertyName;

        device.IsFavorite = true;

        Assert.Equal(nameof(Device.IsFavorite), changed);
    }

    [Fact]
    public void CopyFrom_KeepsFavoriteFlag()
    {
        // El diálogo de edición no toca la estrella: editar un favorito no debe quitársela.
        var favorite = new Device { Name = "PC", IsFavorite = true };
        favorite.CopyFrom(new Device { Name = "PC renombrado" });

        Assert.True(favorite.IsFavorite);
    }

    [Fact]
    public void TrySanitizeImported_KeepsFavoriteFlag()
    {
        var device = new Device { MacAddress = "AABBCCDDEEFF", IsFavorite = true };

        Assert.True(DeviceValidator.TrySanitizeImported(device, out _));
        Assert.True(device.IsFavorite);
    }
}

public class StartupRegistrationTests
{
    [Fact]
    public void BuildCommand_QuotesPathAndStartsMinimized()
    {
        Assert.Equal("\"C:\\Program Files\\Wake on LAN\\WakeOnLan.exe\" /minimized",
            StartupRegistration.BuildCommand(@"C:\Program Files\Wake on LAN\WakeOnLan.exe"));
    }

    [Fact]
    public void MinimizedArgument_IsNotACommandLineAction()
    {
        // Debe abrir la app (en la bandeja), no ejecutarse como comando y salir.
        Assert.Null(CommandLine.TryRun(new[] { StartupRegistration.MinimizedArgument }));
    }
}
