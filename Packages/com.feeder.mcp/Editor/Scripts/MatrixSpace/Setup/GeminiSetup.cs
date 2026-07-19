#nullable enable

using System.Collections.Generic;

namespace Feeder.MCP.Editor.MatrixSpace.Setup
{
    /// <summary>Google Gemini CLI: npm global install + ~/.gemini/settings.json MCP entry.</summary>
    public sealed class GeminiSetup : AgentSetupBase
    {
        private const string NpmPackage = "@google/gemini-cli";
        private const string ConfiguratorId = "gemini";

        public override string PresetId => "gemini";

        public override IReadOnlyList<SetupStep> BuildSteps() => new[]
        {
            StepWriteMcpConfig(ConfiguratorId),
            StepCheckInstalled(),
            StepEnsureNpm(),
            StepNpmInstall(NpmPackage),
            StepResolveExecutable("gemini"),
            StepVerify(),
        };
    }
}
