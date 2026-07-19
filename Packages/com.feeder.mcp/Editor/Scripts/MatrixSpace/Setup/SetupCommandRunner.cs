#nullable enable

using System;
using System.Diagnostics;
using System.IO;

namespace Feeder.MCP.Editor.MatrixSpace.Setup
{
    /// <summary>
    /// Runs an install/prerequisite command headlessly, streaming stdout+stderr line by line
    /// so long npm/winget installs surface live progress in the setup log. `.cmd`/`.bat`
    /// targets are wrapped in cmd.exe via <see cref="CliLaunchInfo"/>.
    /// </summary>
    public static class SetupCommandRunner
    {
        /// <summary>Returns the process exit code, or -1 when it could not be started / timed out.</summary>
        public static int Run(string fileName, string arguments, Action<string> onLine, int timeoutMs = 600000)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                var launch = new CliLaunchInfo(fileName);
                onLine($"Running: {fileName} {arguments}");
                var startInfo = new ProcessStartInfo
                {
                    FileName = launch.FileName,
                    Arguments = launch.BuildArguments(arguments),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                // Installers and npm shims need the freshly installed tool dirs on PATH.
                CliEnvironment.Apply(startInfo);

                using var process = new Process { StartInfo = startInfo };
                process.OutputDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                        onLine(e.Data.Trim());
                };
                process.ErrorDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                        onLine(e.Data.Trim());
                };

                if (!process.Start())
                {
                    onLine("Process failed to start (Process.Start returned false).");
                    return -1;
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (!process.WaitForExit(timeoutMs))
                {
                    onLine($"Timed out after {timeoutMs / 1000}s — killing process.");
                    try { process.Kill(); } catch (Exception) { /* already gone */ }
                    return -1;
                }

                // Flushes the async output readers before ExitCode is read.
                process.WaitForExit();
                onLine($"Exit code: {process.ExitCode} ({stopwatch.Elapsed.TotalSeconds:F1}s)");
                return process.ExitCode;
            }
            catch (Exception ex)
            {
                onLine($"Failed to run '{fileName} {arguments}': {ex.Message}");
                return -1;
            }
        }

        /// <summary>PATH lookup via where.exe; also probes the given absolute fallback paths.</summary>
        public static string? Find(string exeName, params string[] fallbackPaths)
        {
            var resolved = CliPathResolver.ResolveExecutable(exeName);
            if (resolved != null)
                return resolved;

            foreach (var candidate in fallbackPaths)
            {
                if (!string.IsNullOrEmpty(candidate) && File.Exists(candidate))
                    return candidate;
            }

            return null;
        }

        /// <summary>%APPDATA%\npm\&lt;name&gt;.cmd — where npm -g shims land on Windows.</summary>
        public static string NpmShimPath(string exeName)
            => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", exeName + ".cmd");

        /// <summary>%USERPROFILE%\.local\bin\&lt;file&gt; — used by cursor/uv style installers.</summary>
        public static string LocalBinPath(string fileName)
            => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", fileName);
    }
}
