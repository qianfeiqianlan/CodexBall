using Avalonia.Media;
using CodexBall.Core.Companion;

namespace CodexBall.Rendering;

public interface ICompanionPlayer : IDisposable
{
    event EventHandler? FrameChanged;
    event EventHandler? Completed;
    event EventHandler<Exception>? Failed;
    IImage? Image { get; }
    SkinRect ImageBounds { get; }
    void Play(CompanionAction action);
    void Stop();
    void Pause();
    void Resume();
    void SetLookTarget(double x, double y);
    void SetDragImpulse(double horizontalDelta);
}
