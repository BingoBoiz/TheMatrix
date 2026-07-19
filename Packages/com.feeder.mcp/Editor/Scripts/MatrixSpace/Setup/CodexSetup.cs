#nullable enable

using System.Collections.Generic;

namespace Feeder.MCP.Editor.MatrixSpace.Setup
{
    /// <summary>OpenAI Codex CLI: npm global install + ~/.codex/config.toml MCP entry.</summary>
    public sealed class CodexSetup : AgentSetupBase
    {
        private const string NpmPackage = "@openai/codex";
        private const string ConfiguratorId = "codex";

        public override string PresetId => "codex";

        public override IReadOnlyList<SetupStep> BuildSteps() => new[]
        {
            StepWriteMcpConfig(ConfiguratorId),
            StepCheckInstalled(),
            StepEnsureNpm(),
            StepNpmInstall(NpmPackage),
            StepResolveExecutable("codex"),
            StepVerify(),
        };
    }
}
