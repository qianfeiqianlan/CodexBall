using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CodexBall.App.Services;

public sealed class CodexProcessMonitor
{
    private const string CodexProcessName = "codex";

    public bool IsCodexRunning()
    {
        var ownDescendants = GetDescendantProcessIds(Environment.ProcessId);

        foreach (var process in Process.GetProcessesByName(CodexProcessName))
        {
            using (process)
            {
                if (process.Id != Environment.ProcessId && !ownDescendants.Contains(process.Id))
                {
                    return true;
                }
            }
        }

        return false;
    }

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

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(SnapshotFlags flags, uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool Process32First(IntPtr snapshot, ref ProcessEntry32 processEntry);

    [DllImport("kernel32.dll", SetLastError = true)]
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
