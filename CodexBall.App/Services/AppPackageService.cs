using System.Runtime.InteropServices;
using System.Text;

namespace CodexBall.App.Services;

public static class AppPackageService
{
    private const int ErrorInsufficientBuffer = 122;
    private const int AppModelErrorNoPackage = 15700;

    public static bool IsPackaged()
    {
        var length = 0;
        var result = GetCurrentPackageFullName(ref length, null);
        return result is 0 or ErrorInsufficientBuffer;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, StringBuilder? packageFullName);
}
