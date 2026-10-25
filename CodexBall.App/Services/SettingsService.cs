using System.IO;
using System.Text.Json;

namespace CodexBall.App.Services;

public sealed class SettingsService
{
    private readonly string _settingsPath;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    public SettingsService(string? directory = null)
    {
        directory ??= OperatingSystem.IsMacOS()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "CodexBall")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexBall");
        Directory.CreateDirectory(directory);
        _settingsPath = Path.Combine(directory, "settings.json");
    }

    public async Task<AppSettings> LoadAsync()
    {
        if (!File.Exists(_settingsPath))
        {
            return new AppSettings();
        }

        try
        {
            await using var stream = File.OpenRead(_settingsPath);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream) ?? new AppSettings();
            settings.Opacity = double.IsFinite(settings.Opacity) ? Math.Clamp(settings.Opacity, .2, 1) : 1;
            if (settings.Left is { } left && !double.IsFinite(left)) settings.Left = null;
            if (settings.Top is { } top && !double.IsFinite(top)) settings.Top = null;
            return settings;
        }
        catch (JsonException) { return new AppSettings(); }
    }

    public async Task SaveAsync(AppSettings settings)
    {
        var snapshot = JsonSerializer.SerializeToUtf8Bytes(settings, _jsonOptions);
        await _saveLock.WaitAsync();
        try
        {
            var temporaryPath = $"{_settingsPath}.tmp";
            await using (var stream = File.Create(temporaryPath))
            {
                await stream.WriteAsync(snapshot);
            }

            File.Move(temporaryPath, _settingsPath, overwrite: true);
        }
        finally { _saveLock.Release(); }
    }
}
