using System.ComponentModel;
using System.Globalization;

namespace CodexBall.App.Services;

public sealed class AppLocalization : INotifyPropertyChanged
{
    public static AppLocalization Current { get; } = new();
    private readonly CultureInfo _systemCulture = CultureInfo.CurrentUICulture;
    public string Preference { get; private set; } = "system";
    public bool IsChinese => ResolveLanguage(Preference, _systemCulture) == "zh-CN";
    public event PropertyChangedEventHandler? PropertyChanged;
    public static string ResolveLanguage(string? preference, CultureInfo systemCulture)
        => preference switch { "zh-CN" => "zh-CN", "en-US" => "en-US", _ => systemCulture.TwoLetterISOLanguageName == "zh" ? "zh-CN" : "en-US" };
    public void Apply(string? preference)
    {
        Preference = preference is "zh-CN" or "en-US" ? preference : "system";
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }
    public string this[string key] => Text(key);
    public static string Text(string key) => Current.IsChinese && Translations.TryGetValue(key, out var value) ? value : key;
    public static string Format(string key, params object[] args) => string.Format(Text(key), args);
    private static readonly Dictionary<string, string> Translations = new()
    {
        ["Close"] = "关闭",
        ["Unable to register shortcuts.\n{0}"] = "无法注册快捷键。\n{0}",
        ["Refresh"] = "刷新",
        ["Always On Top"] = "始终置顶",
        ["Edge Hide (Alt+H)"] = "贴边隐藏 (Alt+H)",
        ["Appearance (Alt+C)"] = "外观 (Alt+C)",
        ["Language"] = "语言",
        ["Follow system"] = "跟随系统",
        ["Launch at Startup"] = "开机启动",
        ["Home"] = "项目主页",
        ["Upgrade"] = "升级",
        ["Exit"] = "退出",
        ["Upgrade to {0}"] = "升级至 {0}",
        ["Version {0}"] = "版本 {0}",
        ["Waiting for Codex"] = "等待 Codex",
        ["CodexBall Upgrade"] = "CodexBall 升级",
        ["Click to view usage details"] = "点击查看用量详情",
        ["Codex remaining quota"] = "Codex 剩余额度",
        ["Codex usage"] = "Codex 用量",
        ["Show CodexBall"] = "显示 CodexBall",
        ["Not logged in"] = "未登录",
        ["Codex not found"] = "未找到 Codex",
        ["Unavailable"] = "暂不可用",
        ["Short window"] = "短期额度",
        ["Long window"] = "长期额度",
        ["{0}% left"] = "剩余 {0}%",
        ["Reset {0}"] = "重置：{0}",
        ["Last update {0}"] = "上次更新 {0}",
        ["Loading"] = "加载中",
        ["Codex is not running"] = "Codex 未运行",
        ["Codex is not logged in"] = "Codex 未登录",
        ["Rate limit unavailable"] = "暂时无法获取额度",
        ["Appearance failed to load. Restored CodexBall.\n{0}"] = "外观加载失败，已恢复 CodexBall。\n{0}",
        ["Live2D rendering stopped. Restored CodexBall.\n{0}"] = "Live2D 渲染已暂停，已恢复 CodexBall。\n{0}",
        ["Unable to load appearance. Kept current appearance.\n{0}"] = "无法加载外观，保留当前外观。\n{0}",
        ["CodexBall Appearance"] = "CodexBall 外观"
    };
}
