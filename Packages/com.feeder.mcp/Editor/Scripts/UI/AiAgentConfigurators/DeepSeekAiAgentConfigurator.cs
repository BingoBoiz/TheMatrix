#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using AgentConfig = Feeder.McpPlugin.AgentConfig;
using TransportMethod = Feeder.McpPlugin.Common.Consts.MCP.Server.TransportMethod;

namespace Feeder.MCP.Editor.UI
{
    /// <summary>
    /// DeepSeek AI agent configurator for the Matrix AI Connector.
    ///
    /// The shared <see cref="AgentConfig.AiAgentConfiguratorRegistry"/> (compiled into
    /// Feeder.McpPlugin.dll) has no runtime registration API, so DeepSeek is defined here, in
    /// the package's Editor assembly, and injected into the agent dropdown right after
    /// Claude Code by <see cref="AiAgentCatalog"/>.
    ///
    /// Configuration surface:
    /// - MCP entry: the standard project-root <c>.mcp.json</c> (identical shape Claude Code
    ///   writes — <c>mcpServers.Feeder-MCP</c> with type http/stdio), which DeepSeek-family
    ///   clients consume.
    /// - Skills: generated into the project's <c>.agents/skills</c> folder — the folder
    ///   DeepSeek Harness (and Codex-class agents) read skills from.
    /// </summary>
    public sealed class DeepSeekAiAgentConfigurator : AgentConfig.AiAgentConfigurator
    {
        public const string Id = "deepseek";

        public override string AgentName => "DeepSeek";
        public override string AgentId => Id;
        public override string DownloadUrl => "https://www.deepseek.com/";
        public override string SkillsPath => ".agents/skills";
        public override string IconName => ""; // no bundled icon asset — the window hides the icon slot

        public override string TutorialUrl => "https://api-docs.deepseek.com/";
        public override string TutorialLinkLabel => "DeepSeek Docs";
        public override string DownloadLinkLabel => "DeepSeek";

        protected override AgentConfig.AiAgentConfig CreateStdioConfig(
            AgentConfig.AgentConfiguratorSettings settings, ILogger logger)
        {
            var config = new AgentConfig.JsonAiAgentConfig(
                AgentConfig.AiAgentConfig.DefaultMcpServerName,
                Path.Combine(settings.ProjectRootPath, ".mcp.json"), "mcpServers", logger);

            // Use the resolved executable path (like the shared Claude Code configurator does),
            // not the bare name — the server binary lives under Library/mcp-server and is not on PATH.
            // JsonSerializer.Serialize produces a properly escaped JSON string (Windows paths
            // contain backslashes that must not be interpreted as JSON escapes).
            config.SetProperty("command",
                JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(settings.ExecutableFullPath)),
                requiredForConfiguration: true, AgentConfig.ValueComparisonMode.Path);
            config.SetProperty("args", JsonNode.Parse(
                    $"[{string.Join(",", BuildStdioArgs(settings))}]"),
                requiredForConfiguration: true, AgentConfig.ValueComparisonMode.Exact);

            if (settings.IsStdioAuthRequired)
                config.ApplyStdioAuthorization(true, settings.Token);
            return config;
        }

        protected override AgentConfig.AiAgentConfig CreateHttpConfig(
            AgentConfig.AgentConfiguratorSettings settings, ILogger logger)
        {
            var config = new AgentConfig.JsonAiAgentConfig(
                AgentConfig.AiAgentConfig.DefaultMcpServerName,
                Path.Combine(settings.ProjectRootPath, ".mcp.json"), "mcpServers", logger);

            config.SetProperty("type", JsonNode.Parse("\"http\""),
                requiredForConfiguration: true, AgentConfig.ValueComparisonMode.Exact);
            config.SetProperty("url", JsonNode.Parse($"\"{settings.Host}\""),
                requiredForConfiguration: true, AgentConfig.ValueComparisonMode.Url);

            if (settings.IsHttpAuthRequired)
                config.ApplyHttpAuthorization(true, settings.Token);
            return config;
        }

        protected override IReadOnlyList<AgentConfig.ConfigurationSection> BuildSections(
            AgentConfig.AgentConfiguratorSettings settings, TransportMethod transport, ILogger logger)
            => DefaultConfigurationSections(settings, transport, logger);

        protected override IReadOnlyList<AgentConfig.ConfigurationSection> BuildTroubleshootingSections(
            AgentConfig.AgentConfiguratorSettings settings, TransportMethod transport, ILogger logger)
        {
            var sections = new List<AgentConfig.ConfigurationSection>(base.BuildTroubleshootingSections(settings, transport, logger));
            sections.AddRange(AgentConfig.AiAgentConfigurator.TroubleshootingSection(new[]
            {
                "In Unity, open Tools > Feeder > Matrix AI Connector and select DeepSeek.",
                "Enable Auto-generate Skills — skills are written to the project's .agents/skills folder, which DeepSeek Harness (DSH) reads automatically.",
                "Connect DeepSeek Harness to Unity: in DSH, configure the mcp-client plugin with " +
                    $"transport \"streamable-http\", url \"{settings.Host}\" and headers {{\"Authorization\": \"Bearer <token>\"}} " +
                    "(the token is shown in the MCP configuration section). DSH does not read .mcp.json.",
                "The Configure button writes the project-root .mcp.json entry (server name Feeder-MCP) for MCP-native clients such as Claude Code.",
            }));
            return sections;
        }

        /// <summary>
        /// Builds the stdio launch arguments exactly like the shared library does for Claude Code
        /// (<c>port</c>, <c>plugin-timeout</c>, <c>client-transport=stdio</c>, <c>authorization</c>),
        /// so the written <c>.mcp.json</c> entry matches Claude Code's for the same settings —
        /// except <c>command</c>, which uses the resolved executable path (see
        /// <see cref="CreateStdioConfig"/>).
        /// </summary>
        static IEnumerable<string> BuildStdioArgs(AgentConfig.AgentConfiguratorSettings settings)
        {
            yield return $"\"port={settings.Port}\"";
            yield return $"\"plugin-timeout={settings.TimeoutMs}\"";
            yield return "\"client-transport=stdio\"";
            yield return $"\"authorization={settings.AuthOption.ToString().ToLowerInvariant()}\"";
        }
    }
}
