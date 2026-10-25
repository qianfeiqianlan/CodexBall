using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Threading;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System.Runtime.InteropServices;
using CodexBall.App.Companion.Live2D;
using CodexBall.Core.Companion;
using Live2DCSharpSDK.App;
using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Motion;
using Live2DCSharpSDK.OpenGL;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using PixelFormat = OpenTK.Graphics.OpenGL4.PixelFormat;
using NativeWindow = OpenTK.Windowing.Desktop.NativeWindow;

namespace CodexBall.Rendering;

/// <summary>Official Cubism Core + community C# framework. GPU offscreen rendering is copied to Avalonia with alpha.</summary>
public sealed class Live2DCompanionPlayer : ICompanionPlayer
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly LoadedSkin _skin;
    private NativeWindow? _context;
    private LAppDelegateOpenGL? _app;
    private LAppModel? _model;
    private readonly WriteableBitmap _bitmap;
    private readonly int _width, _height;
    private readonly byte[] _pixels, _flipped;
    private int _framebuffer, _colorTexture;
    private bool _running, _disposed, _completed;
    private CompanionAction _action;
    private long _previousTick;
    private float _spring, _springVelocity, _springTarget;
    private float _lookX, _lookY;
    private float _elapsed;
    private string _eyeLeft = "ParamEyeLOpen", _eyeRight = "ParamEyeROpen";
    private static readonly CubismOption Options = new() { LogFunction = _ => { }, LoggingLevel = LogLevel.Off };

    public event EventHandler? FrameChanged;
    public event EventHandler? Completed;
    public event EventHandler<Exception>? Failed;
    public WriteableBitmap Image => _bitmap;
    Avalonia.Media.IImage? ICompanionPlayer.Image => _bitmap;
    public SkinRect Orb => _skin.Definition.Orb;
    public SkinRect ImageBounds { get; }
    public string RendererDescription { get; private set; } = "";
    public long RenderedFrames { get; private set; }
    public double LastFrameMilliseconds { get; private set; }

    public Live2DCompanionPlayer(LoadedSkin skin, bool forceCoreProfile = false)
    {
        _skin = skin;
        _timer.Tick += OnRendering;
        _width = (int)skin.Definition.Width * 2;
        _height = (int)skin.Definition.Height * 2;
        ImageBounds = new SkinRect { Width = skin.Definition.Width, Height = skin.Definition.Height };
        // Keep bitmap DPI at 96; the Image control explicitly downsamples 2x pixels to the skin DIP size.
        _bitmap = new WriteableBitmap(new PixelSize(_width, _height), new Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888, AlphaFormat.Premul);
        _pixels = new byte[_width * _height * 4];
        _flipped = new byte[_pixels.Length];
        var stage = "OpenGL context creation";
        try
        {
            GLFWProvider.CheckForMainThread = false; // All access stays on the owning Avalonia dispatcher thread.
            _context = new NativeWindow(new NativeWindowSettings
            {
                ClientSize = new Vector2i(_width, _height),
                StartVisible = false,
                StartFocused = false,
                APIVersion = new Version(3, 3),
                Profile = (OperatingSystem.IsMacOS() || forceCoreProfile) ? ContextProfile.Core : ContextProfile.Compatability,
                Flags = (OperatingSystem.IsMacOS() || forceCoreProfile) ? ContextFlags.ForwardCompatible : ContextFlags.Offscreen,
                Title = "CodexBall offscreen renderer"
            });
            _context.MakeCurrent();
            RendererDescription = $"{GL.GetString(StringName.Vendor)} / {GL.GetString(StringName.Renderer)} / {GL.GetString(StringName.Version)}";
            stage = "Cubism Core startup / native library loading";
            if (!CubismFramework.IsStarted) CubismFramework.StartUp(new LAppAllocator(), Options);
            stage = "OpenGL framebuffer creation";
            _framebuffer = GL.GenFramebuffer();
            _colorTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _colorTexture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, _width, _height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _colorTexture, 0);
            if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
                throw new InvalidOperationException("Live2D 离屏渲染目标创建失败。");
            GL.Viewport(0, 0, _width, _height);
            stage = "Live2D adapter initialization";
            _app = new LAppDelegateOpenGL((OperatingSystem.IsMacOS() || forceCoreProfile) ? new CoreProfileOpenTkApi(_context) : new OpenTkApi(_context));
            var modelPath = SkinLoader.ResolveFile(skin.Directory, skin.Definition.Live2DModel!);
            stage = "Model / texture / shader loading";
            _model = _app.Live2dManager.LoadModel(Path.GetDirectoryName(modelPath)! + Path.DirectorySeparatorChar, Path.GetFileName(modelPath));
            ConfigureParameterIds(_model);
            _model.CustomValueUpdate = true;
            _model.ValueUpdate = UpdateParameters;
            Play(CompanionAction.Idle);
            stage = "First Live2D frame";
            RenderFrame(1f / 60);
        }
        catch (Exception ex)
        {
            Exception cause = ex;
            try { Dispose(); } catch (Exception cleanup) { cause = new AggregateException(ex, cleanup); }
            throw new InvalidOperationException($"Live2D initialization failed at: {stage}. Renderer: {RendererDescription}. Run scripts/Setup-AvaloniaProbe.ps1 for matching Native 5 R4.1 binaries.\n{ex.Message}", cause);
        }
    }

    private void ConfigureParameterIds(LAppModel model)
    {
        var parameters = model.Parameters.ToHashSet(StringComparer.Ordinal);
        string Resolve(string modern, string legacy) => parameters.Contains(modern) ? modern : parameters.Contains(legacy) ? legacy : modern;
        model.IdParamAngleX = Resolve("ParamAngleX", "PARAM_ANGLE_X");
        model.IdParamAngleY = Resolve("ParamAngleY", "PARAM_ANGLE_Y");
        model.IdParamAngleZ = Resolve("ParamAngleZ", "PARAM_ANGLE_Z");
        model.IdParamBodyAngleX = Resolve("ParamBodyAngleX", "PARAM_BODY_ANGLE_X");
        model.IdParamEyeBallX = Resolve("ParamEyeBallX", "PARAM_EYE_BALL_X");
        model.IdParamEyeBallY = Resolve("ParamEyeBallY", "PARAM_EYE_BALL_Y");
        model.IdParamBreath = Resolve("ParamBreath", "PARAM_BREATH");
        _eyeLeft = Resolve("ParamEyeLOpen", "PARAM_EYE_L_OPEN");
        _eyeRight = Resolve("ParamEyeROpen", "PARAM_EYE_R_OPEN");
        model.LoadBreath();
    }

    public void Play(CompanionAction action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _action = action;
        _completed = false;
        if (_model is null) return;
        _model.RandomMotion = action != CompanionAction.Drag;
        if (action == CompanionAction.Drag)
        {
            _springTarget = 12;
            return;
        }
        if (action == CompanionAction.Drop)
        {
            _springTarget = 0;
            _springVelocity += 55;
        }
        else _springTarget = 0;
        var group = _skin.Definition.MotionGroups.GetValueOrDefault(action.ToString());
        if (group is null) group = "Idle";
        if (_model.StartRandomMotion(group, MotionPriority.PriorityForce, (_, _) => _completed = action != CompanionAction.Idle) is null)
        {
            _model.StartRandomMotion("Idle", MotionPriority.PriorityForce);
            _completed = action != CompanionAction.Idle;
        }
    }

    private void UpdateParameters(LAppModel model)
    {
        model.Model.AddParameterValue(model.IdParamAngleX, model._dragX * 18);
        model.Model.AddParameterValue(model.IdParamAngleY, model._dragY * 12);
        model.Model.AddParameterValue(model.IdParamAngleZ, _spring);
        model.Model.AddParameterValue(model.IdParamBodyAngleX, model._dragX * 5 + _spring * 0.2f);
        model.Model.AddParameterValue(model.IdParamEyeBallX, model._dragX);
        model.Model.AddParameterValue(model.IdParamEyeBallY, model._dragY);
        // The adapter's automatic blink is disabled upstream; add a short blink over the base motion.
        var phase = _elapsed % 4.2f;
        if (phase < .25f)
        {
            var open = MathF.Abs(phase - .125f) / .125f;
            model.Model.SetParameterValue(_eyeLeft, open);
            model.Model.SetParameterValue(_eyeRight, open);
        }
    }

    public void SetLookTarget(double x, double y)
    {
        _lookX = (float)Math.Clamp(x, -1, 1);
        _lookY = (float)Math.Clamp(y, -1, 1);
    }

    public void SetDragImpulse(double horizontalDelta)
        => _springTarget = (float)Math.Clamp(horizontalDelta * 2, -22, 22);

    public void RenderFrame(float seconds)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var started = Stopwatch.GetTimestamp();
        var dt = Math.Clamp(seconds, 0, .05f);
        _elapsed += dt;
        _springVelocity += ((_springTarget - _spring) * 90 - _springVelocity * 13) * dt;
        _spring += _springVelocity * dt;
        _context!.MakeCurrent();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
        GL.Viewport(0, 0, _width, _height);
        _model!.SetDragging(_lookX, _lookY);
        _app!.Run(dt);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
        GL.ReadPixels(0, 0, _width, _height, PixelFormat.Bgra, PixelType.UnsignedByte, _pixels);
        var stride = _width * 4;
        for (var y = 0; y < _height; y++) System.Buffer.BlockCopy(_pixels, y * stride, _flipped, (_height - y - 1) * stride, stride);
        using (var buffer = _bitmap.Lock())
            for (var y = 0; y < _height; y++) Marshal.Copy(_flipped, y * stride, buffer.Address + y * buffer.RowBytes, stride);
        RenderedFrames++;
        LastFrameMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        FrameChanged?.Invoke(this, EventArgs.Empty);
        if (_completed) { _completed = false; Completed?.Invoke(this, EventArgs.Empty); }
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = Stopwatch.GetTimestamp();
        var elapsed = Stopwatch.GetElapsedTime(_previousTick, now).TotalSeconds;
        if (elapsed < 1.0 / 65) return;
        _previousTick = now;
        try { RenderFrame((float)elapsed); }
        catch (Exception ex) { Pause(); Failed?.Invoke(this, ex); }
    }

    public void Stop() { Pause(); _model?._motionManager.StopAllMotions(); }
    public void Pause()
    {
        if (!_running) return;
        _timer.Stop();
        _running = false;
    }
    public void Resume()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_running) return;
        _previousTick = Stopwatch.GetTimestamp();
        _timer.Start();
        _running = true;
    }
    public void Dispose()
    {
        if (_disposed) return;
        Pause();
        _disposed = true;
        if (_context is null) { _bitmap.Dispose(); return; }
        _context.MakeCurrent();
        if (_model is not null)
            foreach (var texture in _model.Textures) texture.Dispose();
        _app?.Dispose();
        if (_framebuffer != 0) GL.DeleteFramebuffer(_framebuffer);
        if (_colorTexture != 0) GL.DeleteTexture(_colorTexture);
        // Destroying this private GL context also reclaims adapter-owned buffers/masks.
        _context.Dispose();
        _context = null;
        _bitmap.Dispose();
    }
}
