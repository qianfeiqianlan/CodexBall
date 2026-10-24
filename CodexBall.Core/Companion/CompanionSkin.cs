using System.Text.Json.Serialization;

namespace CodexBall.Core.Companion;

[JsonConverter(typeof(JsonStringEnumConverter<CompanionAction>))]
public enum CompanionAction { Idle, Pat, Drag, Drop, Sleep, QuotaLow, QuotaRecovered }

public sealed class SkinRect
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}

public sealed class SkinFrame
{
    public string File { get; set; } = "";
    public int DurationMs { get; set; } = 150;
    // Optional pixel crop for sprite sheets. Rendering does not modify the source image.
    public SkinRect? Crop { get; set; }
}

public sealed class SkinAnimation
{
    public bool Loop { get; set; }
    public List<SkinFrame> Frames { get; set; } = [];
    public SkinRect? Orb { get; set; }
    public SkinRect? ImageBounds { get; set; }
    public List<List<SkinFrame>> Variants { get; set; } = [];

    public IEnumerable<List<SkinFrame>> Sequences()
    {
        if (Frames is { Count: > 0 }) yield return Frames;
        if (Variants is not null)
            foreach (var variant in Variants) yield return variant;
    }
}

public sealed class CompanionSkin
{
    public int SchemaVersion { get; set; } = 1;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Author { get; set; } = "";
    public string License { get; set; } = "";
    public string? Live2DModel { get; set; }
    public Dictionary<string, string> MotionGroups { get; set; } = [];
    public double Width { get; set; } = 144;
    public double Height { get; set; } = 180;
    public SkinRect Head { get; set; } = new() { X = 25, Y = 15, Width = 95, Height = 65 };
    public SkinRect Orb { get; set; } = new() { X = 55, Y = 85, Width = 34, Height = 34 };
    public Dictionary<string, SkinAnimation> Actions { get; set; } = new(StringComparer.Ordinal);
}

public sealed record LoadedSkin(string Directory, CompanionSkin Definition)
{
    public SkinAnimation Resolve(CompanionAction action)
        => Definition.Actions.TryGetValue(action.ToString(), out var animation)
            ? animation : Definition.Actions[nameof(CompanionAction.Idle)];
}
