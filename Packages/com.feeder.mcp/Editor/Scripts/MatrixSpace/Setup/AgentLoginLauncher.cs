#nullable enable

using System;
using System.Diagnostics;

namespace Feeder.MCP.Editor.MatrixSpace.Setup
{
    /// <summary>
    /// Opens a real terminal window running the vendor CLI's own login flow (browser/device
    /// sign-in). Matrix Space never touches credentials — this is the one manual step.
    /// </summary>
    public static class AgentLoginLauncher
    {
        /// <summary>Returns false when the CLI executable can't be resolved (nothing to run).</summary>
        public static bool Open(AgentBackendPreset preset)
        {
            var path = AgentBackendCatalog.ResolveExecutablePath(preset);
            if (path == null)
                return false;

            var command = $"\"{path}\" {preset.LoginArgs}".TrimEnd();
            var hint = string.IsNullOrEmpty(preset.LoginHint) ? string.Empty : $"echo {preset.LoginHint} & ";
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/k \"{hint}{command}\"",
                // UseShellExecute=false still opens a console window when nothing is
                // redirected, and lets us hand the shell the augmented PATH (node/npm dirs).
                UseShellExecute = false,
                CreateNoWindow = false,
                WorkingDirectory = MatrixSpacePaths.ProjectRoot,
            };
            CliEnvironment.Apply(startInfo);

            try
            {
                Process.Start(startInfo);
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[MatrixSpace] Failed to open login terminal: {ex.Message}");
                return false;
            }
        }
    }
}
