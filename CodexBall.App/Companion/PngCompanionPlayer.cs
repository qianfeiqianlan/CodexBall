using System.IO;
using System.Buffers.Binary;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CodexBall.Core.Companion;

namespace CodexBall.App.Companion;

public sealed class PngCompanionPlayer : ICompanionPlayer
{
    private readonly LoadedSkin _skin;
    private readonly Dictionary<SkinFrame, BitmapSource> _frames = [];
    private readonly DispatcherTimer _timer = new();
    private SkinAnimation? _animation;
    private IReadOnlyList<SkinFrame> _sequence = [];
    private int _index;
    private bool _paused;
    public event EventHandler? FrameChanged;
    public event EventHandler? Completed;
    public ImageSource? Image { get; private set; }
    public SkinRect Orb => _animation?.Orb ?? _skin.Definition.Orb;
    public SkinRect ImageBounds => _animation?.ImageBounds ?? new SkinRect { Width = _skin.Definition.Width, Height = _skin.Definition.Height };

    public PngCompanionPlayer(LoadedSkin skin)
    {
        _skin = skin;
        var images = new Dictionary<string, BitmapSource>(StringComparer.OrdinalIgnoreCase);
        long pixels = 0;
        Span<byte> header = stackalloc byte[24];
        foreach (var (name, animation) in skin.Definition.Actions)
        {
            if (!Enum.TryParse<CompanionAction>(name, out var action) || !Enum.IsDefined(action)) continue;
            foreach (var frame in animation.Sequences().SelectMany(s => s))
            {
                var path = SkinLoader.ResolveFile(skin.Directory, frame.File);
                if (!images.TryGetValue(path, out var source))
                {
                    using var stream = File.OpenRead(path);
                    stream.ReadExactly(header);
                    if (!header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
                        throw new InvalidDataException("素材不是有效的 PNG 图片。");
                    var width = BinaryPrimitives.ReadInt32BigEndian(header[16..20]);
                    var height = BinaryPrimitives.ReadInt32BigEndian(header[20..24]);
                    if (width is <= 0 or > 4096 || height is <= 0 or > 4096
                        || (pixels += (long)width * height) > 16_000_000)
                        throw new InvalidDataException("外观图片尺寸过大：单边上限 4096，总像素上限 1600 万。");
                    stream.Position = 0;
                    var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    source = decoder.Frames[0];
                    source.Freeze();
                    images[path] = source;
                }
                if (frame.Crop is { } crop)
                {
                    if (crop.X + crop.Width > source.PixelWidth || crop.Y + crop.Height > source.PixelHeight)
                        throw new InvalidDataException("裁切区域超出图片边界。");
                    var cropped = new CroppedBitmap(source, new Int32Rect((int)crop.X, (int)crop.Y, (int)crop.Width, (int)crop.Height));
                    cropped.Freeze();
                    _frames[frame] = cropped;
                }
                else _frames[frame] = source;
            }
        }
        _timer.Tick += OnTick;
        Play(CompanionAction.Idle);
    }

    public void Play(CompanionAction action)
    {
        _timer.Stop();
        _animation = _skin.Resolve(action);
        var sequences = _animation.Sequences().ToArray();
        _sequence = sequences[Random.Shared.Next(sequences.Length)];
        _index = 0;
        ShowFrame();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_animation is null) return;
        if (++_index >= _sequence.Count)
        {
            if (_animation.Loop) _index = 0;
            else { _timer.Stop(); Completed?.Invoke(this, EventArgs.Empty); return; }
        }
        ShowFrame();
    }

    private void ShowFrame()
    {
        if (_animation is null) return;
        var frame = _sequence[_index];
        Image = _frames[frame];
        FrameChanged?.Invoke(this, EventArgs.Empty);
        _timer.Interval = TimeSpan.FromMilliseconds(frame.DurationMs);
        // A single static looping frame needs no timer.
        if (!_paused && !(_animation.Loop && _sequence.Count == 1)) _timer.Start();
    }

    public void Stop() { _timer.Stop(); _animation = null; }
    public void Pause() { _paused = true; _timer.Stop(); }
    public void Resume() { _paused = false; ShowFrame(); }
    public void Dispose() { Stop(); _timer.Tick -= OnTick; _frames.Clear(); Image = null; }
}
