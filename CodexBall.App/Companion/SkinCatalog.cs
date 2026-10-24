using System.IO;
using CodexBall.Core.Companion;

namespace CodexBall.App.Companion;

public sealed record SkinEntry(string Id, string? Directory, string Name);

public static class SkinCatalog
{
    public const string BallId = "codexball";
    public static string BuiltInDirectory => Path.Combine(AppContext.BaseDirectory, "Skins", "local-koharu");
    public static string LegacyDirectory => Path.Combine(AppContext.BaseDirectory, "Skins", "cream-pink");

    public static IReadOnlyList<SkinEntry> Discover(out string? warning)
    {
        var entries = new List<SkinEntry> { new(BallId, null, "CodexBall") };
        var errors = new List<string>();
        var bundled = Path.Combine(AppContext.BaseDirectory, "Skins");
        foreach (var id in new[] { "local-koharu", "local-haruto", "local-mao_pro", "local-mark_free_t04" })
        {
            var directory = Path.Combine(bundled, id);
            if (!System.IO.Directory.Exists(directory)) continue;
            try
            {
                var skin = SkinLoader.Load(directory);
                if (skin.Definition.Id == id) entries.Add(new(id, directory, skin.Definition.Name));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException)
            { errors.Add($"{Path.GetFileName(directory)}：{ex.Message}"); }
        }
        warning = errors.Count == 0 ? null : string.Join(Environment.NewLine, errors);
        return entries;
    }
}
