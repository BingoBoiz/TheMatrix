#nullable enable

using System.Collections.Generic;

namespace Feeder.MCP.Editor.MatrixSpace.Setup
{
    /// <summary>
    /// Moonshot Kimi CLI: Python tool installed via uv (`uv tool install kimi-cli`).
    /// uv itself is bootstrapped through winget when missing. No MCP configurator exists
    /// for Kimi, so that step is omitted.
    /// </summary>
    public sealed class KimiSetup : AgentSetupBase
    {
        private const string UvPackage = "kimi-cli";

        public override string PresetId => "kimi";

        public override IReadOnlyList<SetupStep> BuildSteps() => new[]
        {
            StepCheckInstalled(),
            StepEnsureUv(),
            StepUvToolInstall(),
            StepResolveExecutable("kimi"),
            StepVerify(),
        };

        private static SetupStep StepEnsureUv() => new("Check uv (Python tool manager)", ctx =>
        {
            if (ctx.SkipInstall)
                return true;

            var uv = FindUv();
            if (uv == null)
            {
                ctx.Log("uv not found — installing via winget…");
                SetupCommandRunner.Run("winget",
                    "install --id astral-sh.uv -e --accept-source-agreements --accept-package-agreements",
                    ctx.Log);
                uv = FindUv();
                if (uv == null)
                {
                    ctx.Log($"uv still missing — checked PATH (where.exe uv) and {SetupCommandRunner.LocalBinPath("uv.exe")}.");
                    ctx.Log("uv could not be installed automatically. Opening the Kimi CLI page — follow its manual install, then press SETUP again.");
                    if (!string.IsNullOrEmpty(ctx.Preset.InstallUrl))
                        ctx.OpenUrl(ctx.Preset.InstallUrl);
                    return false;
                }
            }

            // Reuses NpmPath as the generic "resolved package-manager path" slot.
            ctx.NpmPath = uv;
            ctx.Log($"uv: {uv}");
            return true;
        });

        private static SetupStep StepUvToolInstall() => new($"uv tool install {UvPackage}", ctx =>
        {
            if (ctx.SkipInstall)
                return true;

            ctx.Log($"Installing {UvPackage}…");
            var exitCode = SetupCommandRunner.Run(ctx.NpmPath!, $"tool install {UvPackage}", ctx.Log);
            if (exitCode != 0)
            {
                ctx.Log($"uv tool install failed (exit {exitCode}). Opening the vendor install guide.");
                if (!string.IsNullOrEmpty(ctx.Preset.InstallUrl))
                    ctx.OpenUrl(ctx.Preset.InstallUrl);
                return false;
            }

            return true;
        });

        private static string? FindUv() => SetupCommandRunner.Find("uv",
            SetupCommandRunner.LocalBinPath("uv.exe"));
    }
}
