using System;
using System.Collections.Generic;
using Feeder.McpPlugin.Common.Hub.Client;
using R3;

namespace Feeder.McpPlugin;

public interface IResourceManager : IClientResourceHub, IDisposable
{
	Observable<Unit> OnResourcesUpdated { get; }

	int EnabledResourcesCount { get; }

	int TotalResourcesCount { get; }

	IEnumerable<IRunResource> GetAllResources();

	bool HasResource(string name);

	bool AddResource(IRunResource resourceParams);

	bool RemoveResource(string name);

	bool IsResourceEnabled(string name);

	bool SetResourceEnabled(string name, bool enabled);
}
