using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Feeder.McpPlugin.Common.Model;

namespace Feeder.McpPlugin;

public interface IRunTool : IEnabled
{
	string Name { get; }

	string? Title { get; }

	string? Description { get; }

	string? SkillDescription { get; }

	string? SkillBody { get; }

	JsonNode? InputSchema { get; }

	JsonNode? OutputSchema { get; }

	McpToolType ToolType => McpToolType.Standard;

	bool? ReadOnlyHint { get; }

	bool? DestructiveHint { get; }

	bool? IdempotentHint { get; }

	bool? OpenWorldHint { get; }

	int TokenCount { get; }

	Task<ResponseCallTool> Run(string requestId, IReadOnlyDictionary<string, JsonElement>? namedParameters, CancellationToken cancellationToken = default(CancellationToken));
}
