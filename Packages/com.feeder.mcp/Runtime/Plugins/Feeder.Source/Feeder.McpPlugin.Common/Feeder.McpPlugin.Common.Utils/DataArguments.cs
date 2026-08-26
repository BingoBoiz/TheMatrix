using System;
using System.Collections.Generic;

namespace Feeder.McpPlugin.Common.Utils
{
public class DataArguments : IDataArguments
{
	public int Port { get; private set; } = 8080;

	public int PluginTimeoutMs { get; private set; }

	public int IdleTimeoutSeconds { get; private set; } = 600;

	public int MaxIdleSessionCount { get; private set; } = 1000;

	public Consts.MCP.Server.TransportMethod ClientTransport { get; private set; }

	public Consts.MCP.Server.AuthOption Authorization { get; private set; } = Consts.MCP.Server.AuthOption.none;

	public string? Token { get; private set; }

	public string? WebhookToolUrl { get; private set; }

	public string? WebhookPromptUrl { get; private set; }

	public string? WebhookResourceUrl { get; private set; }

	public string? WebhookConnectionUrl { get; private set; }

	public string? WebhookToken { get; private set; }

	public string? WebhookHeader { get; private set; }

	public int WebhookTimeoutMs { get; private set; } = 10000;

	public string? WebhookAuthorizationUrl { get; private set; }

	public bool WebhookAuthorizationFailOpen { get; private set; }

	public DataArguments(string[] args)
	{
		Port = 8080;
		PluginTimeoutMs = 10000;
		ClientTransport = Consts.MCP.Server.TransportMethod.streamableHttp;
		ParseEnvironmentVariables();
		ParseCommandLineArguments(args);
		if (Authorization == Consts.MCP.Server.AuthOption.unknown)
		{
			Authorization = Consts.MCP.Server.AuthOption.none;
		}
	}

	private void ParseEnvironmentVariables()
	{
		string environmentVariable = Environment.GetEnvironmentVariable("MCP_PLUGIN_PORT");
		if (environmentVariable != null && int.TryParse(environmentVariable, out var result))
		{
			Port = result;
		}
		string environmentVariable2 = Environment.GetEnvironmentVariable("MCP_PLUGIN_CLIENT_TIMEOUT");
		if (environmentVariable2 != null && int.TryParse(environmentVariable2, out var result2))
		{
			PluginTimeoutMs = result2;
		}
		string environmentVariable3 = Environment.GetEnvironmentVariable("MCP_PLUGIN_IDLE_TIMEOUT_SECONDS");
		if (environmentVariable3 != null && int.TryParse(environmentVariable3, out var result3) && result3 > 0)
		{
			IdleTimeoutSeconds = result3;
		}
		string environmentVariable4 = Environment.GetEnvironmentVariable("MCP_PLUGIN_MAX_IDLE_SESSION_COUNT");
		if (environmentVariable4 != null && int.TryParse(environmentVariable4, out var result4) && result4 > 0)
		{
			MaxIdleSessionCount = result4;
		}
		string environmentVariable5 = Environment.GetEnvironmentVariable("MCP_PLUGIN_CLIENT_TRANSPORT");
		if (environmentVariable5 != null && Enum.TryParse<Consts.MCP.Server.TransportMethod>(environmentVariable5, out var result5))
		{
			ClientTransport = result5;
		}
		string environmentVariable6 = Environment.GetEnvironmentVariable("MCP_PLUGIN_TOKEN");
		if (environmentVariable6 != null)
		{
			Token = environmentVariable6;
		}
		string environmentVariable7 = Environment.GetEnvironmentVariable("MCP_AUTHORIZATION");
		if (environmentVariable7 != null && Enum.TryParse<Consts.MCP.Server.AuthOption>(environmentVariable7, ignoreCase: true, out var result6))
		{
			Authorization = result6;
		}
		string environmentVariable8 = Environment.GetEnvironmentVariable("MCP_PLUGIN_WEBHOOK_TOOL_URL");
		if (environmentVariable8 != null)
		{
			WebhookToolUrl = environmentVariable8;
		}
		string environmentVariable9 = Environment.GetEnvironmentVariable("MCP_PLUGIN_WEBHOOK_PROMPT_URL");
		if (environmentVariable9 != null)
		{
			WebhookPromptUrl = environmentVariable9;
		}
		string environmentVariable10 = Environment.GetEnvironmentVariable("MCP_PLUGIN_WEBHOOK_RESOURCE_URL");
		if (environmentVariable10 != null)
		{
			WebhookResourceUrl = environmentVariable10;
		}
		string environmentVariable11 = Environment.GetEnvironmentVariable("MCP_PLUGIN_WEBHOOK_CONNECTION_URL");
		if (environmentVariable11 != null)
		{
			WebhookConnectionUrl = environmentVariable11;
		}
		string environmentVariable12 = Environment.GetEnvironmentVariable("MCP_PLUGIN_WEBHOOK_TOKEN");
		if (environmentVariable12 != null)
		{
			WebhookToken = environmentVariable12;
		}
		string environmentVariable13 = Environment.GetEnvironmentVariable("MCP_PLUGIN_WEBHOOK_HEADER");
		if (environmentVariable13 != null)
		{
			WebhookHeader = environmentVariable13;
		}
		string environmentVariable14 = Environment.GetEnvironmentVariable("MCP_PLUGIN_WEBHOOK_TIMEOUT");
		if (environmentVariable14 != null && int.TryParse(environmentVariable14, out var result7))
		{
			WebhookTimeoutMs = result7;
		}
		string environmentVariable15 = Environment.GetEnvironmentVariable("MCP_PLUGIN_WEBHOOK_AUTHORIZATION_URL");
		if (environmentVariable15 != null)
		{
			WebhookAuthorizationUrl = environmentVariable15;
		}
		string environmentVariable16 = Environment.GetEnvironmentVariable("MCP_PLUGIN_WEBHOOK_AUTHORIZATION_FAIL_OPEN");
		if (environmentVariable16 != null && bool.TryParse(environmentVariable16, out var result8))
		{
			WebhookAuthorizationFailOpen = result8;
		}
	}

	private void ParseCommandLineArguments(string[] args)
	{
		Dictionary<string, string> dictionary = ArgsUtils.ParseLineArguments(args);
		string valueOrDefault = dictionary.GetValueOrDefault("port".TrimStart('-'));
		if (valueOrDefault != null && int.TryParse(valueOrDefault, out var result))
		{
			Port = result;
		}
		string valueOrDefault2 = dictionary.GetValueOrDefault("plugin-timeout".TrimStart('-'));
		if (valueOrDefault2 != null && int.TryParse(valueOrDefault2, out var result2))
		{
			PluginTimeoutMs = result2;
		}
		string valueOrDefault3 = dictionary.GetValueOrDefault("idle-timeout-seconds".TrimStart('-'));
		if (valueOrDefault3 != null && int.TryParse(valueOrDefault3, out var result3) && result3 > 0)
		{
			IdleTimeoutSeconds = result3;
		}
		string valueOrDefault4 = dictionary.GetValueOrDefault("max-idle-session-count".TrimStart('-'));
		if (valueOrDefault4 != null && int.TryParse(valueOrDefault4, out var result4) && result4 > 0)
		{
			MaxIdleSessionCount = result4;
		}
		string valueOrDefault5 = dictionary.GetValueOrDefault("client-transport".TrimStart('-'));
		if (valueOrDefault5 != null && Enum.TryParse<Consts.MCP.Server.TransportMethod>(valueOrDefault5, out var result5))
		{
			ClientTransport = result5;
		}
		string valueOrDefault6 = dictionary.GetValueOrDefault("token".TrimStart('-'));
		if (valueOrDefault6 != null)
		{
			Token = valueOrDefault6;
		}
		string valueOrDefault7 = dictionary.GetValueOrDefault("authorization".TrimStart('-'));
		if (valueOrDefault7 != null && Enum.TryParse<Consts.MCP.Server.AuthOption>(valueOrDefault7, ignoreCase: true, out var result6))
		{
			Authorization = result6;
		}
		string valueOrDefault8 = dictionary.GetValueOrDefault("webhook-tool-url".TrimStart('-'));
		if (valueOrDefault8 != null)
		{
			WebhookToolUrl = valueOrDefault8;
		}
		string valueOrDefault9 = dictionary.GetValueOrDefault("webhook-prompt-url".TrimStart('-'));
		if (valueOrDefault9 != null)
		{
			WebhookPromptUrl = valueOrDefault9;
		}
		string valueOrDefault10 = dictionary.GetValueOrDefault("webhook-resource-url".TrimStart('-'));
		if (valueOrDefault10 != null)
		{
			WebhookResourceUrl = valueOrDefault10;
		}
		string valueOrDefault11 = dictionary.GetValueOrDefault("webhook-connection-url".TrimStart('-'));
		if (valueOrDefault11 != null)
		{
			WebhookConnectionUrl = valueOrDefault11;
		}
		string valueOrDefault12 = dictionary.GetValueOrDefault("webhook-token".TrimStart('-'));
		if (valueOrDefault12 != null)
		{
			WebhookToken = valueOrDefault12;
		}
		string valueOrDefault13 = dictionary.GetValueOrDefault("webhook-header".TrimStart('-'));
		if (valueOrDefault13 != null)
		{
			WebhookHeader = valueOrDefault13;
		}
		string valueOrDefault14 = dictionary.GetValueOrDefault("webhook-timeout".TrimStart('-'));
		if (valueOrDefault14 != null && int.TryParse(valueOrDefault14, out var result7))
		{
			WebhookTimeoutMs = result7;
		}
		string valueOrDefault15 = dictionary.GetValueOrDefault("webhook-authorization-url".TrimStart('-'));
		if (valueOrDefault15 != null)
		{
			WebhookAuthorizationUrl = valueOrDefault15;
		}
		string valueOrDefault16 = dictionary.GetValueOrDefault("webhook-authorization-fail-open".TrimStart('-'));
		if (valueOrDefault16 != null && bool.TryParse(valueOrDefault16, out var result8))
		{
			WebhookAuthorizationFailOpen = result8;
		}
	}
}
}
