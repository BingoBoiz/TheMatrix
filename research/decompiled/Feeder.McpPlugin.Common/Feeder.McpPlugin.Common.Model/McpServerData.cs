using System.Text.Json.Serialization;

namespace Feeder.McpPlugin.Common.Model;

public class McpServerData
{
	[JsonPropertyName("serverVersion")]
	public string? ServerVersion { get; set; }

	[JsonPropertyName("serverApiVersion")]
	public string? ServerApiVersion { get; set; }

	[JsonPropertyName("serverTransport")]
	[JsonConverter(typeof(JsonStringEnumConverter))]
	public Consts.MCP.Server.TransportMethod ServerTransport { get; set; }

	[JsonPropertyName("isAiAgentConnected")]
	public bool IsAiAgentConnected { get; set; }
}
