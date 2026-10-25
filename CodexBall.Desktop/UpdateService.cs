using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace CodexBall.Desktop;

internal sealed class UpdateService
{
    public static string CurrentVersion => typeof(UpdateService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.0.0";
    private ReleaseAsset? _asset;
    private bool _upgrading;
    public string? LatestVersion { get; private set; }
    public bool IsUpgradeAvailable => _asset is not null && LatestVersion is not null;
    public event Action? StateChanged;
    private static readonly HttpClient Client = CreateClient();
    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) }; client.DefaultRequestHeaders.UserAgent.ParseAdd("CodexBall.Desktop"); return client;
    }
    public async Task CheckAsync()
    {
        if (DesktopPlatform.IsPackaged) return;
        try
        {
            var release = await Client.GetFromJsonAsync<Release>("https://api.github.com/repos/qianfeiqianlan/CodexBall/releases/latest");
            var latest = release?.Tag?.TrimStart('v', 'V');
            if (!Version.TryParse(latest, out var version) || !Version.TryParse(CurrentVersion, out var current) || version <= current) return;
            _asset = release!.Assets?.FirstOrDefault(a => MatchesAsset(a.Name, RuntimeInformation.RuntimeIdentifier));
            if (_asset is not null) LatestVersion = latest;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException) { DesktopApp.Log(e); }
        StateChanged?.Invoke();
    }
    public static bool MatchesAsset(string name, string rid) => name.StartsWith("CodexBall-Desktop-", StringComparison.OrdinalIgnoreCase) && name.EndsWith($"-{rid}.zip", StringComparison.OrdinalIgnoreCase);
    public async Task<bool> StartUpgradeAsync()
    {
        if (DesktopPlatform.IsPackaged || _asset is null || _upgrading) return false;
        _upgrading = true;
        try
        {
            if (!Uri.TryCreate(_asset.Url, UriKind.Absolute, out var url) || url.Scheme != "https" || url.Host != "github.com") throw new InvalidDataException("Invalid release download URL.");
            var temp = Path.Combine(Path.GetTempPath(), "CodexBall", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temp);
            var zip = Path.Combine(temp, "release.zip");
            using (var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                await using var file = File.Create(zip); await response.Content.CopyToAsync(file);
            }
            var extracted = Path.Combine(temp, "extracted"); ZipFile.ExtractToDirectory(zip, extracted);
            var host = Path.GetFileName(DesktopPlatform.AppHost);
            var candidates = Directory.GetFiles(extracted, host, SearchOption.AllDirectories);
            if (candidates.Length != 1) throw new InvalidDataException("Expected exactly one CodexBall.Desktop executable in the release.");
            var source = Path.GetDirectoryName(candidates[0])!;
            if (!File.Exists(Path.Combine(source, "CodexBall.Desktop.runtimeconfig.json"))) throw new InvalidDataException("Invalid desktop release.");
            var script = Path.Combine(temp, OperatingSystem.IsWindows() ? "update.ps1" : "update.sh");
            if (OperatingSystem.IsWindows())
            {
                await File.WriteAllTextAsync(script, """
param([int]$targetPid,[string]$source,[string]$destination,[string]$executable)
$ErrorActionPreference='Stop'
Wait-Process -Id $targetPid -Timeout 60 -ErrorAction SilentlyContinue
if (Get-Process -Id $targetPid -ErrorAction SilentlyContinue) { throw 'Application has not exited' }
Get-ChildItem -LiteralPath $source -Force | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $destination -Recurse -Force }
Start-Process -FilePath $executable -WindowStyle Hidden
""");
            }
            else
            {
                await File.WriteAllTextAsync(script, """
#!/bin/sh
set -eu
pid="$1"; source="$2"; destination="$3"; executable="$4"
attempt=0
while kill -0 "$pid" 2>/dev/null; do
  attempt=$((attempt+1))
  [ "$attempt" -lt 60 ] || exit 1
  sleep 1
done
/usr/bin/ditto "$source" "$destination"
/bin/chmod +x "$executable"
exec "$executable"
""");
            }
            var start = new ProcessStartInfo(OperatingSystem.IsWindows() ? "powershell.exe" : "/bin/sh") { UseShellExecute = false, CreateNoWindow = true };
            if (OperatingSystem.IsWindows()) { start.ArgumentList.Add("-NoProfile"); start.ArgumentList.Add("-ExecutionPolicy"); start.ArgumentList.Add("Bypass"); start.ArgumentList.Add("-File"); }
            var destination = AppContext.BaseDirectory;
            if (OperatingSystem.IsMacOS())
            {
                var current = new DirectoryInfo(destination); var payload = new DirectoryInfo(source);
                if (current.Name == "MacOS" && current.Parent?.Name == "Contents" && current.Parent.Parent?.Extension == ".app" && payload.Parent?.Parent?.Extension == ".app")
                { destination = current.Parent.Parent.FullName; source = payload.Parent.Parent.FullName; }
            }
            start.ArgumentList.Add(script); start.ArgumentList.Add(Environment.ProcessId.ToString()); start.ArgumentList.Add(source); start.ArgumentList.Add(destination); start.ArgumentList.Add(DesktopPlatform.AppHost);
            using var updater = Process.Start(start) ?? throw new InvalidOperationException("Unable to start updater.");
            return true;
        }
        finally { _upgrading = false; StateChanged?.Invoke(); }
    }
    private sealed class Release { [JsonPropertyName("tag_name")] public string? Tag { get; set; } [JsonPropertyName("assets")] public ReleaseAsset[]? Assets { get; set; } }
    private sealed class ReleaseAsset { [JsonPropertyName("name")] public string Name { get; set; } = ""; [JsonPropertyName("browser_download_url")] public string? Url { get; set; } }
}
