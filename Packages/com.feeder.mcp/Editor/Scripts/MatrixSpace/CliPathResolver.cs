#nullable enable

using System;
using System.Diagnostics;
using System.IO;

namespace Feeder.MCP.Editor.MatrixSpace
{
    /// <summary>
    /// Resolves a CLI executable from an explicit path or from PATH (`where.exe`).
    /// </summary>
    public static class CliPathResolver
    {
        public static string? ResolveExecutable(string nameOrPath)
        {
            if (string.IsNullOrWhiteSpace(nameOrPath))
                return null;

            nameOrPath = nameOrPath.Trim();
            if (nameOrPath.Contains(Path.DirectorySeparatorChar) ||
                nameOrPath.Contains(Path.AltDirectorySeparatorChar))
            {
                return File.Exists(nameOrPath) ? nameOrPath : null;
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "where.exe",
                    Arguments = nameOrPath,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                    return null;

                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(5000);
                if (process.ExitCode != 0)
                    return null;

                foreach (var rawLine in output.Split('\n'))
                {
                    var line = rawLine.Trim();
                    if (line.Length > 0 && File.Exists(line))
                        return line;
                }
            }
            catch (Exception)
            {
                // Resolution is best-effort; the caller reports "not found".
            }

            return null;
        }

        /// <summary>Runs `&lt;exe&gt; --version`; returns trimmed first line or null.</summary>
        public static string? ProbeVersion(string resolvedPath, int timeoutMs = 15000)
        {
            try
            {
                var launch = new CliLaunchInfo(resolvedPath);
                var startInfo = new ProcessStartInfo
                {
                    FileName = launch.FileName,
                    Arguments = launch.BuildArguments("--version"),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                    return null;

                var output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit(timeoutMs);
                if (process.ExitCode != 0 || output.Length == 0)
                    return null;

                var newline = output.IndexOf('\n');
                return newline > 0 ? output.Substring(0, newline).Trim() : output;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
