namespace Feeder.McpPlugin.Common.Utils
{
public interface IDataArguments
{
	int Port { get; }

	int PluginTimeoutMs { get; }

	int IdleTimeoutSeconds { get; }

	int MaxIdleSessionCount { get; }

	Consts.MCP.Server.TransportMethod ClientTransport { get; }

	Consts.MCP.Server.AuthOption Authorization { get; }

	string? Token { get; }

	string? WebhookToolUrl { get; }

	string? WebhookPromptUrl { get; }

	string? WebhookResourceUrl { get; }

	string? WebhookConnectionUrl { get; }

	string? WebhookToken { get; }

	string? WebhookHeader { get; }

	int WebhookTimeoutMs { get; }

	string? WebhookAuthorizationUrl { get; }

	bool WebhookAuthorizationFailOpen { get; }
}
}
