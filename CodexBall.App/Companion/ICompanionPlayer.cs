using System.Windows.Media;
using CodexBall.Core.Companion;

namespace CodexBall.App.Companion;

public interface ICompanionPlayer : IDisposable
{
    event EventHandler? FrameChanged;
    event EventHandler? Completed;
    ImageSource? Image { get; }
    SkinRect Orb { get; }
    SkinRect ImageBounds { get; }
    void Play(CompanionAction action);
    void Stop();
    void Pause();
    void Resume();
}
