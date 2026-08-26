using System;
using Feeder.McpPlugin.Common.Utils;
using Microsoft.Extensions.Logging;

namespace Feeder.McpPlugin.Common.Model
{
public static class ResponseListPromptExtensions
{
	public static ResponseListPrompts Log(this ResponseListPrompts response, ILogger logger, Exception? ex = null)
	{
		if (!logger.IsEnabled(LogLevel.Debug))
		{
			return response;
		}
		foreach (ResponsePrompt prompt in response.Prompts)
		{
			logger.LogDebug(ex, "Prompt: " + prompt.Name + ":\n" + prompt.ToPrettyJson());
		}
		return response;
	}

	public static ResponseData<ResponseListPrompts> Pack(this ResponseListPrompts response, string requestId, string? message = null)
	{
		return ResponseData<ResponseListPrompts>.Success(requestId, message ?? "List Prompt execution completed.").SetData(response);
	}
}
}
