namespace Feeder.McpPlugin.Common.Model;

public class ResponsePromptArgument
{
	public string Name { get; set; } = string.Empty;

	public string? Title { get; set; }

	public string? Description { get; set; }

	public bool? Required { get; set; }
}
