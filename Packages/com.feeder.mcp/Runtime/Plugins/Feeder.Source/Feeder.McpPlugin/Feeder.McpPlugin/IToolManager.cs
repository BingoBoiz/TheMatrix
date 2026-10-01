using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Feeder.McpPlugin.Common.Model;
using Feeder.McpPlugin.Common.Hub.Client;
using R3;

namespace Feeder.McpPlugin
{
public interface IToolManager : IClientToolHub, IDisposable
{
	Observable<Unit> OnToolsUpdated { get; }

	Observable<ToolCallActivity> OnToolCall { get; }

	int EnabledToolsCount { get; }

	int TotalToolsCount { get; }

	ulong ToolCallsCount => 0uL;

	int EnabledToolsTokenCount { get; }

	IEnumerable<IRunTool> GetAllTools();

	bool HasTool(string name);

	bool AddTool(string name, IRunTool runner);

	bool RemoveTool(string name);

	bool IsToolEnabled(string name);

	bool SetToolEnabled(string name, bool enabled);

	Task<ResponseCallTool> RunTool(IRunTool tool, string requestId, IReadOnlyDictionary<string, JsonElement>? namedParameters, CancellationToken cancellationToken = default(CancellationToken));
}
}
