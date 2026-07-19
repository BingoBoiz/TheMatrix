#nullable enable

using System;
using System.Diagnostics;
using System.IO;

namespace Feeder.MCP.Editor.MatrixSpace
{
    /// <summary>
    /// The Unity Editor inherits the PATH from when it was launched, so tools installed
    /// afterwards (Node.js, npm shims, uv/cursor installs) are invisible to child processes —
    /// npm shims like codex.cmd then fail with "'node' is not recognized". This helper
    /// prepends the well-known tool directories to PATH for every CLI child process.
    /// </summary>
    public static class CliEnvironment
    {
        /// <summary>Current process PATH with existing well-known tool dirs prepended.</summary>
        public static string AugmentedPath()
        {
            var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

            // Recomputed each call: these dirs can appear mid-session right after an install.
            var candidates = new[]
            {
                Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files", "nodejs"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin"),
            };

            foreach (var dir in candidates)
            {
                if (!Directory.Exists(dir))
                    continue;
                if (path.IndexOf(dir, StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                path = dir + Path.PathSeparator + path;
            }

            return path;
        }

        /// <summary>Applies the augmented PATH to a child process (requires UseShellExecute=false).</summary>
        public static void Apply(ProcessStartInfo startInfo)
        {
            try
            {
                startInfo.EnvironmentVariables["PATH"] = AugmentedPath();
            }
            catch (Exception)
            {
                // Best-effort: a failed PATH override must never block launching the process.
            }
        }
    }
}
