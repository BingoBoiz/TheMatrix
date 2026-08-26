using System.Collections.Generic;
using Feeder.McpPlugin.Common.Hub.Client;

namespace Feeder.McpPlugin;

public interface ISystemToolManager : IClientSystemToolHub
{
	int TotalToolsCount { get; }

	IEnumerable<IRunTool> GetAllTools();

	bool HasTool(string name);
}
