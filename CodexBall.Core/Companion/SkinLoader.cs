using System.Text.Json;

namespace CodexBall.Core.Companion;

public static class SkinLoader
{
    public const int MaxFrames = 256;
    public const long MaxFileBytes = 16 * 1024 * 1024;
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static LoadedSkin Load(string directory)
    {
        directory = Path.GetFullPath(directory);
        var manifest = ResolveFile(directory, "manifest.json");
        if (new FileInfo(manifest).Length > 128 * 1024)
            throw new InvalidDataException("外观配置超过 128 KB。");
        var skin = JsonSerializer.Deserialize<CompanionSkin>(File.ReadAllText(manifest), Options)
            ?? throw new InvalidDataException("外观配置为空。");
        if (skin.SchemaVersion != 1 || string.IsNullOrWhiteSpace(skin.Id) || string.IsNullOrWhiteSpace(skin.Name))
            throw new InvalidDataException("外观版本不支持，或缺少 ID、名称。");
        if (!ValidSize(skin.Width, 64, 512) || !ValidSize(skin.Height, 64, 512))
            throw new InvalidDataException("外观画布必须在 64 至 512 DIP 之间。");
        ValidateRect(skin.Head, skin.Width, skin.Height);
        ValidateRect(skin.Orb, skin.Width, skin.Height);
        if (skin.Live2DModel is not null)
        {
            try { ValidateLive2D(directory, skin.Live2DModel); }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
            { throw new InvalidDataException("Live2D 模型配置结构无效。", ex); }
            if (skin.MotionGroups is null) throw new InvalidDataException("Live2D 动作映射无效。");
            return new LoadedSkin(directory, skin);
        }
        if (skin.Actions is null || !skin.Actions.ContainsKey("Idle"))
            throw new InvalidDataException("外观必须提供 Idle 动作。");
        var count = 0;
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, animation) in skin.Actions)
        {
            // Unknown actions are forward-compatible and never executed.
            if (!Enum.TryParse<CompanionAction>(name, out var action) || !Enum.IsDefined(action)) continue;
            if (animation is null || !animation.Sequences().Any())
                throw new InvalidDataException($"动作 {name} 没有帧。");
            if (animation.Orb is not null) ValidateRect(animation.Orb, skin.Width, skin.Height);
            if (animation.ImageBounds is not null) ValidateRect(animation.ImageBounds, skin.Width, skin.Height);
            foreach (var sequence in animation.Sequences())
            {
                if (sequence is null || sequence.Count == 0) throw new InvalidDataException($"动作 {name} 有空变体。");
                foreach (var frame in sequence)
                {
                    if (++count > MaxFrames || frame is null || frame.DurationMs is < 40 or > 60000)
                        throw new InvalidDataException("帧数超过限制，或帧时长不在 40 至 60000 ms 之间。");
                    var path = ResolveFile(directory, frame.File);
                    if (!string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("目前仅支持本地 PNG 素材。");
                    if (new FileInfo(path).Length > MaxFileBytes)
                        throw new InvalidDataException("单张图片不能超过 16 MB。");
                    files.Add(path);
                    if (frame.Crop is not null)
                    {
                        ValidateRect(frame.Crop, 4096, 4096);
                        if (new[] { frame.Crop.X, frame.Crop.Y, frame.Crop.Width, frame.Crop.Height }.Any(v => v != Math.Truncate(v)))
                            throw new InvalidDataException("裁切区域必须使用整数像素。");
                    }
                }
            }
        }
        if (files.Sum(f => new FileInfo(f).Length) > 64 * 1024 * 1024)
            throw new InvalidDataException("外观图片总大小不能超过 64 MB。");
        return new LoadedSkin(directory, skin);
    }

    private static void ValidateLive2D(string directory, string modelPath)
    {
        var model = ResolveFile(directory, modelPath);
        if (!model.EndsWith(".model3.json", StringComparison.OrdinalIgnoreCase) || new FileInfo(model).Length > 512 * 1024)
            throw new InvalidDataException("需要有效的 Live2D model3.json 配置。");
        using var json = JsonDocument.Parse(File.ReadAllText(model));
        if (!json.RootElement.TryGetProperty("FileReferences", out var references)
            || !references.TryGetProperty("Moc", out _) || !references.TryGetProperty("Textures", out _))
            throw new InvalidDataException("Live2D 模型缺少 Moc 或 Textures。");
        var modelDirectory = Path.GetDirectoryName(model)!;
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Check(string? relative)
        {
            if (string.IsNullOrWhiteSpace(relative)) throw new InvalidDataException("Live2D 素材路径为空。");
            var path = ResolveFile(modelDirectory, relative);
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext is not (".json" or ".png" or ".moc3" or ".wav"))
                throw new InvalidDataException("Live2D 包含不支持的素材类型。");
            if (new FileInfo(path).Length > 32 * 1024 * 1024)
                throw new InvalidDataException("Live2D 单个资源超过 32 MB。");
            files.Add(path);
            if (files.Count > 512) throw new InvalidDataException("Live2D 资源数超过 512。");
        }
        foreach (var reference in references.EnumerateObject())
        {
            if (reference.Value.ValueKind == JsonValueKind.String) Check(reference.Value.GetString());
            else if (reference.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in reference.Value.EnumerateArray())
                    if (item.ValueKind == JsonValueKind.String) Check(item.GetString());
                    else if (item.TryGetProperty("File", out var file)) Check(file.GetString());
            }
            else if (reference.Name == "Motions" && reference.Value.ValueKind == JsonValueKind.Object)
            {
                foreach (var group in reference.Value.EnumerateObject())
                    foreach (var motion in group.Value.EnumerateArray())
                    {
                        Check(motion.GetProperty("File").GetString());
                        if (motion.TryGetProperty("Sound", out var sound)) Check(sound.GetString());
                    }
            }
        }
        if (files.Sum(f => new FileInfo(f).Length) > 128 * 1024 * 1024)
            throw new InvalidDataException("Live2D 素材包超过 128 MB。");
    }

    public static string ResolveFile(string directory, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) || relativePath.Contains(':'))
            throw new InvalidDataException("素材路径必须是包内相对路径。");
        var root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("素材不能引用外观包以外的文件。");
        for (var current = path; current is not null; current = Path.GetDirectoryName(current))
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("外观素材不能使用符号链接或目录联接。");
            if (string.Equals(current.TrimEnd(Path.DirectorySeparatorChar), root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)) break;
        }
        return path;
    }

    private static bool ValidSize(double value, double min, double max)
        => double.IsFinite(value) && value >= min && value <= max;

    private static void ValidateRect(SkinRect? rect, double width, double height)
    {
        if (rect is null || !ValidSize(rect.X, 0, width) || !ValidSize(rect.Y, 0, height)
            || !ValidSize(rect.Width, 1, width) || !ValidSize(rect.Height, 1, height)
            || rect.X + rect.Width > width || rect.Y + rect.Height > height)
            throw new InvalidDataException("外观区域超出画布或尺寸无效。");
    }
}
