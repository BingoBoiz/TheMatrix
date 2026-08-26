namespace Feeder.McpPlugin.Common.Model;

public class RequestPromptsUpdated
{
	public string RequestId { get; set; } = string.Empty;

	public override string ToString()
	{
		return "RequestId: " + RequestId;
	}
}
