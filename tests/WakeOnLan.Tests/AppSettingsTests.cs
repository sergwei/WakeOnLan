using WakeOnLan.Services;

namespace WakeOnLan.Tests;

public sealed class AppSettingsTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), "WakeOnLan.Tests", Guid.NewGuid().ToString("N"), "settings.json");

    public void Dispose()
    {
        var folder = Path.GetDirectoryName(_file)!;
        if (Directory.Exists(folder))
            Directory.Delete(folder, recursive: true);
    }

    [Fact]
    public void CloseToTray_IsEnabledByDefault()
    {
        Assert.True(new AppSettings().CloseToTray);
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        Assert.True(AppSettings.Load(_file).CloseToTray);
    }

    [Fact]
    public void SaveAndLoad_RoundTrips()
    {
        new AppSettings { CloseToTray = false }.Save(_file);

        Assert.False(AppSettings.Load(_file).CloseToTray);
        Assert.False(File.Exists(_file + ".tmp"));
    }

    [Fact]
    public void Load_CorruptFile_ReturnsDefaults()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        File.WriteAllText(_file, "{ esto no es json");

        Assert.True(AppSettings.Load(_file).CloseToTray);
    }

    [Fact]
    public void Load_OversizedFile_ReturnsDefaults()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        File.WriteAllText(_file, """{"CloseToTray":false}""" + new string(' ', 70 * 1024));

        Assert.True(AppSettings.Load(_file).CloseToTray);
    }
}
