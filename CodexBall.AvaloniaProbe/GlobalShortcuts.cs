using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace CodexBall.AvaloniaProbe;

// Deliberately registers only two shortcuts; no global keyboard/mouse monitoring.
internal sealed class GlobalShortcuts : IDisposable
{
    private readonly Window _window;
    private readonly Action _hide, _cycle;
    private nint _handle;
    public string Status { get; private set; } = "";
    public GlobalShortcuts(Window window, Action hide, Action cycle)
    {
        _window = window; _hide = hide; _cycle = cycle;
        if (!OperatingSystem.IsWindows())
        {
            Status = AppLocalization.Text("此验证版尚未接入本平台全局快捷键；请使用验证面板或托盘。", "Global shortcuts are not implemented on this platform in this probe; use the panel or tray.");
            return;
        }
        _handle = window.TryGetPlatformHandle()!.Handle;
        Win32Properties.AddWndProcHookCallback(window, WndProc);
        var hideOk = RegisterHotKey(_handle, 0xCB01, 0x4001, 0x48);
        var hideError = hideOk ? 0 : Marshal.GetLastWin32Error();
        var cycleOk = RegisterHotKey(_handle, 0xCB02, 0x4001, 0x43);
        var cycleError = cycleOk ? 0 : Marshal.GetLastWin32Error();
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
