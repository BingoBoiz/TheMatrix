using Feeder.McpPlugin.Common;

namespace Feeder.McpPlugin.AgentConfig;

public static class DockerCommands
{
	public static string ContainerName(AgentConfiguratorSettings settings)
	{
		return $"{settings.ServerExecutableName}-{settings.Port}";
	}

	public static string SetupRun(AgentConfiguratorSettings settings)
	{
		string text = $"-p {settings.Port}:{settings.Port}";
		string text2 = string.Format("-e {0}={1} ", "MCP_PLUGIN_CLIENT_TRANSPORT", Consts.MCP.Server.TransportMethod.streamableHttp) + string.Format("-e {0}={1} ", "MCP_PLUGIN_PORT", settings.Port) + string.Format("-e {0}={1} ", "MCP_PLUGIN_CLIENT_TIMEOUT", settings.TimeoutMs) + string.Format("-e {0}={1}", "MCP_AUTHORIZATION", settings.AuthOption);
		if (settings.AuthOption == Consts.MCP.Server.AuthOption.required && !string.IsNullOrEmpty(settings.Token))
		{
			text2 = text2 + " -e MCP_PLUGIN_TOKEN=" + settings.Token;
		}
		string text3 = "--name " + ContainerName(settings);
		string text4 = settings.DockerImage + ":" + settings.ServerVersion;
		return "docker run -d " + text + " " + text2 + " " + text3 + " " + text4;
	}

	public static string Run(AgentConfiguratorSettings settings)
	{
		return "docker start " + ContainerName(settings);
	}

	public static string Stop(AgentConfiguratorSettings settings)
	{
		return "docker stop " + ContainerName(settings);
	}

	public static string Remove(AgentConfiguratorSettings settings)
	{
		return "docker rm " + ContainerName(settings);
	}
}
