using System;
using System.Collections.Generic;
using Feeder.McpPlugin.Common.Model;
using Feeder.ReflectorNet;
using R3;

namespace Feeder.McpPlugin;

public interface IMcpManager : IDisposable
{
	Reflector Reflector { get; }

	IReadOnlyList<McpClientData> ActiveClients { get; }

	Observable<Unit> OnForceDisconnect { get; }

	Observable<McpClientData> OnClientConnected { get; }

	Observable<McpClientData> OnClientDisconnected { get; }

	Observable<IReadOnlyList<McpClientData>> OnClientsChanged { get; }

	IToolManager? ToolManager { get; }

	IPromptManager? PromptManager { get; }

	IResourceManager? ResourceManager { get; }

	ISystemToolManager? SystemToolManager { get; }
}
