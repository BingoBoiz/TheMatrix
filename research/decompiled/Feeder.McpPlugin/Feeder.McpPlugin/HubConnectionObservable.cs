using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using R3;

namespace Feeder.McpPlugin;

public class HubConnectionObservable : IDisposable
{
	protected readonly HubConnection _hubConnection;

	private readonly Subject<Exception?> _closedSubject = new Subject<Exception>();

	private readonly Subject<Exception?> _reconnectingSubject = new Subject<Exception>();

	private readonly Subject<string?> _reconnectedSubject = new Subject<string>();

	public Observable<Exception?> Closed => (Observable<Exception?>)(object)_closedSubject;

	public Observable<Exception?> Reconnecting => (Observable<Exception?>)(object)_reconnectingSubject;

	public Observable<string?> Reconnected => (Observable<string?>)(object)_reconnectedSubject;

	public Observable<HubConnectionState> State => Observable.Merge<HubConnectionState>(new Observable<HubConnectionState>[3]
	{
		ObservableExtensions.Select<Exception, HubConnectionState>((Observable<Exception>)(object)_closedSubject, (Func<Exception, HubConnectionState>)((Exception x) => (HubConnectionState)0)),
		ObservableExtensions.Select<Exception, HubConnectionState>((Observable<Exception>)(object)_reconnectingSubject, (Func<Exception, HubConnectionState>)((Exception x) => (HubConnectionState)3)),
		ObservableExtensions.Select<string, HubConnectionState>((Observable<string>)(object)_reconnectedSubject, (Func<string, HubConnectionState>)((string x) => (HubConnectionState)1))
	});

	public HubConnectionObservable(HubConnection hubConnection)
	{
		_hubConnection = hubConnection ?? throw new ArgumentNullException("hubConnection");
		_hubConnection.Closed += OnClosedConnection;
		_hubConnection.Reconnecting += OnReconnecting;
		_hubConnection.Reconnected += OnReconnected;
	}

	private Task OnClosedConnection(Exception? ex)
	{
		_closedSubject.OnNext(ex);
		return Task.CompletedTask;
	}

	private Task OnReconnecting(Exception? ex)
	{
		_reconnectingSubject.OnNext(ex);
		return Task.CompletedTask;
	}

	private Task OnReconnected(string? connectionId)
	{
		_reconnectedSubject.OnNext(connectionId);
		return Task.CompletedTask;
	}

	public virtual void Dispose()
	{
		_hubConnection.Closed -= OnClosedConnection;
		_hubConnection.Reconnecting -= OnReconnecting;
		_hubConnection.Reconnected -= OnReconnected;
		_closedSubject.Dispose();
		_reconnectingSubject.Dispose();
		_reconnectedSubject.Dispose();
	}
}
