#nullable enable

using System;
using System.Diagnostics;
using System.IO;

namespace Feeder.MCP.Editor.MatrixSpace
{
    /// <summary>
    /// Resolved launch info for the claude CLI on Windows. npm installs a `claude.cmd` shim
    /// which cannot be started directly with UseShellExecute=false, so `.cmd`/`.bat` targets
    /// are wrapped in `cmd.exe /d /s /c`.
    /// </summary>
    public readonly struct CliLaunchInfo
    {
        public readonly string FileName;
        /// <summary>Prefix prepended to the tool arguments (empty for native executables).</summary>
        public readonly string ArgumentsPrefix;
        public readonly string ArgumentsSuffix;
        public readonly string ResolvedPath;

        public CliLaunchInfo(string resolvedPath)
        {
            ResolvedPath = resolvedPath;
            var ext = Path.GetExtension(resolvedPath);
            if (string.Equals(ext, ".cmd", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".bat", StringComparison.OrdinalIgnoreCase))
            {
                FileName = "cmd.exe";
                ArgumentsPrefix = $"/d /s /c \"\"{resolvedPath}\" ";
                ArgumentsSuffix = "\"";
            }
            else
            {
                FileName = resolvedPath;
                ArgumentsPrefix = string.Empty;
                ArgumentsSuffix = string.Empty;
            }
        }

        public string BuildArguments(string toolArguments)
            => ArgumentsPrefix + toolArguments + ArgumentsSuffix;
    }

    /// <summary>
    /// Discovers the claude CLI executable on Windows. Order: explicit user setting,
    /// `where claude`, then well-known install locations. Result is cached per domain.
    /// </summary>
    public static class ClaudeCliLocator
    {
        private static string? _cachedPath;
        private static bool _probed;

        public static void InvalidateCache()
        {
            _cachedPath = null;
            _probed = false;
        }

        /// <summary>Returns the resolved claude executable path, or null when not found.</summary>
        public static string? Locate()
        {
            var configured = MatrixSpaceSettings.ClaudeExecutablePath.Value;
            if (!string.IsNullOrWhiteSpace(configured))
                return File.Exists(configured) ? configured : null;

            if (_probed)
                return _cachedPath;

            _probed = true;
            _cachedPath = ProbeWellKnownPaths() ?? ProbeWhere();
            return _cachedPath;
        }

        public static bool TryGetLaunchInfo(out CliLaunchInfo info, out string error)
        {
            var path = Locate();
            if (path == null)
            {
                info = default;
                error = "claude CLI not found. Install Claude Code (npm install -g @anthropic-ai/claude-code) " +
                        "or set the executable path in Matrix Space settings.";
                return false;
            }

            info = new CliLaunchInfo(path);
            error = string.Empty;
            return true;
        }

        private static string? ProbeWellKnownPaths()
        {
            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", "claude.cmd"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "claude", "claude.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "claude.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "claude.cmd"),
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                    return candidate;
            }

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var found = ProbeNativeInstall(Path.Combine(appData, "Claude", "claude-code"));
            if (found != null)
                return found;

            // The MSIX-packaged Claude desktop app virtualizes its AppData writes, so the
            // claude-code folder it manages lives under the package's LocalCache instead.
            var packagesRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages");
            if (Directory.Exists(packagesRoot))
            {
                foreach (var package in Directory.GetDirectories(packagesRoot, "Claude_*"))
                {
                    found = ProbeNativeInstall(Path.Combine(package, "LocalCache", "Roaming", "Claude", "claude-code"));
                    if (found != null)
                        return found;
                }
            }

            return null;
        }

        /// <summary>
        /// The native (non-npm) installer keeps versioned folders under
        /// &lt;root&gt;\&lt;version&gt;\claude.exe — pick the newest.
        /// </summary>
        private static string? ProbeNativeInstall(string root)
        {
            try
            {
                if (!Directory.Exists(root))
                    return null;

                string? best = null;
                Version? bestVersion = null;
                foreach (var dir in Directory.GetDirectories(root))
                {
                    var exe = Path.Combine(dir, "claude.exe");
                    if (!File.Exists(exe))
                        continue;

                    Version.TryParse(Path.GetFileName(dir), out var version);
                    if (best == null || (version != null && (bestVersion == null || version > bestVersion)))
                    {
                        best = exe;
                        bestVersion = version;
                    }
                }

                return best;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string? ProbeWhere()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "where.exe",
                    Arguments = "claude",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                CliEnvironment.Apply(startInfo);

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
                // Discovery is best-effort; the caller reports "not found".
            }

            return null;
        }

        /// <summary>Runs `claude --version` synchronously (short timeout). Null when unavailable.</summary>
        public static string? ProbeVersion()
        {
            if (!TryGetLaunchInfo(out var info, out _))
                return null;

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = info.FileName,
                    Arguments = info.BuildArguments("--version"),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                CliEnvironment.Apply(startInfo);

                using var process = Process.Start(startInfo);
                if (process == null)
                    return null;

                var output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit(15000);
                return process.ExitCode == 0 && output.Length > 0 ? output : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
