using System.Text.Json.Serialization;

namespace Feeder.McpPlugin.Common.Model;

public class RequestVersionHandshake : IRequestID
{
	[JsonPropertyName("requestId")]
	public string RequestID { get; set; } = string.Empty;

	[JsonPropertyName("apiVersion")]
	public string ApiVersion { get; set; } = string.Empty;

	[JsonPropertyName("pluginVersion")]
	public string PluginVersion { get; set; } = string.Empty;

	[JsonPropertyName("environment")]
	public string Environment { get; set; } = string.Empty;
}
