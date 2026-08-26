using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Feeder.McpPlugin.AgentConfig
{
public abstract class AiAgentConfig
{
	public static readonly string[] DeprecatedMcpServerNames = new string[2] { "Unity-MCP", "ai-game-developer" };

	public const string DefaultMcpServerName = "Feeder-MCP";

	public static readonly string[] DefaultIdentityKeys = new string[2] { "command", "url" };

	protected readonly List<string> _identityKeys = new List<string>(DefaultIdentityKeys);

	protected readonly ILogger? _logger;

	public string Name { get; set; }

	public string ConfigPath { get; set; }

	public string BodyPath { get; set; }

	public abstract string ExpectedFileContent { get; }

	public IReadOnlyList<string> IdentityKeys => _identityKeys;

	protected AiAgentConfig(string name, string configPath, string bodyPath = "mcpServers", ILogger? logger = null)
	{
		Name = name;
		ConfigPath = configPath;
		BodyPath = bodyPath;
		_logger = logger;
	}

	public AiAgentConfig AddIdentityKey(string key)
	{
		if (!_identityKeys.Contains(key))
		{
			_identityKeys.Add(key);
		}
		return this;
	}

	public abstract bool Configure();

	public abstract bool Unconfigure();

	public abstract bool IsDetected();

	public abstract bool IsConfigured();

	public virtual void ApplyHttpAuthorization(bool isRequired, string? token)
	{
	}

	public virtual void ApplyStdioAuthorization(bool isRequired, string? token)
	{
	}
}
}
