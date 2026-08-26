using System;
using System.Collections.Generic;
using Feeder.McpPlugin.Common.Hub.Client;
using R3;

namespace Feeder.McpPlugin;

public interface IToolManager : IClientToolHub, IDisposable
{
	Observable<Unit> OnToolsUpdated { get; }

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
}
