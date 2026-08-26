using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Feeder.McpPlugin.Common.Model;

namespace Feeder.McpPlugin;

public sealed class ProxyToolFactory : IDynamicToolFactory
{
	public IRunTool CreateProxyTool(string name, string? title, string? description, string? skillDescription, string? skillBody, JsonNode? inputSchema, JsonNode? outputSchema, bool? readOnlyHint, bool? destructiveHint, bool? idempotentHint, bool? openWorldHint, Func<string, IReadOnlyDictionary<string, JsonElement>?, CancellationToken, Task<ResponseCallTool>> handler)
	{
		return new ProxyTool(name, title, description, skillDescription, skillBody, inputSchema, outputSchema, readOnlyHint, destructiveHint, idempotentHint, openWorldHint, handler);
	}
}
