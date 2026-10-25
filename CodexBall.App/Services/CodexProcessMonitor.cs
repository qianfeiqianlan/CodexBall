using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CodexBall.App.Services;

public sealed class CodexProcessMonitor
{
    private const string CodexProcessName = "codex";

    public bool IsCodexRunning()
    {
        var ownDescendants = GetDescendantProcessIds(Environment.ProcessId);

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                string name;
                try { name = process.ProcessName; }
                catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { continue; }
                if (IsExternalCodexProcess(process.Id, name, Environment.ProcessId, ownDescendants))
                {
                    return true;
                }
            }
        }

        return false;
    }

    internal static bool IsExternalCodexProcess(int id, string name, int ownId, ISet<int> descendants)
        => name.Equals(CodexProcessName, StringComparison.OrdinalIgnoreCase)
            && id != ownId && !descendants.Contains(id);

    private static HashSet<int> GetDescendantProcessIds(int processId)
    {
        var parentByProcessId = GetParentProcessMap();
        var descendants = new HashSet<int>();
        var added = true;

        while (added)
        {
            added = false;
            foreach (var (childProcessId, parentProcessId) in parentByProcessId)
            {
                if (descendants.Contains(childProcessId))
                {
                    continue;
                }

                if (parentProcessId == processId || descendants.Contains(parentProcessId))
                {
                    descendants.Add(childProcessId);
                    added = true;
                }
            }
        }

        return descendants;
    }

    private static Dictionary<int, int> GetParentProcessMap()
    {
        if (!OperatingSystem.IsWindows()) return ReadUnixParents();
        var snapshot = CreateToolhelp32Snapshot(SnapshotFlags.Process, 0);
        if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1))
        {
            return [];
        }

        try
        {
            var entry = new ProcessEntry32 { Size = (uint)Marshal.SizeOf<ProcessEntry32>() };
            var parents = new Dictionary<int, int>();

            if (!Process32First(snapshot, ref entry))
            {
                return parents;
            }

            do
            {
                parents[(int)entry.ProcessId] = (int)entry.ParentProcessId;
            }
            while (Process32Next(snapshot, ref entry));

            return parents;
        }
        finally
        {
            CloseHandle(snapshot);
        }
    }

    private static Dictionary<int, int> ReadUnixParents()
    {
        var parents = new Dictionary<int, int>();
        try
        {
            using var ps = Process.Start(new ProcessStartInfo("/bin/ps", "-axo pid=,ppid=") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true });
            if (ps is null) return parents;
            var output = ps.StandardOutput.ReadToEnd();
            ps.WaitForExit(2000);
            foreach (var line in output.Split('\n'))
            {
                var columns = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (columns.Length == 2 && int.TryParse(columns[0], out var child) && int.TryParse(columns[1], out var parent)) parents[child] = parent;
            }
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { }
        return parents;
    }
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(SnapshotFlags flags, uint processId);

    [DllImport("kernel32.dll", EntryPoint = "Process32FirstW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Process32First(IntPtr snapshot, ref ProcessEntry32 processEntry);

    [DllImport("kernel32.dll", EntryPoint = "Process32NextW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry32 processEntry);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [Flags]
    private enum SnapshotFlags : uint
    {
        Process = 0x00000002
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct ProcessEntry32
    {
        public uint Size;
        public uint Usage;
        public uint ProcessId;
        public IntPtr DefaultHeapId;
        public uint ModuleId;
        public uint Threads;
        public uint ParentProcessId;
        public int PriClassBase;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string ExeFile;
    }
}
