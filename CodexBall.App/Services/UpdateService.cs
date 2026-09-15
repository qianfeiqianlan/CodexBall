using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodexBall.App.Services;

public sealed class UpdateService
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/qianfeiqianlan/CodexBall/releases/latest";
    private const string ProductName = "CodexBall";
    private static readonly HttpClient HttpClient = CreateHttpClient();

    private ReleaseInfo? _latestRelease;

    public event EventHandler? StateChanged;

    public string CurrentVersionText { get; } = GetCurrentVersionText();

    public string? LatestVersionText { get; private set; }

    public bool IsUpgradeAvailable { get; private set; }

    public bool IsUpgradeInProgress { get; private set; }

    public async Task CheckForUpdatesAsync()
    {
        try
        {
            var release = await HttpClient.GetFromJsonAsync<ReleaseInfo>(
                LatestReleaseUrl,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));

            if (release?.TagName is null)
            {
                return;
            }

            _latestRelease = release;
            LatestVersionText = NormalizeVersionText(release.TagName);
            IsUpgradeAvailable = IsNewerVersion(CurrentVersionText, LatestVersionText);
            OnStateChanged();
        }
        catch
        {
            IsUpgradeAvailable = false;
            OnStateChanged();
        }
    }

    public async Task StartUpgradeAsync()
    {
        if (_latestRelease is null || IsUpgradeInProgress)
        {
            return;
        }

        var asset = SelectDownloadAsset(_latestRelease);
        if (asset?.DownloadUrl is null)
        {
            throw new InvalidOperationException("No downloadable CodexBall release asset was found.");
        }

        IsUpgradeInProgress = true;
        OnStateChanged();

        var tempDirectory = Path.Combine(Path.GetTempPath(), ProductName, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        var assetPath = Path.Combine(tempDirectory, asset.Name);
        await using (var assetStream = await HttpClient.GetStreamAsync(asset.DownloadUrl))
        await using (var fileStream = File.Create(assetPath))
        {
            await assetStream.CopyToAsync(fileStream);
        }

        var updaterPath = Path.Combine(tempDirectory, "update.ps1");
        await File.WriteAllTextAsync(updaterPath, BuildUpdaterScript(tempDirectory, assetPath, asset.Name));

        Process.Start(new ProcessStartInfo("powershell.exe")
        {
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{updaterPath}\"",
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("CodexBall");
        return client;
    }

    private static ReleaseAsset? SelectDownloadAsset(ReleaseInfo release)
    {
        var assets = release.Assets ?? [];
        return assets.FirstOrDefault(asset =>
                asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                && asset.Name.Contains(ProductName, StringComparison.OrdinalIgnoreCase))
            ?? assets.FirstOrDefault(asset =>
                asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                && asset.Name.Contains(ProductName, StringComparison.OrdinalIgnoreCase))
            ?? assets.FirstOrDefault(asset => asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            ?? assets.FirstOrDefault(asset => asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildUpdaterScript(string tempDirectory, string assetPath, string assetName)
    {
        var processId = Environment.ProcessId;
        var processPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Cannot locate the running executable.");
        var installDirectory = AppContext.BaseDirectory;
        var isZip = assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? "$true" : "$false";

        return $$"""
            $ErrorActionPreference = 'Stop'
            $processId = {{processId}}
            $tempDirectory = '{{EscapePowerShell(tempDirectory)}}'
            $assetPath = '{{EscapePowerShell(assetPath)}}'
            $installDirectory = '{{EscapePowerShell(installDirectory)}}'
            $processPath = '{{EscapePowerShell(processPath)}}'
            $isZip = {{isZip}}
            $extractDirectory = Join-Path $tempDirectory 'extracted'

            Wait-Process -Id $processId -Timeout 60 -ErrorAction SilentlyContinue
            Start-Sleep -Milliseconds 500

            if ($isZip) {
                if (Test-Path -LiteralPath $extractDirectory) {
                    Remove-Item -LiteralPath $extractDirectory -Recurse -Force
                }

                Expand-Archive -LiteralPath $assetPath -DestinationPath $extractDirectory -Force
                $sourceExe = Get-ChildItem -LiteralPath $extractDirectory -Recurse -Filter 'CodexBall.exe' | Select-Object -First 1
                if ($null -eq $sourceExe) {
                    throw 'CodexBall.exe was not found in the downloaded release archive.'
                }

                $sourceDirectory = $sourceExe.DirectoryName
                Get-ChildItem -LiteralPath $sourceDirectory -Force | ForEach-Object {
                    Copy-Item -LiteralPath $_.FullName -Destination $installDirectory -Recurse -Force
                }
            } else {
                Copy-Item -LiteralPath $assetPath -Destination $processPath -Force
            }

            Start-Process -FilePath $processPath
            """;
    }

    private static string EscapePowerShell(string value)
        => value.Replace("'", "''", StringComparison.Ordinal);

    private static bool IsNewerVersion(string current, string latest)
    {
        if (!Version.TryParse(NormalizeVersionText(current), out var currentVersion)
            || !Version.TryParse(NormalizeVersionText(latest), out var latestVersion))
        {
            return !string.Equals(current, latest, StringComparison.OrdinalIgnoreCase);
        }

        return latestVersion > currentVersion;
    }

    private static string GetCurrentVersionText()
    {
        var version = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? "0.0.0";

        return NormalizeVersionText(version);
    }

    private static string NormalizeVersionText(string version)
    {
        var normalized = version.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
        {
            normalized = normalized[1..];
        }

        var metadataIndex = normalized.IndexOf('+', StringComparison.Ordinal);
        if (metadataIndex >= 0)
        {
            normalized = normalized[..metadataIndex];
        }

        return normalized;
    }

    private void OnStateChanged()
        => StateChanged?.Invoke(this, EventArgs.Empty);

    private sealed class ReleaseInfo
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; init; }

        [JsonPropertyName("assets")]
        public ReleaseAsset[]? Assets { get; init; }
    }

    private sealed class ReleaseAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string? DownloadUrl { get; init; }
    }
}
