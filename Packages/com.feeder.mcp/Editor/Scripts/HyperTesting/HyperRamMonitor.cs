#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace Feeder.MCP.Editor.HyperTesting
{
    // samples system and per-clone RAM into Journal/ram-samples.csv while any clone runs
    [InitializeOnLoad]
    public static class HyperRamMonitor
    {
        static Timer? _timer;
        static int _reserveMb;
        static DateTime _lastAlertUtc = DateTime.MinValue;

        static HyperRamMonitor()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
            if (!HyperTestingActivation.IsEnabled || File.Exists(Path.Combine(HyperTestingPaths.ProjectRoot, HyperTestingPaths.MarkerFileName)))
                return;
            if (HyperPoolState.Load().clones.Any(c => HyperClonePool.IsCloneProcess(c.pid)))
            {
                var config = HyperTestingConfig.Load();
                EnsureRunning(config.ramSampleIntervalSec, config.ramReserveMb);
            }
        }

        public static void EnsureRunning(int intervalSec, int reserveMb)
        {
            _reserveMb = reserveMb;
            if (_timer != null)
                return;
            var period = TimeSpan.FromSeconds(Math.Max(5, intervalSec));
            _timer = new Timer(_ => SampleSafe(), null, TimeSpan.Zero, period);
        }

        static void Stop()
        {
            _timer?.Dispose();
            _timer = null;
        }

        static void SampleSafe()
        {
            try
            {
                Sample();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{HyperTestingActivation.LogPrefix} RAM sample failed: {ex.Message}");
            }
        }

        public static string Sample()
        {
            var clones = HyperPoolState.Load().clones.OrderBy(c => c.index).ToList();
            var live = clones.Select(c => (c.index, memory: HyperSystemMemory.ReadProcess(c.pid))).Where(c => c.memory.alive).ToList();
            if (live.Count == 0)
            {
                Stop();
                return "no clone running";
            }

            var (total, available, load) = HyperSystemMemory.Read();
            var origin = HyperSystemMemory.CurrentProcessMb();
            var perClone = string.Join(";", live.Select(c => $"hc{c.index}={c.memory.workingSetMb}/{c.memory.peakMb}"));
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{total},{available},{load},{origin},{live.Count},{live.Sum(c => c.memory.workingSetMb)},{perClone}";

            Directory.CreateDirectory(HyperTestingPaths.Journal);
            if (!File.Exists(HyperTestingPaths.RamCsv))
                File.WriteAllText(HyperTestingPaths.RamCsv, "time,totalMb,availableMb,loadPercent,originEditorMb,clones,clonesTotalMb,perClone(workingSet/peak)\n");
            File.AppendAllText(HyperTestingPaths.RamCsv, line + "\n");

            if (available >= 0 && available < _reserveMb && DateTime.UtcNow - _lastAlertUtc > TimeSpan.FromMinutes(5))
            {
                _lastAlertUtc = DateTime.UtcNow;
                var alert = $"LOW RAM: {available} MB free (reserve {_reserveMb} MB), {live.Count} clones using {live.Sum(c => c.memory.workingSetMb)} MB [{perClone}]. Stop a clone before starting anything heavy.";
                Debug.LogWarning($"{HyperTestingActivation.LogPrefix} {alert}");
                HyperJournal.AppendAuto(HyperTestingPaths.RamNotes, alert);
            }
            return line;
        }
    }
}
