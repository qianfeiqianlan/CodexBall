using System.Runtime.InteropServices;
using Avalonia.Threading;

namespace CodexBall.Desktop;

// Registered hotkeys receive only these combinations; no global input monitoring.
internal sealed class MacHotKeys : IDisposable
{
    private const string Carbon = "/System/Library/Frameworks/Carbon.framework/Carbon";
    private const uint Signature = 0x43425844;
    private readonly Handler _callback;
    private nint _handler, _hide, _cycle;
    public string Status { get; }
    public bool Succeeded { get; }
    public MacHotKeys(Action hide, Action cycle)
    {
        _callback = (_, evt, _) =>
        {
            if (GetEventParameter(evt, 0x2D2D2D2D, 0x686B6964, 0, 8, 0, out var id) != 0 || id.Signature != Signature) return -9874;
            if (id.Id == 1) Dispatcher.UIThread.Post(hide);
            else if (id.Id == 2) Dispatcher.UIThread.Post(cycle);
            return 0;
        };
        var spec = new EventType { Class = 0x6B657962, Kind = 6 };
        var target = GetApplicationEventTarget();
        var installed = InstallEventHandler(target, _callback, 1, ref spec, 0, out _handler);
        if (installed != 0) { Status = $"Hotkey handler: {installed}"; return; }
        var h = RegisterEventHotKey(4, 1u << 11, new HotKeyId { Signature = Signature, Id = 1 }, target, 0, out _hide);
        var c = RegisterEventHotKey(8, 1u << 11, new HotKeyId { Signature = Signature, Id = 2 }, target, 0, out _cycle);
        Succeeded = h == 0 && c == 0;
        Status = $"Option+H: {(h == 0 ? "OK" : h)}; Option+C: {(c == 0 ? "OK" : c)}";
    }
    public void Dispose()
    {
        if (_hide != 0) { UnregisterEventHotKey(_hide); _hide = 0; }
        if (_cycle != 0) { UnregisterEventHotKey(_cycle); _cycle = 0; }
        if (_handler != 0) { RemoveEventHandler(_handler); _handler = 0; }
        GC.KeepAlive(_callback);
    }
    [StructLayout(LayoutKind.Sequential)] private struct EventType { public uint Class, Kind; }
    [StructLayout(LayoutKind.Sequential)] private struct HotKeyId { public uint Signature, Id; }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Handler(nint next, nint evt, nint data);
    [DllImport(Carbon)] private static extern nint GetApplicationEventTarget();
    [DllImport(Carbon)] private static extern int InstallEventHandler(nint target, Handler handler, uint count, ref EventType types, nint data, out nint result);
    [DllImport(Carbon)] private static extern int RegisterEventHotKey(uint key, uint modifiers, HotKeyId id, nint target, uint options, out nint result);
    [DllImport(Carbon)] private static extern int GetEventParameter(nint evt, uint name, uint type, nint actualType, uint size, nint actualSize, out HotKeyId result);
    [DllImport(Carbon)] private static extern int UnregisterEventHotKey(nint hotkey);
    [DllImport(Carbon)] private static extern int RemoveEventHandler(nint handler);
}
