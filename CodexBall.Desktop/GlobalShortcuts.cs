using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace CodexBall.Desktop;

// Deliberately registers only two shortcuts; no global keyboard/mouse monitoring.
internal sealed class GlobalShortcuts : IDisposable
{
    private readonly Window _window;
    private readonly Action _hide, _cycle;
    private nint _handle;
    private MacHotKeys? _mac;
    public bool Succeeded { get; private set; }
    public string Status { get; private set; } = "";
    public GlobalShortcuts(Window window, Action hide, Action cycle)
    {
        _window = window; _hide = hide; _cycle = cycle;
        if (OperatingSystem.IsMacOS())
        {
            _mac = new MacHotKeys(hide, cycle); Status = _mac.Status; Succeeded = _mac.Succeeded; return;
        }
        if (!OperatingSystem.IsWindows())
        {
            Status = "Global shortcuts unsupported on this platform";
            return;
        }
        _handle = window.TryGetPlatformHandle()!.Handle;
        Win32Properties.AddWndProcHookCallback(window, WndProc);
        var hideOk = RegisterHotKey(_handle, 0xCB01, 0x4001, 0x48);
        var hideError = hideOk ? 0 : Marshal.GetLastWin32Error();
        var cycleOk = RegisterHotKey(_handle, 0xCB02, 0x4001, 0x43);
        var cycleError = cycleOk ? 0 : Marshal.GetLastWin32Error();
        Succeeded = hideOk && cycleOk;
        Status = $"Alt+H: {(hideOk ? "OK" : $"error {hideError}")}; Alt+C: {(cycleOk ? "OK" : $"error {cycleError}")}";
    }
    private nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg != 0x0312) return 0;
        if (wParam == 0xCB01) { _hide(); handled = true; }
        if (wParam == 0xCB02) { _cycle(); handled = true; }
        return 0;
    }
    public void Dispose()
    {
        _mac?.Dispose();
        if (_handle == 0) return;
        UnregisterHotKey(_handle, 0xCB01); UnregisterHotKey(_handle, 0xCB02);
        Win32Properties.RemoveWndProcHookCallback(_window, WndProc);
        _handle = 0;
    }
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint window, int id);
}
