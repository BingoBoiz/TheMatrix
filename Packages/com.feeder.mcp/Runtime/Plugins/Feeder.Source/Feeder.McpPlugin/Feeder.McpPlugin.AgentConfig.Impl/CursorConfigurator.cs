using System.Collections.Generic;
using System.IO;
using Feeder.McpPlugin.Common;
using Microsoft.Extensions.Logging;

namespace Feeder.McpPlugin.AgentConfig.Impl
{
public sealed class CursorConfigurator : AiAgentConfigurator
{
	public override string AgentName => "Cursor";

	public override string AgentId => "cursor";

	public override string DownloadUrl => "https://cursor.com/download";

	public override string TutorialUrl => "https://www.youtube.com/watch?v=dyk-4gTolSU";

	public override string? SkillsPath => ".cursor/skills";

	public override string? IconName => "cursor-64.png";

	private static string LocalConfigPath(AgentConfiguratorSettings s)
	{
		return Path.Combine(s.ProjectRootPath, ".cursor", "mcp.json");
	}

	protected override AiAgentConfig CreateStdioConfig(AgentConfiguratorSettings settings, ILogger? logger)
	{
		return AgentConfigBuilders.JsonStdio(AgentName, LocalConfigPath(settings), settings, logger);
	}

	protected override AiAgentConfig CreateHttpConfig(AgentConfiguratorSettings settings, ILogger? logger)
	{
		return AgentConfigBuilders.JsonHttp(AgentName, LocalConfigPath(settings), settings, logger);
	}

	protected override IReadOnlyList<ConfigurationSection> BuildSections(AgentConfiguratorSettings settings, Consts.MCP.Server.TransportMethod transport, ILogger? logger)
	{
		return DefaultConfigurationSections(settings, transport, logger);
	}

	protected override IReadOnlyList<ConfigurationSection> BuildTroubleshootingSections(AgentConfiguratorSettings settings, Consts.MCP.Server.TransportMethod transport, ILogger? logger)
	{
		return AiAgentConfigurator.TroubleshootingSection("- '.cursor/mcp.json' file must have no json syntax errors.", "- Open Cursor settings window, go to 'MCP Servers' to restart ai-game-developer or to get more information about the available MCP tools and the status of the server.");
	}
}
}
