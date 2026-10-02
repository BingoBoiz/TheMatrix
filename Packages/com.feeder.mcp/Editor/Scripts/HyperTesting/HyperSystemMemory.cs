#nullable enable
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Feeder.MCP.Editor.HyperTesting
{
    public static class HyperSystemMemory
    {
        [StructLayout(LayoutKind.Sequential)]
        struct MemoryStatusEx
        {
            public uint length;
            public uint memoryLoad;
            public ulong totalPhys;
            public ulong availPhys;
            public ulong totalPageFile;
            public ulong availPageFile;
            public ulong totalVirtual;
            public ulong availVirtual;
            public ulong availExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

        public static (long totalMb, long availableMb, int loadPercent) Read()
        {
            try
            {
                var status = new MemoryStatusEx { length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
                if (!GlobalMemoryStatusEx(ref status))
                    return (-1, -1, -1);
                return ((long)(status.totalPhys / 1048576), (long)(status.availPhys / 1048576), (int)status.memoryLoad);
            }
            catch (Exception)
            {
                return (-1, -1, -1);
            }
        }

        public static (long workingSetMb, long peakMb, bool alive) ReadProcess(int pid)
        {
            if (pid <= 0)
                return (0, 0, false);
            try
            {
                using var process = Process.GetProcessById(pid);
                if (process.HasExited)
                    return (0, 0, false);
                process.Refresh();
                return (process.WorkingSet64 / 1048576, process.PeakWorkingSet64 / 1048576, true);
            }
            catch (Exception)
            {
                return (0, 0, false);
            }
        }

        public static long CurrentProcessMb()
        {
            using var process = Process.GetCurrentProcess();
            return process.WorkingSet64 / 1048576;
        }
    }
}
