#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Feeder.ReflectorNet.Utils;
using Debug = UnityEngine.Debug;

namespace Feeder.MCP.Editor.HyperTesting
{
    // creates, launches, refreshes and stops the clone editors; every entry point refuses unless the user enabled the mode
    public static class HyperClonePool
    {
        static readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        static readonly string[] LibraryExcludedDirs = { "Bee", "MatrixHyperTesting" };
        static readonly string[] LibraryExcludedFiles = { "*-lock", "EditorInstance.json", "ilpp.pid" };

        sealed class Context
        {
            public string OriginRoot = string.Empty;
            public string OriginDataPath = string.Empty;
            public string UnityPath = string.Empty;
            public string PackageVersion = string.Empty;
            public string ClonesRoot = string.Empty;
            public HyperTestingConfig Config = new HyperTestingConfig();
        }

        static Task<Context?> CaptureAsync() => MainThread.Instance.RunAsync<Context?>(() =>
        {
            if (!HyperTestingActivation.IsEnabled)
                return null;
            var context = new Context
            {
                OriginRoot = HyperTestingPaths.ProjectRoot,
                OriginDataPath = HyperTestingPaths.DataPath,
                UnityPath = EditorApplication.applicationPath,
                PackageVersion = HyperTestingActivation.PackageVersion,
                Config = HyperTestingConfig.Load(),
            };
            context.ClonesRoot = context.Config.ResolvedClonesRoot(context.OriginRoot);
            return context;
        });

        public static async Task<bool> IsAvailableAsync() => await CaptureAsync() != null;

        static string Refusal =>
            File.Exists(Path.Combine(HyperTestingPaths.ProjectRoot, HyperTestingPaths.MarkerFileName))
                ? "This editor is itself a Hyper Testing clone. Pool commands run only in the original project."
                : $"{HyperTestingActivation.ModeName} is OFF for this project. Only the user can enable it in Tools > Feeder > Hyper Testing (Experimental). Do not try to enable it; ask the user.";

        public static async Task<string> UpAsync(int count)
        {
            var ctx = await CaptureAsync();
            if (ctx == null)
                return Refusal;
            count = Mathf.Clamp(count, 1, Math.Max(1, ctx.Config.maxClones));

            await _gate.WaitAsync();
            try
            {
                var log = new StringBuilder();
                for (var index = 1; index <= count; index++)
                {
                    var line = await Task.Run(() => BringUp(ctx, index));
                    log.AppendLine(line);
                    if (line.StartsWith("STOP"))
                        break;
                }
                HyperRamMonitor.EnsureRunning(ctx.Config.ramSampleIntervalSec, ctx.Config.ramReserveMb);
                await WriteAgentFilesAsync();
                return log.ToString() + "\n" + StatusText(ctx);
            }
            finally
            {
                _gate.Release();
            }
        }

        static string BringUp(Context ctx, int index)
        {
            var state = HyperPoolState.Load();
            var entry = state.Find(index);
            if (entry != null && IsCloneProcess(entry.pid))
                return $"hc{index}: already running (pid {entry.pid}, port {entry.port})";

            var gate = RamGate(ctx.Config, state);
            if (gate != null)
            {
                HyperJournal.AppendAuto(HyperTestingPaths.RamNotes, $"RAM gate stopped starting hc{index}: {gate}");
                return $"STOP hc{index}: {gate}";
            }

            var root = Path.Combine(ctx.ClonesRoot, $"hc{index}");
            var watch = Stopwatch.StartNew();
            var port = entry?.port > 0 && !IsPortBusy(entry.port) ? entry.port : PickPort(ctx.Config.basePort + index, state);
            try
            {
                var created = PrepareClone(ctx, index, root, port);
                var prepared = watch.Elapsed.TotalSeconds;
                if (File.Exists(HyperTestingPaths.StatusFile(root)))
                    File.Delete(HyperTestingPaths.StatusFile(root));
                var pid = Launch(ctx, root);
                entry ??= new HyperPoolEntry { index = index };
                if (!state.clones.Contains(entry))
                    state.clones.Add(entry);
                entry.port = port;
                entry.pid = pid;
                entry.root = root;
                entry.state = "starting";
                state.Save();

                var ready = WaitUntil(root, pid, TimeSpan.FromMinutes(created ? 20 : 8), s => s.pid == pid && s.runnerActive && !s.isCompiling && !s.isUpdating, out var status, out var why);
                entry.state = ready ? "ready" : "unhealthy";
                entry.note = ready ? string.Empty : why;
                var fresh = HyperPoolState.Load();
                fresh.clones.RemoveAll(c => c.index == index);
                fresh.clones.Add(entry);
                fresh.Save();

                var summary = $"hc{index}: {(ready ? "READY" : "NOT READY - " + why)} port {port} pid {pid}, prepare {prepared:0}s{(created ? " (new clone, Library copied)" : "")}, total {watch.Elapsed.TotalSeconds:0}s, RAM {status?.workingSetMb ?? 0} MB";
                HyperJournal.AppendAuto(HyperTestingPaths.CloneIssues, (ready ? "info: " : "PROBLEM: ") + summary);
                return summary;
            }
            catch (Exception ex)
            {
                HyperJournal.AppendAuto(HyperTestingPaths.CloneIssues, $"PROBLEM: hc{index} failed to come up: {ex.Message}");
                return $"STOP hc{index}: {ex.Message}";
            }
        }

        static string? RamGate(HyperTestingConfig config, HyperPoolState state)
        {
            var (_, available, _) = HyperSystemMemory.Read();
            if (available < 0)
                return null;
            var measuredPeak = state.clones.Select(c => HyperSystemMemory.ReadProcess(c.pid)).Where(p => p.alive).Select(p => p.peakMb).DefaultIfEmpty(0).Max();
            var needed = Math.Max(config.ramPerCloneEstimateMb, (long)(measuredPeak * 1.2));
            return available - config.ramReserveMb < needed
                ? $"only {available} MB free; keeping {config.ramReserveMb} MB reserve leaves less than the {needed} MB one more editor needs"
                : null;
        }

        static bool PrepareClone(Context ctx, int index, string root, int port)
        {
            var isNew = !Directory.Exists(Path.Combine(root, "Library"));
            Directory.CreateDirectory(root);

            Junction(Path.Combine(root, "Assets"), Path.Combine(ctx.OriginRoot, "Assets"));

            var packages = Path.Combine(root, "Packages");
            Directory.CreateDirectory(packages);
            foreach (var dir in Directory.GetDirectories(Path.Combine(ctx.OriginRoot, "Packages")))
                Junction(Path.Combine(packages, Path.GetFileName(dir)), dir);
            SyncPackageFiles(ctx.OriginRoot, root);

            Robocopy(Path.Combine(ctx.OriginRoot, "ProjectSettings"), Path.Combine(root, "ProjectSettings"), Array.Empty<string>(), Array.Empty<string>());
            if (isNew)
                Robocopy(Path.Combine(ctx.OriginRoot, "Library"), Path.Combine(root, "Library"), LibraryExcludedDirs, LibraryExcludedFiles);

            WriteCloneBridgeConfig(ctx.OriginRoot, root, port);

            var marker = new HyperCloneMarker
            {
                index = index,
                port = port,
                originRoot = ctx.OriginRoot,
                originDataPath = ctx.OriginDataPath,
                cloneRoot = root,
                createdUtc = DateTime.UtcNow.ToString("o"),
                packageVersion = ctx.PackageVersion,
                playTargetFrameRate = ctx.Config.playTargetFrameRate,
                playLowestQuality = ctx.Config.playLowestQuality,
                isolatePlayerPrefs = ctx.Config.isolatePlayerPrefs,
                closeSceneViews = ctx.Config.closeSceneViews,
            };
            File.WriteAllText(Path.Combine(root, HyperTestingPaths.MarkerFileName), JsonUtility.ToJson(marker, true));
            Directory.CreateDirectory(Path.Combine(root, "Logs"));
            return isNew;
        }

        static void Junction(string link, string target)
        {
            if (Directory.Exists(link))
            {
                if ((File.GetAttributes(link) & FileAttributes.ReparsePoint) == 0)
                    throw new IOException($"'{link}' exists as a real folder, not a junction. Remove it by hand after checking it holds nothing you need.");
                return;
            }
            var code = Run("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"", out var output);
            if (code != 0 || !Directory.Exists(link))
                throw new IOException($"mklink /J failed for '{link}': {output.Trim()}");
        }

        static bool SyncPackageFiles(string originRoot, string cloneRoot)
        {
            var changed = false;
            var originPackages = Path.Combine(originRoot, "Packages");
            foreach (var file in Directory.GetFiles(originPackages))
            {
                var text = Regex.Replace(File.ReadAllText(file), "\"file:([^\"]+)\"", m =>
                {
                    var value = m.Groups[1].Value;
                    if (Path.IsPathRooted(value))
                        return m.Value;
                    return $"\"file:{Path.GetFullPath(Path.Combine(originPackages, value)).Replace('\\', '/')}\"";
                });
                var destination = Path.Combine(cloneRoot, "Packages", Path.GetFileName(file));
                if (File.Exists(destination) && File.ReadAllText(destination) == text)
                    continue;
                File.WriteAllText(destination, text);
                changed = true;
            }
            return changed;
        }

        static void WriteCloneBridgeConfig(string originRoot, string cloneRoot, int port)
        {
            var source = Path.Combine(originRoot, "UserSettings", "Feeder-MCP-Config.json");
            var userSettings = Path.Combine(cloneRoot, "UserSettings");
            Directory.CreateDirectory(userSettings);
            var json = File.Exists(source) ? File.ReadAllText(source) : "{}";
            json = Regex.Replace(json, "\"host\"\\s*:\\s*\"[^\"]*\"", $"\"host\": \"http://localhost:{port}\"");
            json = Regex.Replace(json, "\"token\"\\s*:\\s*\"[^\"]*\"", $"\"token\": \"{Guid.NewGuid():N}\"");
            if (!json.Contains("\"host\""))
                json = json.TrimEnd().TrimEnd('}') + $",\"host\": \"http://localhost:{port}\"}}";
            File.WriteAllText(Path.Combine(userSettings, "Feeder-MCP-Config.json"), json);
        }

        static int Launch(Context ctx, string root)
        {
            var args = $"-projectPath \"{root}\" -logFile \"{Path.Combine(root, "Logs", "hyper-editor.log")}\" -silent-crashes";
            if (ctx.Config.jobWorkerCount > 0)
                args += $" -job-worker-count {ctx.Config.jobWorkerCount}";
            var info = new ProcessStartInfo(ctx.UnityPath, args) { UseShellExecute = false, WorkingDirectory = root };
            foreach (var key in info.Environment.Keys.Where(k => k.StartsWith("UNITY_MCP_", StringComparison.OrdinalIgnoreCase)).ToList())
                info.Environment.Remove(key);
            var process = Process.Start(info) ?? throw new IOException("Unity.exe did not start");
            if (ctx.Config.belowNormalPriority)
            {
                try { process.PriorityClass = ProcessPriorityClass.BelowNormal; }
                catch (Exception ex) { Debug.LogWarning($"{HyperTestingActivation.LogPrefix} could not lower priority: {ex.Message}"); }
            }
            return process.Id;
        }

        public static async Task<string> RefreshAsync()
        {
            var ctx = await CaptureAsync();
            if (ctx == null)
                return Refusal;
            await _gate.WaitAsync();
            try
            {
                var live = HyperPoolState.Load().clones.Where(c => IsCloneProcess(c.pid)).OrderBy(c => c.index).ToList();
                if (live.Count == 0)
                    return "No clone is running. Run action 'up' first.";

                using var compileSlots = new SemaphoreSlim(Math.Max(1, ctx.Config.maxParallelCompiles));
                var results = await Task.WhenAll(live.Select(entry => Task.Run(async () =>
                {
                    await compileSlots.WaitAsync();
                    try { return RefreshOne(ctx, entry); }
                    finally { compileSlots.Release(); }
                })));
                return string.Join("\n", results);
            }
            finally
            {
                _gate.Release();
            }
        }

        static string RefreshOne(Context ctx, HyperPoolEntry entry)
        {
            var watch = Stopwatch.StartNew();
            var settingsCode = Robocopy(Path.Combine(ctx.OriginRoot, "ProjectSettings"), Path.Combine(entry.root, "ProjectSettings"), Array.Empty<string>(), Array.Empty<string>());
            var packagesChanged = SyncPackageFiles(ctx.OriginRoot, entry.root);
            var id = SendCommand(entry.root, "refresh");
            var done = WaitUntil(entry.root, entry.pid, TimeSpan.FromSeconds(ctx.Config.compileTimeoutSec), s => s.lastCommandId == id, out var status, out var why);
            var notes = new List<string>();
            if ((settingsCode & 1) != 0)
                notes.Add("ProjectSettings re-synced from the original (restart the clone if a changed setting needs an editor restart)");
            if (packagesChanged)
                notes.Add("Packages manifest changed (package resolve ran)");
            var line = done
                ? $"hc{entry.index}: {(status!.compileFailed ? "COMPILE ERRORS" : "compiled clean")} in {watch.Elapsed.TotalSeconds:0}s, RAM {status.workingSetMb} MB {string.Join("; ", notes)}"
                : $"hc{entry.index}: refresh NOT confirmed after {watch.Elapsed.TotalSeconds:0}s - {why}";
            if (!done || status!.compileFailed)
                HyperJournal.AppendAuto(HyperTestingPaths.CloneIssues, "PROBLEM: " + line);
            return line;
        }

        public static async Task<string> DownAsync(bool purge)
        {
            var ctx = await CaptureAsync();
            if (ctx == null)
                return Refusal;
            await _gate.WaitAsync();
            try
            {
                var state = HyperPoolState.Load();
                var lines = await Task.Run(() => state.clones.OrderBy(c => c.index).Select(entry => StopOne(entry, purge)).ToList());
                if (purge)
                    state.clones.Clear();
                else
                    state.clones.ForEach(c => { c.pid = 0; c.state = "stopped"; });
                state.Save();
                HyperTestingWorkspace.WriteAgentFiles(state);
                return string.Join("\n", lines);
            }
            finally
            {
                _gate.Release();
            }
        }

        static string StopOne(HyperPoolEntry entry, bool purge)
        {
            var result = $"hc{entry.index}: ";
            if (IsCloneProcess(entry.pid))
            {
                SendCommand(entry.root, "quit");
                if (!WaitForExit(entry.pid, TimeSpan.FromSeconds(45)))
                {
                    try { using var p = Process.GetProcessById(entry.pid); p.Kill(); }
                    catch (Exception) { }
                    result += "killed after 45 s (did not quit on command)";
                    HyperJournal.AppendAuto(HyperTestingPaths.CloneIssues, $"PROBLEM: hc{entry.index} ignored the quit command and was killed");
                }
                else
                {
                    result += "stopped";
                }
            }
            else
            {
                result += "was not running";
            }

            if (purge && Directory.Exists(entry.root))
                result += "; " + SafeDeleteClone(entry.root);
            return result;
        }

        // junctions are unlinked one by one first: a recursive delete through a junction would wipe the original project
        static string SafeDeleteClone(string root)
        {
            var links = new List<string> { Path.Combine(root, "Assets") };
            var packages = Path.Combine(root, "Packages");
            if (Directory.Exists(packages))
                links.AddRange(Directory.GetDirectories(packages));

            foreach (var link in links.Where(Directory.Exists))
            {
                if ((File.GetAttributes(link) & FileAttributes.ReparsePoint) == 0)
                    continue;
                Run("cmd.exe", $"/c rmdir \"{link}\"", out _);
                if (Directory.Exists(link))
                    return $"purge ABORTED: could not unlink junction '{link}'";
            }

            var leftover = FindReparsePoint(root);
            if (leftover != null)
                return $"purge ABORTED: '{leftover}' is still a link; delete the clone by hand";

            Directory.Delete(root, true);
            return "folder deleted";
        }

        static string? FindReparsePoint(string folder)
        {
            foreach (var dir in Directory.GetDirectories(folder))
            {
                if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0)
                    return dir;
                var nested = FindReparsePoint(dir);
                if (nested != null)
                    return nested;
            }
            return null;
        }

        public static async Task<string> StatusAsync()
        {
            var ctx = await CaptureAsync();
            return ctx == null ? Refusal : StatusText(ctx) + JobText();
        }

        static Task<string>? _job;
        static string _jobName = string.Empty;
        static DateTime _jobStartedUtc;

        static string LastJobFile => Path.Combine(Path.GetDirectoryName(HyperTestingPaths.PoolFile)!, "last-job.txt");

        // long pool work runs in the background so an MCP call never hits a client timeout; poll with status
        public static string StartJob(string name, Func<Task<string>> work)
        {
            if (_job != null && !_job.IsCompleted)
                return $"Job '{_jobName}' is still running ({(DateTime.UtcNow - _jobStartedUtc).TotalSeconds:0}s). Poll action 'status' until it finishes.";
            _jobName = name;
            _jobStartedUtc = DateTime.UtcNow;
            var lastJobFile = LastJobFile;
            _job = Task.Run(async () =>
            {
                string result;
                try { result = await work(); }
                catch (Exception ex) { result = "FAILED: " + ex; }
                Directory.CreateDirectory(Path.GetDirectoryName(lastJobFile)!);
                File.WriteAllText(lastJobFile, $"{name} finished {DateTime.Now:yyyy-MM-dd HH:mm:ss} after {(DateTime.UtcNow - _jobStartedUtc).TotalSeconds:0}s" + Environment.NewLine + result);
                return result;
            });
            return $"Started job '{name}'. It runs in the background (clone creation can take minutes). Poll action 'status' every 30-60 s.";
        }

        static string JobText()
        {
            if (_job != null && !_job.IsCompleted)
                return $"{Environment.NewLine}Job '{_jobName}' RUNNING for {(DateTime.UtcNow - _jobStartedUtc).TotalSeconds:0}s.";
            return File.Exists(LastJobFile) ? Environment.NewLine + "Last job: " + File.ReadAllText(LastJobFile) : string.Empty;
        }

        static string StatusText(Context ctx)
        {
            var config = ctx.Config;
            var (total, available, load) = HyperSystemMemory.Read();
            var text = new StringBuilder();
            text.AppendLine($"{HyperTestingActivation.ModeName}: ON (package {ctx.PackageVersion}), clones folder {ctx.ClonesRoot}");
            text.AppendLine($"System RAM: {available} MB free of {total} MB ({load}% used), reserve {config.ramReserveMb} MB, original editor {HyperSystemMemory.CurrentProcessMb()} MB");
            var state = HyperPoolState.Load();
            if (state.clones.Count == 0)
                text.AppendLine("No clones yet. Action 'up' creates and starts them.");
            foreach (var entry in state.clones.OrderBy(c => c.index))
            {
                var alive = IsCloneProcess(entry.pid);
                var memory = HyperSystemMemory.ReadProcess(entry.pid);
                var status = ReadStatus(entry.root);
                var age = status == null ? -1 : DateTimeOffset.UtcNow.ToUnixTimeSeconds() - status.writtenUnix;
                text.Append($"hc{entry.index}: {(alive ? "running" : "not running")}, port {entry.port}, pid {entry.pid}, mcp http://localhost:{entry.port}/mcp, agent {HyperTestingPaths.AgentPrefix}{entry.index}");
                if (alive)
                    text.Append($", RAM {memory.workingSetMb} MB (peak {memory.peakMb} MB)");
                if (status != null && alive)
                {
                    text.Append($", runner {(status.runnerActive ? "active" : "INACTIVE " + status.runnerProblem)}, playing {status.isPlaying}, compiling {status.isCompiling}, compile errors {status.compileFailed}, status age {age}s");
                    if (status.blockedWrites > 0)
                        text.Append($", BLOCKED WRITES {status.blockedWrites}");
                }
                if (!string.IsNullOrEmpty(entry.note))
                    text.Append($", note: {entry.note}");
                text.AppendLine();
            }
            return text.ToString();
        }

        static string SendCommand(string root, string command)
        {
            var id = Guid.NewGuid().ToString("N").Substring(0, 12);
            Directory.CreateDirectory(HyperTestingPaths.ChannelFolder(root));
            WriteAtomic(HyperTestingPaths.CommandFile(root), JsonUtility.ToJson(new HyperCloneCommand { id = id, command = command }));
            return id;
        }

        internal static void WriteAtomic(string path, string text)
        {
            var temp = path + ".tmp";
            File.WriteAllText(temp, text);
            if (File.Exists(path))
                File.Replace(temp, path, null);
            else
                File.Move(temp, path);
        }

        internal static HyperCloneStatus? ReadStatus(string root)
        {
            try
            {
                var path = HyperTestingPaths.StatusFile(root);
                return File.Exists(path) ? JsonUtility.FromJson<HyperCloneStatus>(File.ReadAllText(path)) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        static bool WaitUntil(string root, int pid, TimeSpan timeout, Func<HyperCloneStatus, bool> condition, out HyperCloneStatus? status, out string why)
        {
            var deadline = DateTime.UtcNow + timeout;
            status = null;
            while (DateTime.UtcNow < deadline)
            {
                if (!IsCloneProcess(pid))
                {
                    why = "the clone editor process exited (see its Logs/hyper-editor.log)";
                    return false;
                }
                status = ReadStatus(root);
                if (status != null && !status.runnerActive && !string.IsNullOrEmpty(status.runnerProblem))
                {
                    why = "runner refused: " + status.runnerProblem;
                    return false;
                }
                if (status != null && condition(status))
                {
                    why = string.Empty;
                    return true;
                }
                Thread.Sleep(2000);
            }
            why = $"timed out after {timeout.TotalSeconds:0}s (an unfocused editor may be throttled; status age matters)";
            return false;
        }

        static bool WaitForExit(int pid, TimeSpan timeout)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                return process.WaitForExit((int)timeout.TotalMilliseconds);
            }
            catch (Exception)
            {
                return true;
            }
        }

        internal static bool IsCloneProcess(int pid)
        {
            if (pid <= 0)
                return false;
            try
            {
                using var process = Process.GetProcessById(pid);
                return !process.HasExited && process.ProcessName.IndexOf("Unity", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        static int PickPort(int preferred, HyperPoolState state)
        {
            var taken = new HashSet<int>(state.clones.Select(c => c.port));
            for (var port = preferred; port < preferred + 500; port += 10)
            {
                if (!taken.Contains(port) && !IsPortBusy(port))
                    return port;
            }
            throw new IOException($"no free port near {preferred}");
        }

        static bool IsPortBusy(int port) =>
            IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(endpoint => endpoint.Port == port);

        static int Robocopy(string source, string destination, string[] excludedDirs, string[] excludedFiles)
        {
            source = Path.GetFullPath(source);
            destination = Path.GetFullPath(destination);
            var args = new StringBuilder($"\"{source}\" \"{destination}\" /MIR /MT:16 /R:1 /W:1 /NFL /NDL /NP /NJH /NJS");
            if (excludedDirs.Length > 0)
                args.Append(" /XD ").Append(string.Join(" ", excludedDirs.Select(d => $"\"{Path.Combine(source, d)}\"")));
            if (excludedFiles.Length > 0)
                args.Append(" /XF ").Append(string.Join(" ", excludedFiles.Select(f => $"\"{f}\"")));
            var code = Run("robocopy.exe", args.ToString(), out var output);
            if (code >= 8)
                throw new IOException($"robocopy '{source}' -> '{destination}' failed with code {code}: {output.Trim()}");
            return code;
        }

        static int Run(string file, string args, out string output)
        {
            var info = new ProcessStartInfo(file, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var process = Process.Start(info)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            output = stdout.Result + stderr.Result;
            return process.ExitCode;
        }

        static Task WriteAgentFilesAsync()
        {
            var state = HyperPoolState.Load();
            return MainThread.Instance.RunAsync(() => HyperTestingWorkspace.WriteAgentFiles(state));
        }
    }
}
