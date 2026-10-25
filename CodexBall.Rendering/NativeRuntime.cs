using System.Reflection;
using System.Runtime.InteropServices;
using Live2DCSharpSDK.Framework;

namespace CodexBall.Rendering;

public static class NativeRuntime
{
    public static void Register()
        => NativeLibrary.SetDllImportResolver(typeof(CubismFramework).Assembly, Resolve);

    private static nint Resolve(string name, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (name != "Live2DCubismCore") return 0;
        var arch = RuntimeInformation.ProcessArchitecture;
        var relative = OperatingSystem.IsWindows() && arch == Architecture.X64 ? "win-x64/native/Live2DCubismCore.dll"
            : OperatingSystem.IsLinux() && arch == Architecture.X64 ? "linux-x64/native/libLive2DCubismCore.so"
            : OperatingSystem.IsMacOS() && arch is Architecture.X64 or Architecture.Arm64 ? "osx/native/libLive2DCubismCore.dylib"
            : throw new PlatformNotSupportedException($"Live2D probe runtime unavailable: {RuntimeInformation.RuntimeIdentifier}");
        return NativeLibrary.Load(Path.Combine(AppContext.BaseDirectory, "runtimes", relative));
    }
}
