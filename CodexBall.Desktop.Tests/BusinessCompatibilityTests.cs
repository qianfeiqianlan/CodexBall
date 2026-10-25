using System.Globalization;
using System.Text.Json;
using CodexBall.App.Companion;
using CodexBall.App.Services;
using CodexBall.Core.Codex;
using CodexBall.Core.Companion;
using CodexBall.Desktop;

namespace CodexBall.Desktop.Tests;

public sealed class BusinessCompatibilityTests : IDisposable
{
    [Theory]
    [InlineData(10,"browser",false)]
    [InlineData(10,"Codex",true)]
    [InlineData(10,"codex",true)]
    [InlineData(1,"codex",false)]
    [InlineData(2,"codex",false)]
    public void ActivityIgnoresOtherProcessesAndOwnAppServer(int id,string name,bool expected)
        => Assert.Equal(expected,CodexProcessMonitor.IsExternalCodexProcess(id,name,1,new HashSet<int> { 2,3 }));

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "CodexBall-tests-" + Guid.NewGuid());
    public BusinessCompatibilityTests() => Directory.CreateDirectory(_directory);
    public void Dispose() => Directory.Delete(_directory, true);
    [Fact]
    public async Task LegacySettingsRemainCompatibleAndPreserveVisiblePosition()
    {
        await File.WriteAllTextAsync(Path.Combine(_directory, "settings.json"), """{"Left":123,"Top":456,"FocusMode":true,"SkinId":"local-mao_pro","CompanionMode":true,"EdgeHideEnabled":true,"Opacity":0.75} """);
        var store = new SettingsService(_directory); var saved = await store.LoadAsync();
        Assert.Equal("system", saved.Language); Assert.Equal("local-mao_pro", saved.SkinId); Assert.Equal(123, saved.Left); Assert.True(saved.EdgeHideEnabled);
        saved.Language = "zh-CN"; await store.SaveAsync(saved); var restored = await store.LoadAsync();
        Assert.Equal("zh-CN", restored.Language); Assert.Equal(.75, restored.Opacity); Assert.Equal(456, restored.Top);
    }
    [Fact]
    public async Task InterruptedJsonAndInvalidOpacityCannotPreventLaunch()
    {
        var store = new SettingsService(_directory);
        await File.WriteAllTextAsync(Path.Combine(_directory, "settings.json"), "{\"Left\":");
        Assert.Equal("codexball", (await store.LoadAsync()).SkinId);
        await File.WriteAllTextAsync(Path.Combine(_directory, "settings.json"), "{\"Opacity\":12}");
        Assert.Equal(1, (await store.LoadAsync()).Opacity);
    }
    [Fact]
    public async Task ConcurrentSavesLeaveValidJsonWithoutTempFile()
    {
        var store = new SettingsService(_directory);
        await Task.WhenAll(Enumerable.Range(0, 10).Select(i => store.SaveAsync(new AppSettings { Left = i })));
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(_directory, "settings.json")));
        Assert.InRange(json.RootElement.GetProperty("Left").GetDouble(), 0, 9);
        Assert.False(File.Exists(Path.Combine(_directory, "settings.json.tmp")));
    }
    [Theory]
    [InlineData("zh-TW", "system", "zh-CN")]
    [InlineData("ja-JP", "system", "en-US")]
    [InlineData("zh-CN", "en-US", "en-US")]
    [InlineData("en-US", "zh-CN", "zh-CN")]
    public void LanguageFollowsStartupCultureAndExplicitPreference(string culture, string preference, string expected)
        => Assert.Equal(expected, AppLocalization.ResolveLanguage(preference, CultureInfo.GetCultureInfo(culture)));
    [Fact]
    public void CuratedCatalogOmitsRemovedAndUnknownModelsAndKeepsOrder()
    {
        foreach (var id in new[] { "local-mark_free_t04", "custom", "local-haruto", "local-koharu", "live2d-haru", "local-mao_pro" })
        {
            var dir = Path.Combine(_directory, id); Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, "idle.png"), [0]);
            var skin = new CompanionSkin { Id = id, Name = id, Actions = new() { ["Idle"] = new SkinAnimation { Loop = true, Frames = [new SkinFrame { File = "idle.png" }] } } };
            File.WriteAllText(Path.Combine(dir, "manifest.json"), JsonSerializer.Serialize(skin));
        }
        var entries = SkinCatalog.Discover(out var warning, _directory);
        Assert.Null(warning);
        Assert.Equal(new[] { "codexball", "local-koharu", "local-haruto", "local-mao_pro", "local-mark_free_t04" }, entries.Select(e => e.Id));
        File.WriteAllText(Path.Combine(_directory, "local-koharu", "manifest.json"), "broken");
        Assert.DoesNotContain(SkinCatalog.Discover(out warning, _directory), e => e.Id == "local-koharu");
        Assert.NotNull(warning);
    }
    [Theory]
    [InlineData("CodexBall-0.2.7-win-x64.zip", "win-x64", false)]
    [InlineData("CodexBall-Desktop-0.2.8-win-x64.zip", "win-x64", true)]
    [InlineData("CodexBall-Desktop-0.2.8-osx-arm64.zip", "win-x64", false)]
    [InlineData("CodexBall-Desktop-0.2.8-osx-arm64.zip", "osx-arm64", true)]
    public void UpgradeNeverSelectsLegacyWpfOrAnotherPlatform(string asset, string rid, bool expected)
        => Assert.Equal(expected, UpdateService.MatchesAsset(asset, rid));
    [Fact]
    public void ExecutableLookupRespectsPathOrderAndWindowsLaunchRules()
    {
        File.WriteAllText(Path.Combine(_directory, "codex.cmd"), "stub");
        var windows = CodexLocator.FindInDirectories([_directory], true);
        Assert.NotNull(windows); Assert.True(windows.RequiresCommandShell);
        File.WriteAllText(Path.Combine(_directory, "codex.exe"), "stub");
        Assert.EndsWith("codex.exe", CodexLocator.FindInDirectories([_directory], true)!.Path);
        Assert.Null(CodexLocator.FindInDirectories([_directory], false));
        var unixPath = Path.Combine(_directory, "codex"); File.WriteAllText(unixPath, "#!/bin/sh\n");
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(unixPath, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        var unix = CodexLocator.FindInDirectories([_directory], false);
        Assert.NotNull(unix); Assert.False(unix.RequiresCommandShell);
    }
}
