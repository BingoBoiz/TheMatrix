using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Feeder.McpPlugin.Common;
using Feeder.McpPlugin.Common.Model;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using R3;

namespace Feeder.McpPlugin;

public abstract class BaseHubConnector : IConnectServerHub, IDisposable
{
	protected readonly ILogger _logger;

	protected readonly Feeder.McpPlugin.Common.Version _apiVersion;

	protected readonly IConnectionManager _connectionManager;

	protected readonly CancellationTokenSource _cancellationTokenSource;

	private const int MaxHandshakeFailures = 3;

	private readonly ThreadSafeBool _isDisposed;

	private readonly ThreadSafeBool _handshakeInFlight;

	private volatile VersionHandshakeResponse? lastHandshakeResponse;

	private int _consecutiveHandshakeFailures;

	protected readonly IDisposable _hubConnectionDisposable;

	protected readonly CompositeDisposable _serverEventsDisposables;

	public ReadOnlyReactiveProperty<HubConnectionState> ConnectionState => _connectionManager.ConnectionState;

	public ReadOnlyReactiveProperty<bool> KeepConnected => _connectionManager.KeepConnected;

	public Observable<Unit> OnAuthorizationRejected => _connectionManager.OnAuthorizationRejected;

	public VersionHandshakeResponse? VersionHandshakeStatus => lastHandshakeResponse;

	public BaseHubConnector(ILogger logger, Feeder.McpPlugin.Common.Version apiVersion, IConnectionManager connectionManager)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Expected O, but got Unknown
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Expected O, but got Unknown
		_cancellationTokenSource = new CancellationTokenSource();
		_isDisposed = new ThreadSafeBool();
		_handshakeInFlight = new ThreadSafeBool();
		_serverEventsDisposables = new CompositeDisposable();
		base._002Ector();
		_logger = logger ?? throw new ArgumentNullException("logger");
		_logger.LogTrace("{class} Ctor.", GetType().Name);
		_apiVersion = apiVersion ?? throw new ArgumentNullException("apiVersion");
		_connectionManager = connectionManager ?? throw new ArgumentNullException("connectionManager");
		CompositeDisposable val = new CompositeDisposable();
		Disposable.AddTo<IDisposable>(ObservableSubscribeExtensions.Subscribe<HubConnection>((Observable<HubConnection>)(object)_connectionManager.HubConnection, (Action<HubConnection>)OnHubConnectionChanged), (ICollection<IDisposable>)val);
		Disposable.AddTo<IDisposable>(ObservableSubscribeExtensions.Subscribe<Unit>(_connectionManager.OnTransportConnected, (Action<Unit>)delegate
		{
			OnConnectionEstablished();
		}), (ICollection<IDisposable>)val);
		_hubConnectionDisposable = (IDisposable)val;
	}

	public BaseHubConnector(ILogger logger, Feeder.McpPlugin.Common.Version apiVersion, string endpoint, IHubConnectionProvider hubConnectionProvider, int maxConsecutiveConnectionFailures = 0)
		: this(logger, apiVersion, new ConnectionManager(logger ?? throw new ArgumentNullException("logger"), apiVersion ?? throw new ArgumentNullException("apiVersion"), endpoint ?? throw new ArgumentNullException("endpoint"), hubConnectionProvider ?? throw new ArgumentNullException("hubConnectionProvider"), maxConsecutiveConnectionFailures))
	{
	}

	public Task<bool> Connect(CancellationToken cancellationToken = default(CancellationToken))
	{
		if (_isDisposed.Value)
		{
			_logger.LogWarning("{method} called on disposed object. Ignoring.", "Connect");
			return Task.FromResult(result: false);
		}
		_logger.LogDebug("{method} Connecting... to {endpoint}.", "Connect", _connectionManager.Endpoint);
		return _connectionManager.Connect(cancellationToken);
	}

	public Task Disconnect(CancellationToken cancellationToken = default(CancellationToken))
	{
		if (_isDisposed.Value)
		{
			_logger.LogWarning("{method} called on disposed object. Ignoring.", "Disconnect");
			return Task.CompletedTask;
		}
		_logger.LogDebug("{method} Disconnecting... from {endpoint}.", "Disconnect", _connectionManager.Endpoint);
		return _connectionManager.Disconnect(cancellationToken);
	}

	public void DisconnectImmediate()
	{
		if (_isDisposed.Value)
		{
			_logger.LogWarning("{method} called on disposed object. Ignoring.", "DisconnectImmediate");
			return;
		}
		_logger.LogDebug("{method}... from {endpoint}.", "DisconnectImmediate", _connectionManager.Endpoint);
		_connectionManager.DisconnectImmediate();
	}

	public bool WaitForImmediateTeardown(TimeSpan timeout)
	{
		if (_isDisposed.Value)
		{
			_logger.LogWarning("{method} called on disposed object. Ignoring.", "WaitForImmediateTeardown");
			return true;
		}
		return _connectionManager.WaitForImmediateTeardown(timeout);
	}

	public Task<VersionHandshakeResponse> PerformVersionHandshake(RequestVersionHandshake request)
	{
		return PerformVersionHandshake(request, _cancellationTokenSource.Token);
	}

	public async Task<VersionHandshakeResponse> PerformVersionHandshake(RequestVersionHandshake request, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (_isDisposed.Value)
		{
			throw new ObjectDisposedException(GetType().Name, "Can't perform version handshake on disposed object.");
		}
		_logger.LogTrace("{class} Performing version handshake.", GetType().Name);
		try
		{
			VersionHandshakeResponse versionHandshakeResponse = await _connectionManager.InvokeAsync<RequestVersionHandshake, VersionHandshakeResponse>("PerformVersionHandshake", request, cancellationToken);
			if (cancellationToken.IsCancellationRequested)
			{
				_logger.LogWarning("{class} Version handshake cancelled.", GetType().Name);
				return new VersionHandshakeResponse
				{
					ApiVersion = "Unknown",
					ServerVersion = "Unknown",
					Compatible = false,
					Message = "Version handshake was cancelled.",
					IsConnectionError = true
				};
			}
			if (versionHandshakeResponse == null)
			{
				_logger.LogError("{class} Version handshake failed: No response from server.", GetType().Name);
				return new VersionHandshakeResponse
				{
					ApiVersion = "Unknown",
					ServerVersion = "Unknown",
					Compatible = false,
					Message = "Version handshake failed with null response.",
					IsConnectionError = true
				};
			}
			_logger.LogInformation("{class} Version handshake completed. Compatible: {Compatible}, Message: {Message}", GetType().Name, versionHandshakeResponse.Compatible, versionHandshakeResponse.Message);
			return versionHandshakeResponse;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "{class} Version handshake failed: {Error}", GetType().Name, ex.Message);
			return new VersionHandshakeResponse
			{
				ApiVersion = "Unknown",
				ServerVersion = "Unknown",
				Compatible = false,
				Message = "Version handshake failed with exception: " + ex.Message,
				IsConnectionError = true
			};
		}
	}

	private void OnHubConnectionChanged(HubConnection? hubConnection)
	{
		if (_isDisposed.Value)
		{
			_logger.LogWarning("{method} called on disposed object. Ignoring.", "OnHubConnectionChanged");
			return;
		}
		_logger.LogTrace("{method} Clearing server events disposables.", "OnHubConnectionChanged");
		_serverEventsDisposables.Clear();
		if (hubConnection != null)
		{
			_consecutiveHandshakeFailures = 0;
			OnBeforeSubscribeToServerEvents();
			_logger.LogTrace("{method} Subscribing to server events.", "OnHubConnectionChanged");
			SubscribeOnServerEvents(hubConnection, _serverEventsDisposables);
		}
	}

	private async void OnConnectionEstablished()
	{
		if (_isDisposed.Value)
		{
			_logger.LogWarning("{method} called on disposed object. Ignoring.", "OnConnectionEstablished");
			return;
		}
		if (!_handshakeInFlight.TrySetTrue())
		{
			_logger.LogDebug("{method} Handshake already in flight. Skipping.", "OnConnectionEstablished");
			return;
		}
		try
		{
			await OnConnectionEstablishedCore();
		}
		catch (Exception exception)
		{
			_logger.LogError(exception, "{method} Unhandled exception during connection establishment.", "OnConnectionEstablished");
		}
		finally
		{
			_handshakeInFlight.TrySetFalse();
		}
	}

	private async Task OnConnectionEstablishedCore()
	{
		CancellationTokenSource cancellationTokenSource = _serverEventsDisposables.ToCancellationTokenSource();
		CancellationToken cancellationToken = cancellationTokenSource.Token;
		VersionHandshakeResponse versionHandshakeResponse = await PerformVersionHandshake(new RequestVersionHandshake
		{
			RequestID = Guid.NewGuid().ToString(),
			ApiVersion = _apiVersion.Api,
			PluginVersion = _apiVersion.Plugin,
			Environment = _apiVersion.Environment
		}, cancellationToken);
		if (cancellationToken.IsCancellationRequested)
		{
			return;
		}
		lastHandshakeResponse = versionHandshakeResponse;
		if (versionHandshakeResponse == null || versionHandshakeResponse.IsConnectionError)
		{
			_consecutiveHandshakeFailures++;
			string text = versionHandshakeResponse?.Message ?? "No response from server";
			_logger.LogWarning("{class} Version handshake failed ({count}/{max}). Reason: {reason}", GetType().Name, _consecutiveHandshakeFailures, 3, text);
			if (_consecutiveHandshakeFailures >= 3)
			{
				_logger.LogError("{class} Version handshake failed {count} times consecutively — disconnecting. Reason: {reason}", GetType().Name, _consecutiveHandshakeFailures, text);
				_connectionManager.DisconnectImmediate();
			}
		}
		else if (!versionHandshakeResponse.Compatible)
		{
			LogVersionMismatchError(versionHandshakeResponse);
			_logger.LogError("{class} Version mismatch — disconnecting. Server: {serverVersion}, API: {apiVersion}, Message: {message}", GetType().Name, versionHandshakeResponse.ServerVersion, versionHandshakeResponse.ApiVersion, versionHandshakeResponse.Message);
			_connectionManager.DisconnectImmediate();
		}
		else
		{
			_consecutiveHandshakeFailures = 0;
			_connectionManager.SetConnected();
			await OnConnectedAsync(cancellationToken);
		}
	}

	private void LogVersionMismatchError(VersionHandshakeResponse handshakeResponse)
	{
		string message = "API VERSION MISMATCH: " + handshakeResponse.Message;
		_logger.LogError(message);
	}

	protected virtual void OnBeforeSubscribeToServerEvents()
	{
	}

	protected abstract void SubscribeOnServerEvents(HubConnection hubConnection, CompositeDisposable disposables);

	protected virtual Task OnConnectedAsync(CancellationToken cancellationToken)
	{
		return Task.CompletedTask;
	}

	public virtual void Dispose()
	{
		if (_isDisposed.TrySetTrue())
		{
			GC.SuppressFinalize(this);
			_logger.LogDebug("{method} called.", "Dispose");
			if (!_cancellationTokenSource.IsCancellationRequested)
			{
				_cancellationTokenSource.Cancel();
			}
			_cancellationTokenSource.Dispose();
			_serverEventsDisposables.Dispose();
			_hubConnectionDisposable.Dispose();
			_connectionManager.Dispose();
			_logger.LogDebug("{method} completed.", "Dispose");
		}
	}
}
