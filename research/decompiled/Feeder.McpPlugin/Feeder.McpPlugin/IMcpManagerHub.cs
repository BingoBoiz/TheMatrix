using System;
using Feeder.McpPlugin.Common.Hub.Server;
using Feeder.McpPlugin.Common.Model;

namespace Feeder.McpPlugin;

public interface IMcpManagerHub : IConnectServerHub, IDisposable, IServerMcpManager, IServerToolHub, IServerPromptHub, IServerResourceHub
{
	VersionHandshakeResponse? VersionHandshakeStatus { get; }
}
