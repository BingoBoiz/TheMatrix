using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using R3;

namespace Feeder.McpPlugin;

public interface IConnectionManager : IConnection, IDisposable
{
	string Endpoint { get; }

	ReadOnlyReactiveProperty<HubConnection?> HubConnection { get; }

	CancellationToken ConnectionCancellationToken { get; }

	Observable<Unit> OnTransportConnected { get; }

	void SetConnected();

	void NotifyAuthorizationRejected();

	Task InvokeAsync<TInput>(string methodName, TInput input, CancellationToken cancellationToken = default(CancellationToken));

	Task<TResult> InvokeAsync<TInput, TResult>(string methodName, TInput input, CancellationToken cancellationToken = default(CancellationToken));

	Task<TResult> InvokeAsync<TResult>(string methodName, CancellationToken cancellationToken = default(CancellationToken));
}
