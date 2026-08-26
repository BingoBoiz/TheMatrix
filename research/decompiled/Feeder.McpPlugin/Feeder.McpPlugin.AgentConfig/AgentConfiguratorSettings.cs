using Feeder.McpPlugin.Common;

namespace Feeder.McpPlugin.AgentConfig;

public sealed class AgentConfiguratorSettings
{
	public OperatingSystemKind OperatingSystem { get; }

	public string ProjectRootPath { get; }

	public string ExecutableFullPath { get; }

	public int Port { get; }

	public int TimeoutMs { get; }

	public string Host { get; }

	public string? Token { get; }

	public ConnectionMode ConnectionMode { get; }

	public Consts.MCP.Server.AuthOption AuthOption { get; }

	public string ServerExecutableName { get; }

	public string ServerVersion { get; }

	public string DockerImage { get; }

	public bool IsHttpAuthRequired
	{
		get
		{
			if (ConnectionMode != ConnectionMode.Cloud)
			{
				return AuthOption == Consts.MCP.Server.AuthOption.required;
			}
			return true;
		}
	}

	public bool IsStdioAuthRequired => AuthOption == Consts.MCP.Server.AuthOption.required;

	public bool IsWindows => OperatingSystem == OperatingSystemKind.Windows;

	public AgentConfiguratorSettings(OperatingSystemKind operatingSystem, string projectRootPath, string executableFullPath, int port, int timeoutMs, string host, string? token = null, ConnectionMode connectionMode = ConnectionMode.Local, Consts.MCP.Server.AuthOption authOption = Consts.MCP.Server.AuthOption.none, string serverExecutableName = "gamedev-mcp-server", string serverVersion = "8.0.0", string dockerImage = "aigamedeveloper/mcp-server")
	{
		OperatingSystem = operatingSystem;
		ProjectRootPath = projectRootPath;
		ExecutableFullPath = executableFullPath;
		Port = port;
		TimeoutMs = timeoutMs;
		Host = host;
		Token = token;
		ConnectionMode = connectionMode;
		AuthOption = authOption;
		ServerExecutableName = serverExecutableName;
		ServerVersion = serverVersion;
		DockerImage = dockerImage;
	}

	public static AgentConfiguratorSettings CreateForHost(string projectRootPath, string executableFullPath, int port, int timeoutMs, string host, string? token = null, ConnectionMode connectionMode = ConnectionMode.Local, Consts.MCP.Server.AuthOption authOption = Consts.MCP.Server.AuthOption.none, string serverExecutableName = "gamedev-mcp-server", string serverVersion = "8.0.0", string dockerImage = "aigamedeveloper/mcp-server")
	{
		return new AgentConfiguratorSettings(HostOperatingSystem.Detect(), projectRootPath, executableFullPath, port, timeoutMs, host, token, connectionMode, authOption, serverExecutableName, serverVersion, dockerImage);
	}
}
