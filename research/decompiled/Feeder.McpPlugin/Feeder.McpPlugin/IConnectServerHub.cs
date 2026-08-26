using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using R3;

namespace Feeder.McpPlugin;

public interface IConnectServerHub : IDisposable
{
	ReadOnlyReactiveProperty<bool> KeepConnected { get; }

	ReadOnlyReactiveProperty<HubConnectionState> ConnectionState { get; }

	Observable<Unit> OnAuthorizationRejected { get; }

	Task<bool> Connect(CancellationToken cancellationToken = default(CancellationToken));

	Task Disconnect(CancellationToken cancellationToken = default(CancellationToken));

	void DisconnectImmediate();

	bool WaitForImmediateTeardown(TimeSpan timeout);
}
