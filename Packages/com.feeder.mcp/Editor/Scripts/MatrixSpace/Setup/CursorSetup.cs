#nullable enable

using System.Collections.Generic;

namespace Feeder.MCP.Editor.MatrixSpace.Setup
{
    /// <summary>
    /// Cursor CLI (cursor-agent): installed via the vendor's PowerShell script into
    /// %USERPROFILE%\.local\bin. Falls back to the install page when the script fails.
    /// </summary>
    public sealed class CursorSetup : AgentSetupBase
    {
        private const string InstallScript = "irm https://cursor.com/install | iex";
        private const string ConfiguratorId = "cursor";

        public override string PresetId => "cursor";

        public override IReadOnlyList<SetupStep> BuildSteps() => new[]
        {
            StepWriteMcpConfig(ConfiguratorId),
            StepCheckInstalled(),
            StepVendorScriptInstall(),
            StepResolveExecutable("cursor-agent"),
            StepVerify(),
        };

        private static SetupStep StepVendorScriptInstall() => new("Run cursor.com install script", ctx =>
        {
            if (ctx.SkipInstall)
                return true;

            ctx.Log("Running the Cursor install script (powershell)…");
            var exitCode = SetupCommandRunner.Run("powershell.exe",
                $"-NoProfile -ExecutionPolicy Bypass -Command \"{InstallScript}\"",
                ctx.Log);
            if (exitCode != 0)
                ctx.Log($"Install script exited with {exitCode} — will still probe for the executable.");

            return true;
        });
    }
}
