using System.Text.Json;
using CodexBall.Core.Companion;
using CodexBall.Core.Models;

namespace CodexBall.Tests;

public sealed class CompanionTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "CodexBall-tests", Guid.NewGuid().ToString("N"));

    public CompanionTests() { Directory.CreateDirectory(_directory); File.WriteAllBytes(Path.Combine(_directory, "idle.png"), [0]); }

    private CompanionSkin ValidSkin() => new()
    {
        Id = "test", Name = "Test", Actions = new()
        {
            ["Idle"] = new() { Loop = true, Frames = [new() { File = "idle.png" }] }
        }
    };

    private LoadedSkin Load(CompanionSkin skin)
    {
        File.WriteAllText(Path.Combine(_directory, "manifest.json"), JsonSerializer.Serialize(skin));
        return SkinLoader.Load(_directory);
    }

    [Fact]
    public void IdleOnlySkinFallsBackForEveryOptionalAction()
    {
        var loaded = Load(ValidSkin());
        foreach (var action in Enum.GetValues<CompanionAction>())
            Assert.Same(loaded.Definition.Actions["Idle"], loaded.Resolve(action));
    }

    [Theory]
    [InlineData("../outside.png")]
    [InlineData("C:\\outside.png")]
    [InlineData("https://example.com/idle.png")]
    [InlineData("idle.png:secret")]
    public void RejectsOutsideOrNonLocalPaths(string file)
    {
        var skin = ValidSkin();
        skin.Actions["Idle"].Frames[0].File = file;
        Assert.Throws<InvalidDataException>(() => Load(skin));
    }

    [Fact]
    public void RejectsMissingIdleAndUnsupportedVersion()
    {
        var skin = ValidSkin();
        skin.Actions.Clear();
        Assert.Throws<InvalidDataException>(() => Load(skin));
        skin = ValidSkin(); skin.SchemaVersion = 2;
        Assert.Throws<InvalidDataException>(() => Load(skin));
    }

    [Fact]
    public void RejectsInvalidTimingAndExcessiveFrames()
    {
        var skin = ValidSkin(); skin.Actions["Idle"].Frames[0].DurationMs = 0;
        Assert.Throws<InvalidDataException>(() => Load(skin));
        skin = ValidSkin();
        skin.Actions["Idle"].Frames = Enumerable.Range(0, 257).Select(_ => new SkinFrame { File = "idle.png" }).ToList();
        Assert.Throws<InvalidDataException>(() => Load(skin));
    }

    [Fact]
    public void UnknownActionsDoNotPreventLoading()
    {
        var skin = ValidSkin(); skin.Actions["FutureAction"] = new();
        Assert.NotNull(Load(skin));
    }

    [Fact]
    public void SupportsVariantSequencesAndRejectsEmptyVariants()
    {
        var skin = ValidSkin();
        skin.Actions["Pat"] = new() { Variants = [[new() { File = "idle.png" }]] };
        Assert.Single(Load(skin).Resolve(CompanionAction.Pat).Sequences());
        skin.Actions["Pat"].Variants.Add([]);
        Assert.Throws<InvalidDataException>(() => Load(skin));
    }

    [Fact]
    public void Live2DReferencesAreValidatedBeforeLoadingNativeModel()
    {
        var skin = ValidSkin();
        skin.Actions.Clear();
        skin.Live2DModel = "model.model3.json";
        File.WriteAllBytes(Path.Combine(_directory, "model.moc3"), [0]);
        File.WriteAllText(Path.Combine(_directory, "model.model3.json"), """
            {"FileReferences":{"Moc":"model.moc3","Textures":["idle.png"]}}
            """);
        Assert.Equal("model.model3.json", Load(skin).Definition.Live2DModel);
        File.WriteAllText(Path.Combine(_directory, "model.model3.json"), """
            {"FileReferences":{"Moc":"../outside.moc3","Textures":["idle.png"]}}
            """);
        Assert.Throws<InvalidDataException>(() => Load(skin));
    }

    [Fact]
    public void LowQuotaHasHysteresisAndNoInitialReaction()
    {
        var tracker = new QuotaReactionTracker();
        var reset = DateTimeOffset.UtcNow.AddHours(1);
        RateLimitWindow Window(int remaining) => new(100 - remaining, 300, reset);
        Assert.Null(tracker.Observe(Window(30)));
        Assert.Equal(CompanionAction.QuotaLow, tracker.Observe(Window(19)));
        Assert.Null(tracker.Observe(Window(19)));
        Assert.Null(tracker.Observe(Window(21)));
        Assert.Null(tracker.Observe(Window(19)));
        Assert.Null(tracker.Observe(Window(25)));
        Assert.Equal(CompanionAction.QuotaLow, tracker.Observe(Window(19)));
        Assert.Null(tracker.Observe(null));
        Assert.Null(tracker.Observe(Window(10)));
    }

    [Fact]
    public void RecoveryRequiresResetAdvanceAndIncrease()
    {
        var tracker = new QuotaReactionTracker();
        var reset = DateTimeOffset.UtcNow.AddHours(-1);
        Assert.Null(tracker.Observe(new(90, 300, reset)));
        Assert.Null(tracker.Observe(new(80, 300, reset)));
        Assert.Equal(CompanionAction.QuotaRecovered, tracker.Observe(new(0, 300, reset.AddHours(5))));
        Assert.Null(tracker.Observe(new(0, 300, reset.AddHours(5))));
        Assert.Null(tracker.Observe(new(20, 300, reset.AddHours(10))));
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
