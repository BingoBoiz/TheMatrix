using System.Collections.Generic;

namespace Feeder.McpPlugin.Common.Model;

public class ResponseListPrompts
{
	public List<ResponsePrompt> Prompts { get; set; } = new List<ResponsePrompt>();
}
