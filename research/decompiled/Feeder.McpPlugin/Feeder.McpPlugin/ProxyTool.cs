using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Feeder.McpPlugin.Common.Model;

namespace Feeder.McpPlugin;

public sealed class ProxyTool : IRunTool, IEnabled
{
	private readonly Func<string, IReadOnlyDictionary<string, JsonElement>?, CancellationToken, Task<ResponseCallTool>> _handler;

	private int? _cachedTokenCount;

	public string Name { get; }

	public string? Title { get; }

	public string? Description { get; }

	public string? SkillDescription { get; }

	public string? SkillBody { get; }

	public JsonNode? InputSchema { get; }

	public JsonNode? OutputSchema { get; }

	public bool? ReadOnlyHint { get; }

	public bool? DestructiveHint { get; }

	public bool? IdempotentHint { get; }

	public bool? OpenWorldHint { get; }

	public bool Enabled { get; set; } = true;

	public int TokenCount
	{
		get
		{
			if (_cachedTokenCount.HasValue)
			{
				return _cachedTokenCount.Value;
			}
			try
			{
				_cachedTokenCount = ToolTokenCount.Calculate(Name, Title, Description, InputSchema, OutputSchema);
			}
			catch
			{
				_cachedTokenCount = 0;
			}
			return _cachedTokenCount.Value;
		}
	}

	public ProxyTool(string name, string? title, string? description, string? skillDescription, string? skillBody, JsonNode? inputSchema, JsonNode? outputSchema, bool? readOnlyHint, bool? destructiveHint, bool? idempotentHint, bool? openWorldHint, Func<string, IReadOnlyDictionary<string, JsonElement>?, CancellationToken, Task<ResponseCallTool>> handler)
	{
		Name = name ?? throw new ArgumentNullException("name");
		Title = title;
		Description = description;
		SkillDescription = skillDescription;
		SkillBody = skillBody;
		InputSchema = ((inputSchema == null) ? null : JsonNode.Parse(inputSchema.ToJsonString()));
		OutputSchema = ((outputSchema == null) ? null : JsonNode.Parse(outputSchema.ToJsonString()));
		ReadOnlyHint = readOnlyHint;
		DestructiveHint = destructiveHint;
		IdempotentHint = idempotentHint;
		OpenWorldHint = openWorldHint;
		_handler = handler ?? throw new ArgumentNullException("handler");
	}

	public Task<ResponseCallTool> Run(string requestId, IReadOnlyDictionary<string, JsonElement>? namedParameters, CancellationToken cancellationToken = default(CancellationToken))
	{
		return _handler(requestId, namedParameters, cancellationToken);
	}
}
