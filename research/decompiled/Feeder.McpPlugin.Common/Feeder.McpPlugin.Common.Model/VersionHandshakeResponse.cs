using System.Text.Json.Serialization;

namespace Feeder.McpPlugin.Common.Model;

public class VersionHandshakeResponse
{
	[JsonPropertyName("apiVersion")]
	public string ApiVersion { get; set; } = string.Empty;

	[JsonPropertyName("serverVersion")]
	public string ServerVersion { get; set; } = string.Empty;

	[JsonPropertyName("compatible")]
	public bool Compatible { get; set; }

	[JsonPropertyName("message")]
	public string Message { get; set; } = string.Empty;

	[JsonIgnore]
	public bool IsConnectionError { get; set; }
}
