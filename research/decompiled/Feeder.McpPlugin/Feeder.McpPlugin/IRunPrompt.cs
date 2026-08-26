using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Feeder.McpPlugin.Common.Model;

namespace Feeder.McpPlugin;

public interface IRunPrompt : IEnabled
{
	string Name { get; }

	string? Title { get; }

	string? Description { get; }

	Role Role { get; }

	JsonNode? InputSchema { get; }

	Task<ResponseGetPrompt> Run(string requestId, IReadOnlyDictionary<string, JsonElement>? namedParameters, CancellationToken cancellationToken = default(CancellationToken));
}
