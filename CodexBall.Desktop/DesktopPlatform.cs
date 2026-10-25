using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Microsoft.Win32;

namespace CodexBall.Desktop;

internal static class DesktopPlatform
{
    public static bool IsPackaged => OperatingSystem.IsWindows() ? HasWindowsPackage() : File.Exists(Path.Combine(AppContext.BaseDirectory, "store-managed"));
    private static bool HasWindowsPackage()
    {
        uint length = 0; return GetCurrentPackageFullName(ref length, 0) != 15700;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern int GetCurrentPackageFullName(ref uint length, nint name);
    public static string AppHost => Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "CodexBall.Desktop.exe" : "CodexBall.Desktop");
    public static void OpenHome() => OpenUrl("https://github.com/qianfeiqianlan/CodexBall");
    public static void OpenUrl(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    private static string LaunchAgent => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library/LaunchAgents/com.codexball.desktop.plist");
    private static string StartupCommand => $"\"{AppHost}\"";
    public static bool StartupEnabled()
    {
        if (OperatingSystem.IsMacOS()) return File.Exists(LaunchAgent);
        if (!OperatingSystem.IsWindows()) return false;
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        return string.Equals(key?.GetValue("CodexBall") as string, StartupCommand, StringComparison.OrdinalIgnoreCase);
    }
    public static void SetStartup(bool enabled)
    {
        if (IsPackaged) return;
        if (!File.Exists(AppHost)) throw new InvalidOperationException("Publish the application before enabling launch at startup.");
        if (OperatingSystem.IsWindows())
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if (enabled) key.SetValue("CodexBall", StartupCommand); else key.DeleteValue("CodexBall", false);
        }
        else if (OperatingSystem.IsMacOS())
        {
            // RunAtLoad starts at next login; toggling does not spawn another copy now.
            if (enabled)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LaunchAgent)!);
                var document = new XDocument(new XDeclaration("1.0", "UTF-8", null), new XDocumentType("plist", "-//Apple//DTD PLIST 1.0//EN", "http://www.apple.com/DTDs/PropertyList-1.0.dtd", null),
                    new XElement("plist", new XAttribute("version", "1.0"), new XElement("dict", new XElement("key", "Label"), new XElement("string", "com.codexball.desktop"), new XElement("key", "ProgramArguments"), new XElement("array", new XElement("string", AppHost)), new XElement("key", "RunAtLoad"), new XElement("true"), new XElement("key", "ProcessType"), new XElement("string", "Interactive"))));
                document.Save(LaunchAgent);
            }
            else if (File.Exists(LaunchAgent)) File.Delete(LaunchAgent);
        }
    }
}
