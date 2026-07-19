#nullable enable

using System.Collections.Generic;

namespace Feeder.MCP.Editor.MatrixSpace.Setup
{
    /// <summary>Anthropic Claude Code: npm global install + project .mcp.json.</summary>
    public sealed class ClaudeSetup : AgentSetupBase
    {
        private const string NpmPackage = "@anthropic-ai/claude-code";
        private const string ConfiguratorId = "claude-code";

        public override string PresetId => "claude";

        public override IReadOnlyList<SetupStep> BuildSteps() => new[]
        {
            StepWriteMcpConfig(ConfiguratorId),
            StepCheckInstalled(),
            StepEnsureNpm(),
            StepNpmInstall(NpmPackage),
            StepResolveExecutable("claude"),
            StepVerify(),
        };
    }
}
