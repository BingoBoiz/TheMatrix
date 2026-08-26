using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Feeder.McpPlugin.Common;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using R3;

namespace Feeder.McpPlugin;

public class ConnectionManager : IConnectionManager, IConnection, IDisposable, IAsyncDisposable
{
	private const int MaxConsecutiveRejections = 3;

	protected readonly string _guid;

	protected readonly ILogger _logger;

	protected readonly Feeder.McpPlugin.Common.Version _apiVersion;

	protected readonly string _endpoint;

	protected readonly IHubConnectionProvider _hubConnectionBuilder;

	protected readonly ReactiveProperty<bool> _continueToReconnect;

	protected readonly ReactiveProperty<HubConnection?> _hubConnection;

	protected readonly ReactiveProperty<HubConnectionState> _connectionState;

	private readonly Subject<Unit> _authorizationRejected;

	private readonly Subject<Unit> _transportConnected;

	protected readonly CompositeDisposable _disposables;

	protected readonly CancellationTokenSource _cancellationTokenSource;

	private readonly SemaphoreSlim _gate;

	private readonly SemaphoreSlim _ongoingConnectionGate;

	private readonly ThreadSafeBool _isDisposed;

	private readonly SerialDisposable _hubStateSubscription;

	private readonly SerialDisposable _hubObservableReconnectSubscription;

	private readonly ReadOnlyReactiveProperty<HubConnectionState> _connectionStateReadOnly;

	private readonly ReadOnlyReactiveProperty<HubConnection?> _hubConnectionReadOnly;

	private readonly ReadOnlyReactiveProperty<bool> _keepConnectedReadOnly;

	private HubConnectionLogger? hubConnectionLogger;

	private HubConnectionObservable? hubConnectionObservable;

	private CancellationTokenSource? internalCts;

	private volatile Task<bool>? _ongoingConnectionTask;

	private Task? _pendingImmediateTeardown;

	private readonly int _maxConsecutiveConnectionFailures;

	protected virtual TimeSpan RejectionThreshold { get; }

	public ReadOnlyReactiveProperty<HubConnectionState> ConnectionState => _connectionStateReadOnly;

	public ReadOnlyReactiveProperty<HubConnection?> HubConnection => _hubConnectionReadOnly;

	public ReadOnlyReactiveProperty<bool> KeepConnected => _keepConnectedReadOnly;

	public Observable<Unit> OnAuthorizationRejected => (Observable<Unit>)(object)_authorizationRejected;

	public Observable<Unit> OnTransportConnected => (Observable<Unit>)(object)_transportConnected;

	public string Endpoint => _endpoint;

	public CancellationToken ConnectionCancellationToken => internalCts?.Token ?? CancellationToken.None;

	public async Task<bool> Connect(CancellationToken cancellationToken = default(CancellationToken))
	{
		if (_isDisposed.Value)
		{
			_logger.LogWarning("{class}[{guid}] {method} called but already disposed, ignored.", "ConnectionManager", _guid, "Connect");
			return false;
		}
		_logger.LogDebug("{class}[{guid}] {method} called.", "ConnectionManager", _guid, "Connect");
		if (cancellationToken.IsCancellationRequested)
		{
			_logger.LogWarning("{class}[{guid}] {method} Connection canceled before starting for endpoint: {endpoint}", "ConnectionManager", _guid, "Connect", Endpoint);
			return false;
		}
		await _ongoingConnectionGate.WaitAsync(cancellationToken);
		Task<bool> ongoingConnectionTask = _ongoingConnectionTask;
		_ongoingConnectionGate.Release();
		if (ongoingConnectionTask != null)
		{
			return await WaitForConnectionCompletion(ongoingConnectionTask, cancellationToken);
		}
		try
		{
			_logger.LogDebug("{class}[{guid}] {method} acquiring gate.", "ConnectionManager", _guid, "Connect");
			await _gate.WaitAsync(cancellationToken);
			_logger.LogDebug("{class}[{guid}] {method} acquired gate.", "ConnectionManager", _guid, "Connect");
		}
		catch (OperationCanceledException)
		{
			_logger.LogWarning("{class}[{guid}] {method} Connection canceled while waiting for gate for endpoint: {endpoint}", "ConnectionManager", _guid, "Connect", Endpoint);
			return false;
		}
		try
		{
			if (cancellationToken.IsCancellationRequested)
			{
				_logger.LogWarning("{class}[{guid}] {method} Connection canceled before starting for endpoint: {endpoint}", "ConnectionManager", _guid, "Connect", Endpoint);
				return false;
			}
			if (_isDisposed.Value)
			{
				_logger.LogWarning("{class}[{guid}] {method} called but already disposed, ignored.", "ConnectionManager", _guid, "Connect");
				return false;
			}
			await _ongoingConnectionGate.WaitAsync(cancellationToken);
			ongoingConnectionTask = _ongoingConnectionTask;
			_ongoingConnectionGate.Release();
			if (ongoingConnectionTask != null)
			{
				_logger.LogDebug("{class}[{guid}] {method} Connection already in progress after acquiring gate, releasing gate and waiting.", "ConnectionManager", _guid, "Connect");
				_gate.Release();
				return await WaitForConnectionCompletion(ongoingConnectionTask, cancellationToken);
			}
			HubConnection currentValue = ((ReadOnlyReactiveProperty<HubConnection>)(object)_hubConnection).CurrentValue;
			HubConnectionState? val = ((currentValue != null) ? new HubConnectionState?(currentValue.State) : ((HubConnectionState?)null));
			bool flag;
			if (val.HasValue)
			{
				HubConnectionState valueOrDefault = val.GetValueOrDefault();
				if (valueOrDefault - 1 <= 1)
				{
					flag = true;
					goto IL_050a;
				}
			}
			flag = false;
			goto IL_050a;
			IL_050a:
			if (flag)
			{
				_logger.LogDebug("{class}[{guid}] {method} Already connected. Ignoring.", "ConnectionManager", _guid, "Connect");
				return true;
			}
			CancelInternalToken(dispose: true);
			internalCts = CancellationTokenSource.CreateLinkedTokenSource(new CancellationToken[1] { cancellationToken });
			cancellationToken = internalCts.Token;
			_continueToReconnect.Value = true;
			await _ongoingConnectionGate.WaitAsync(cancellationToken);
			Task<bool> task = (_ongoingConnectionTask = InternalConnect(cancellationToken));
			_ongoingConnectionGate.Release();
			bool result;
			try
			{
				result = await task;
			}
			finally
			{
				await _ongoingConnectionGate.WaitAsync(CancellationToken.None);
				_ongoingConnectionTask = null;
				_ongoingConnectionGate.Release();
			}
			return result;
		}
		finally
		{
			_logger.LogDebug("{class}[{guid}] {method} releasing gate.", "ConnectionManager", _guid, "Connect");
			_gate.Release();
		}
	}

	private async Task<bool> WaitForConnectionCompletion(Task<bool> ongoingTask, CancellationToken cancellationToken)
	{
		_logger.LogDebug("{class}[{guid}] {method} Connection already in progress, waiting for existing attempt.", "ConnectionManager", _guid, "WaitForConnectionCompletion");
		try
		{
			if (await Task.WhenAny(new Task[2]
			{
				ongoingTask,
				Task.Delay(-1, cancellationToken)
			}) != ongoingTask)
			{
				_logger.LogWarning("{class}[{guid}] {method} Waiting for ongoing connection was canceled for endpoint: {endpoint}", "ConnectionManager", _guid, "WaitForConnectionCompletion", Endpoint);
				return false;
			}
			return await ongoingTask;
		}
		catch (OperationCanceledException)
		{
			_logger.LogWarning("{class}[{guid}] {method} Ongoing connection was canceled for endpoint: {endpoint}", "ConnectionManager", _guid, "WaitForConnectionCompletion", Endpoint);
			return false;
		}
	}

	private async Task<bool> InternalConnect(CancellationToken cancellationToken)
	{
		_ = 2;
		try
		{
			if (_isDisposed.Value)
			{
				_logger.LogWarning("{class}[{guid}] {method} called but already disposed, ignored.", "ConnectionManager", _guid, "InternalConnect");
				return false;
			}
			if (cancellationToken.IsCancellationRequested)
			{
				_logger.LogWarning("{class}[{guid}] {method} Connection canceled before creating HubConnection for endpoint: {endpoint}", "ConnectionManager", _guid, "InternalConnect", Endpoint);
				return false;
			}
			_logger.LogDebug("{class}[{guid}] {method} called.", "ConnectionManager", _guid, "InternalConnect");
			if (!(await CreateHubConnectionIfNeeded(cancellationToken)))
			{
				return false;
			}
			if (cancellationToken.IsCancellationRequested)
			{
				_logger.LogWarning("{class}[{guid}] {method} Connection canceled before starting connection loop for endpoint: {endpoint}", "ConnectionManager", _guid, "InternalConnect", Endpoint);
				return false;
			}
			await Task.Delay(TimeSpan.FromSeconds(1.0), cancellationToken);
			if (cancellationToken.IsCancellationRequested)
			{
				_logger.LogWarning("{class}[{guid}] {method} Connection canceled before starting connection loop for endpoint: {endpoint}", "ConnectionManager", _guid, "InternalConnect", Endpoint);
				return false;
			}
			return await StartConnectionLoop(cancellationToken);
		}
		catch (OperationCanceledException)
		{
			_logger.LogWarning("{class}[{guid}] {method} Connection was canceled for endpoint: {endpoint}", "ConnectionManager", _guid, "InternalConnect", Endpoint);
			return false;
		}
		catch (InvalidOperationException ex2)
		{
			_logger.LogError("{class}[{guid}] {method} Invalid operation during connection: {message}\n{stackTrace}", "ConnectionManager", _guid, "InternalConnect", ex2.Message, ex2.StackTrace);
			return false;
		}
		catch (HubException ex3)
		{
			_logger.LogError("{class}[{guid}] {method} SignalR HubException during connection: {message}\n{stackTrace}", "ConnectionManager", _guid, "InternalConnect", ex3.Message, ex3.StackTrace);
			return false;
		}
		catch (Exception ex4)
		{
			_logger.LogError("{class}[{guid}] {method} Unexpected error during connection: {message}\n{stackTrace}", "ConnectionManager", _guid, "InternalConnect", ex4.Message, ex4.StackTrace);
			return false;
		}
	}

	private async Task<bool> EnsureConnection(CancellationToken cancellationToken)
	{
		HubConnection currentValue = ((ReadOnlyReactiveProperty<HubConnection>)(object)_hubConnection).CurrentValue;
		HubConnectionState? val = ((currentValue != null) ? new HubConnectionState?(currentValue.State) : ((HubConnectionState?)null));
		if (val.HasValue && (int)val.GetValueOrDefault() == 1)
		{
			return true;
		}
		if (!((ReadOnlyReactiveProperty<bool>)(object)_continueToReconnect).CurrentValue)
		{
			_logger.LogWarning("{class}[{guid}] {method} Connection not available and auto-reconnect disabled for endpoint: {endpoint}", "ConnectionManager", _guid, "EnsureConnection", Endpoint);
			return false;
		}
		_logger.LogDebug("{class}[{guid}] {method} Connection is not established. Attempting to connect to: {endpoint}", "ConnectionManager", _guid, "EnsureConnection", Endpoint);
		await Connect(cancellationToken);
		HubConnection currentValue2 = ((ReadOnlyReactiveProperty<HubConnection>)(object)_hubConnection).CurrentValue;
		val = ((currentValue2 != null) ? new HubConnectionState?(currentValue2.State) : ((HubConnectionState?)null));
		if (!val.HasValue || (int)val.GetValueOrDefault() != 1)
		{
			_logger.LogWarning("{class}[{guid}] {method} Failed to establish connection to remote endpoint: {endpoint}", "ConnectionManager", _guid, "EnsureConnection", Endpoint);
			return false;
		}
		return true;
	}

	private async Task<bool> CreateHubConnectionIfNeeded(CancellationToken cancellationToken)
	{
		HubConnection value = _hubConnection.Value;
		if (value != null && (int)value.State != 0)
		{
			return true;
		}
		hubConnectionLogger?.Dispose();
		hubConnectionObservable?.Dispose();
		_hubObservableReconnectSubscription.Disposable = null;
		if (value != null)
		{
			_logger.LogDebug("{class}[{guid}] {method} Disposing existing Disconnected HubConnection for endpoint: {endpoint}", "ConnectionManager", _guid, "CreateHubConnectionIfNeeded", Endpoint);
			try
			{
				await value.DisposeAsync();
			}
			catch (Exception exception)
			{
				_logger.LogDebug(exception, "{class}[{guid}] {method} DisposeAsync of stale HubConnection failed", "ConnectionManager", _guid, "CreateHubConnectionIfNeeded");
			}
			_hubConnection.Value = null;
		}
		_logger.LogDebug("{class}[{guid}] {method} Creating new HubConnection instance for endpoint: {endpoint}", "ConnectionManager", _guid, "CreateHubConnectionIfNeeded", Endpoint);
		HubConnection val = await _hubConnectionBuilder.CreateConnectionAsync(Endpoint);
		if (val == null)
		{
			_logger.LogError("{class}[{guid}] {method} Failed to create HubConnection instance. Check connection configuration for endpoint: {endpoint}", "ConnectionManager", _guid, "CreateHubConnectionIfNeeded", Endpoint);
			return false;
		}
		if (cancellationToken.IsCancellationRequested)
		{
			_logger.LogWarning("{class}[{guid}] {method} Connection canceled before setting up HubConnection for endpoint: {endpoint}", "ConnectionManager", _guid, "CreateHubConnectionIfNeeded", Endpoint);
			try
			{
				await val.DisposeAsync();
			}
			catch
			{
			}
			return false;
		}
		_logger.LogDebug("{class}[{guid}] {method} Successfully created HubConnection instance for endpoint: {endpoint}", "ConnectionManager", _guid, "CreateHubConnectionIfNeeded", Endpoint);
		_hubConnection.Value = val;
		SetupHubConnectionLogging(val);
		SetupHubConnectionObservables(val);
		return true;
	}

	private void SetupHubConnectionLogging(HubConnection hubConnection)
	{
		hubConnectionLogger = new HubConnectionLogger(_logger, hubConnection, _guid);
	}

	private void SetupHubConnectionObservables(HubConnection hubConnection)
	{
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Expected O, but got Unknown
		hubConnectionObservable = new HubConnectionObservable(hubConnection);
		IDisposable disposable = ObservableSubscribeExtensions.Subscribe<string>(ObservableExtensions.Where<string>(hubConnectionObservable.Reconnected, (Func<string, bool>)((string _) => ((ReadOnlyReactiveProperty<bool>)(object)_continueToReconnect).CurrentValue && !_cancellationTokenSource.IsCancellationRequested)), (Action<string>)delegate
		{
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			_logger.LogInformation("{class}[{guid}] {method} SignalR auto-reconnect succeeded for endpoint: {endpoint}", "ConnectionManager", _guid, "SetupHubConnectionObservables", Endpoint);
			_transportConnected.OnNext(Unit.Default);
		});
		IDisposable disposable2 = ObservableSubscribeExtensions.Subscribe<Exception>(ObservableExtensions.Where<Exception>(hubConnectionObservable.Closed, (Func<Exception, bool>)((Exception _) => ((ReadOnlyReactiveProperty<bool>)(object)_continueToReconnect).CurrentValue && !_cancellationTokenSource.IsCancellationRequested)), (Action<Exception>)delegate(Exception ex)
		{
			_logger.LogWarning(ex, "{class}[{guid}] {method} Connection closed (auto-reconnect exhausted). Attempting fresh reconnection to: {endpoint}", "ConnectionManager", _guid, "SetupHubConnectionObservables", Endpoint);
			Connect(_cancellationTokenSource.Token).ContinueWith((Task<bool> t) => t.Exception, TaskContinuationOptions.ExecuteSynchronously);
		});
		_hubObservableReconnectSubscription.Disposable = (IDisposable)new CompositeDisposable(new IDisposable[2] { disposable, disposable2 });
	}

	private async Task<bool> StartConnectionLoop(CancellationToken cancellationToken)
	{
		_logger.LogDebug("{class}[{guid}] {method} Starting connection loop for endpoint: {endpoint}", "ConnectionManager", _guid, "StartConnectionLoop", Endpoint);
		int consecutiveRejections = 0;
		int consecutiveFailures = 0;
		while (!cancellationToken.IsCancellationRequested && ((ReadOnlyReactiveProperty<bool>)(object)_continueToReconnect).CurrentValue)
		{
			if (await AttemptConnection(cancellationToken))
			{
				consecutiveFailures = 0;
				try
				{
					await Task.Delay(RejectionThreshold, cancellationToken);
				}
				catch (OperationCanceledException)
				{
					break;
				}
				HubConnection currentValue = ((ReadOnlyReactiveProperty<HubConnection>)(object)_hubConnection).CurrentValue;
				HubConnectionState? val = ((currentValue != null) ? new HubConnectionState?(currentValue.State) : ((HubConnectionState?)null));
				if (val.HasValue && (int)val.GetValueOrDefault() == 1)
				{
					return true;
				}
				_connectionState.Value = (HubConnectionState)0;
				consecutiveRejections++;
				_logger.LogWarning("{class}[{guid}] {method} Connection to {endpoint} was closed by the server immediately after handshake ({count}/{max}). This typically indicates authorization failure (invalid or revoked token).", "ConnectionManager", _guid, "StartConnectionLoop", Endpoint, consecutiveRejections, 3);
				if (consecutiveRejections >= 3)
				{
					_logger.LogError("{class}[{guid}] {method} Connection to {endpoint} rejected {count} times consecutively. Stopping reconnection attempts. The server is likely rejecting this client due to an authorization issue. Please check your authorization token and try reconnecting.", "ConnectionManager", _guid, "StartConnectionLoop", Endpoint, consecutiveRejections);
					_continueToReconnect.Value = false;
					_connectionState.Value = (HubConnectionState)0;
					_authorizationRejected.OnNext(Unit.Default);
					return false;
				}
			}
			else
			{
				consecutiveRejections = 0;
				consecutiveFailures++;
				if (_maxConsecutiveConnectionFailures > 0 && consecutiveFailures >= _maxConsecutiveConnectionFailures)
				{
					_logger.LogWarning("{class}[{guid}] {method} Connection to {endpoint} failed {count} times consecutively (endpoint unreachable). Stopping reconnection attempts; reconnect once the server is reachable.", "ConnectionManager", _guid, "StartConnectionLoop", Endpoint, consecutiveFailures);
					_continueToReconnect.Value = false;
					_connectionState.Value = (HubConnectionState)0;
					break;
				}
			}
			if (cancellationToken.IsCancellationRequested || !((ReadOnlyReactiveProperty<bool>)(object)_continueToReconnect).CurrentValue)
			{
				break;
			}
			await WaitBeforeRetry(cancellationToken);
		}
		_logger.LogDebug("{class}[{guid}] {method} Connection loop terminated for endpoint: {endpoint}", "ConnectionManager", _guid, "StartConnectionLoop", Endpoint);
		return false;
	}

	protected virtual async Task<bool> AttemptConnection(CancellationToken cancellationToken)
	{
		HubConnection currentValue = ((ReadOnlyReactiveProperty<HubConnection>)(object)_hubConnection).CurrentValue;
		if (currentValue == null)
		{
			return false;
		}
		HubConnectionState state = currentValue.State;
		if (state - 1 <= 1)
		{
			return true;
		}
		_logger.LogInformation("{class}[{guid}] {method} Starting connection attempt to: {endpoint}", "ConnectionManager", _guid, "AttemptConnection", Endpoint);
		try
		{
			Task connectionTask = currentValue.StartAsync(cancellationToken);
			Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(30.0), cancellationToken);
			if (await Task.WhenAny(new Task[2] { connectionTask, timeoutTask }) == timeoutTask)
			{
				_logger.LogWarning("{class}[{guid}] {method} Connection attempt timed out after 30 seconds for endpoint: {endpoint}", "ConnectionManager", _guid, "AttemptConnection", Endpoint);
				connectionTask.ContinueWith((Task t) => t.Exception, TaskContinuationOptions.ExecuteSynchronously);
				return false;
			}
			if (connectionTask.IsCompletedSuccessfully)
			{
				_logger.LogInformation("{class}[{guid}] {method} Connection established successfully to: {endpoint}", "ConnectionManager", _guid, "AttemptConnection", Endpoint);
				_transportConnected.OnNext(Unit.Default);
				return true;
			}
			_logger.LogWarning("{class}[{guid}] {method} Connection attempt failed for endpoint: {endpoint}. Exception: {exception}", "ConnectionManager", _guid, "AttemptConnection", Endpoint, connectionTask.Exception?.Message);
		}
		catch (OperationCanceledException)
		{
			_logger.LogWarning("{class}[{guid}] {method} Connection attempt canceled for endpoint: {endpoint}", "ConnectionManager", _guid, "AttemptConnection", Endpoint);
		}
		catch (HubException ex2)
		{
			_logger.LogError("{class}[{guid}] {method} SignalR HubException during connection attempt to endpoint: {endpoint}. Error: {error}", "ConnectionManager", _guid, "AttemptConnection", Endpoint, ex2.Message);
		}
		catch (Exception ex3)
		{
			_logger.LogError(ex3, "{class}[{guid}] {method} Connection attempt failed for endpoint: {endpoint}. Error: {error}", "ConnectionManager", _guid, "AttemptConnection", Endpoint, ex3.Message);
		}
		return false;
	}

	protected virtual async Task WaitBeforeRetry(CancellationToken cancellationToken)
	{
		if (cancellationToken.IsCancellationRequested)
		{
			return;
		}
		_logger.LogTrace("{class}[{guid}] {method} Waiting 5 seconds before retry for endpoint: {endpoint}", "ConnectionManager", _guid, "WaitBeforeRetry", Endpoint);
		try
		{
			await Task.Delay(TimeSpan.FromSeconds(5.0), cancellationToken);
		}
		catch (OperationCanceledException)
		{
		}
		catch (ObjectDisposedException)
		{
		}
	}

	public void SetConnected()
	{
		if (!_isDisposed.Value)
		{
			_connectionState.Value = (HubConnectionState)1;
		}
	}

	public void NotifyAuthorizationRejected()
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		if (!_isDisposed.Value)
		{
			_authorizationRejected.OnNext(Unit.Default);
		}
	}

	public ConnectionManager(ILogger logger, Feeder.McpPlugin.Common.Version apiVersion, string endpoint, IHubConnectionProvider hubConnectionBuilder, int maxConsecutiveConnectionFailures = 0)
	{
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Expected O, but got Unknown
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Expected O, but got Unknown
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Expected O, but got Unknown
		RejectionThreshold = TimeSpan.FromSeconds(3.0);
		_guid = Guid.NewGuid().ToString();
		_continueToReconnect = new ReactiveProperty<bool>(false);
		_hubConnection = new ReactiveProperty<HubConnection>();
		_connectionState = new ReactiveProperty<HubConnectionState>((HubConnectionState)0);
		_authorizationRejected = new Subject<Unit>();
		_transportConnected = new Subject<Unit>();
		_disposables = new CompositeDisposable();
		_gate = new SemaphoreSlim(1, 1);
		_ongoingConnectionGate = new SemaphoreSlim(1, 1);
		_isDisposed = new ThreadSafeBool();
		_hubStateSubscription = new SerialDisposable();
		_hubObservableReconnectSubscription = new SerialDisposable();
		base._002Ector();
		_logger = logger ?? throw new ArgumentNullException("logger");
		_logger.LogTrace("{class}[{guid}] Ctor.", "ConnectionManager", _guid);
		_maxConsecutiveConnectionFailures = maxConsecutiveConnectionFailures;
		_apiVersion = apiVersion ?? throw new ArgumentNullException("apiVersion");
		_endpoint = endpoint ?? throw new ArgumentNullException("endpoint");
		_hubConnectionBuilder = hubConnectionBuilder ?? throw new ArgumentNullException("hubConnectionBuilder");
		_cancellationTokenSource = _disposables.ToCancellationTokenSource();
		_connectionStateReadOnly = ((ReadOnlyReactiveProperty<HubConnectionState>)(object)_connectionState).ToReadOnlyReactiveProperty();
		_hubConnectionReadOnly = ((ReadOnlyReactiveProperty<HubConnection>)(object)_hubConnection).ToReadOnlyReactiveProperty();
		_keepConnectedReadOnly = ((ReadOnlyReactiveProperty<bool>)(object)_continueToReconnect).ToReadOnlyReactiveProperty();
		Disposable.AddTo<IDisposable>(ObservableSubscribeExtensions.Subscribe<HubConnection>((Observable<HubConnection>)(object)_hubConnection, (Action<HubConnection>)delegate(HubConnection hubConnection)
		{
			//IL_0073: Unknown result type (might be due to invalid IL or missing references)
			//IL_007d: Expected O, but got Unknown
			if (hubConnection == null)
			{
				_hubStateSubscription.Disposable = null;
				_connectionState.Value = (HubConnectionState)0;
			}
			else
			{
				HubConnectionObservable hubConnectionObservable = hubConnection.ToObservable();
				IDisposable disposable = ObservableSubscribeExtensions.Subscribe<HubConnectionState>(ObservableExtensions.Where<HubConnectionState>(hubConnectionObservable.State, (Func<HubConnectionState, bool>)delegate(HubConnectionState state)
				{
					//IL_0000: Unknown result type (might be due to invalid IL or missing references)
					//IL_0002: Invalid comparison between Unknown and I4
					return (int)state != 1;
				}), (Action<HubConnectionState>)delegate(HubConnectionState state)
				{
					//IL_0006: Unknown result type (might be due to invalid IL or missing references)
					_connectionState.Value = state;
				});
				_hubStateSubscription.Disposable = (IDisposable)new CompositeDisposable(new IDisposable[2] { hubConnectionObservable, disposable });
			}
		}), (ICollection<IDisposable>)_disposables);
	}

	public async Task InvokeAsync<TInput>(string methodName, TInput input, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (_isDisposed.Value)
		{
			_logger.LogWarning("{class}[{guid}] {method} called but already disposed, ignored.", "ConnectionManager", _guid, "InvokeAsync");
		}
		else if (await EnsureConnection(cancellationToken))
		{
			await ExecuteHubMethodAsync(methodName, (HubConnection hubConnection) => HubConnectionExtensions.InvokeAsync(hubConnection, methodName, (object)input, cancellationToken));
		}
	}

	public async Task<TResult> InvokeAsync<TInput, TResult>(string methodName, TInput input, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (_isDisposed.Value)
		{
			_logger.LogWarning("{class}[{guid}] {method} called but already disposed, ignored.", "ConnectionManager", _guid, "InvokeAsync");
			return default(TResult);
		}
		if (!(await EnsureConnection(cancellationToken)))
		{
			return default(TResult);
		}
		return await ExecuteHubMethodAsync(methodName, (HubConnection hubConnection) => HubConnectionExtensions.InvokeAsync<TResult>(hubConnection, methodName, (object)input, cancellationToken));
	}

	public async Task<TResult> InvokeAsync<TResult>(string methodName, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (_isDisposed.Value)
		{
			_logger.LogWarning("{class}[{guid}] {method} called but already disposed, ignored.", "ConnectionManager", _guid, "InvokeAsync");
			return default(TResult);
		}
		if (!(await EnsureConnection(cancellationToken)))
		{
			return default(TResult);
		}
		return await ExecuteHubMethodAsync(methodName, (HubConnection hubConnection) => HubConnectionExtensions.InvokeAsync<TResult>(hubConnection, methodName, cancellationToken));
	}

	public void Dispose()
	{
		if (!_isDisposed.TrySetTrue())
		{
			return;
		}
		GC.SuppressFinalize(this);
		_logger.LogDebug("{class}[{guid}] {method}.", "ConnectionManager", _guid, "Dispose");
		DisposeCommonSync();
		bool flag = _gate.Wait(TimeSpan.FromSeconds(5.0));
		try
		{
			if (!flag)
			{
				_logger.LogWarning("{class}[{guid}] {method} Could not acquire gate within timeout during Dispose. Proceeding with cleanup anyway.", "ConnectionManager", _guid, "Dispose");
			}
			if (!_hubConnection.IsDisposed)
			{
				try
				{
					_hubConnection.Value = null;
					((ReadOnlyReactiveProperty<HubConnection>)(object)_hubConnection).Dispose();
					return;
				}
				catch (Exception ex)
				{
					_logger.LogError("{class}[{guid}] {method} Error during disposal: {message}", "ConnectionManager", _guid, "Dispose", ex.Message);
					return;
				}
			}
		}
		finally
		{
			if (flag)
			{
				_gate.Release();
			}
			try
			{
				_gate.Dispose();
			}
			catch (ObjectDisposedException)
			{
			}
			try
			{
				_ongoingConnectionGate.Dispose();
			}
			catch (ObjectDisposedException)
			{
			}
			_logger.LogDebug("{class}[{guid}] {method} completed.", "ConnectionManager", _guid, "Dispose");
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (!_isDisposed.TrySetTrue())
		{
			return;
		}
		GC.SuppressFinalize(this);
		_logger.LogDebug("{class}[{guid}] {method}.", "ConnectionManager", _guid, "DisposeAsync");
		DisposeCommonSync();
		bool isGateAcquired = await _gate.WaitAsync(TimeSpan.FromSeconds(5.0));
		try
		{
			if (((ReadOnlyReactiveProperty<HubConnection>)(object)_hubConnection).CurrentValue != null)
			{
				try
				{
					await ((ReadOnlyReactiveProperty<HubConnection>)(object)_hubConnection).CurrentValue.StopAsync(default(CancellationToken));
					await ((ReadOnlyReactiveProperty<HubConnection>)(object)_hubConnection).CurrentValue.DisposeAsync();
					if (!_hubConnection.IsDisposed)
					{
						_hubConnection.Value = null;
					}
				}
				catch (Exception ex)
				{
					_logger.LogError("{class}[{guid}] {method} Error during async disposal: {message}\n{stackTrace}", "ConnectionManager", _guid, "DisposeAsync", ex.Message, ex.StackTrace);
				}
			}
			if (!_hubConnection.IsDisposed)
			{
				((ReadOnlyReactiveProperty<HubConnection>)(object)_hubConnection).Dispose();
			}
		}
		finally
		{
			if (isGateAcquired)
			{
				_gate.Release();
			}
			try
			{
				_gate.Dispose();
			}
			catch (ObjectDisposedException)
			{
			}
			try
			{
				_ongoingConnectionGate.Dispose();
			}
			catch (ObjectDisposedException)
			{
			}
			_logger.LogDebug("{class}[{guid}] {method} completed.", "ConnectionManager", _guid, "DisposeAsync");
		}
	}

	private void DisposeCommonSync()
	{
		CancelInternalToken(dispose: true);
		_disposables.Dispose();
		if (!_continueToReconnect.IsDisposed)
		{
			_continueToReconnect.Value = false;
		}
		hubConnectionLogger?.Dispose();
		hubConnectionObservable?.Dispose();
		hubConnectionLogger = null;
		hubConnectionObservable = null;
		_hubStateSubscription.Dispose();
		_hubObservableReconnectSubscription.Dispose();
		((ReadOnlyReactiveProperty<HubConnectionState>)(object)_connectionState).Dispose();
		((ReadOnlyReactiveProperty<bool>)(object)_continueToReconnect).Dispose();
		_connectionStateReadOnly.Dispose();
		_hubConnectionReadOnly.Dispose();
		_keepConnectedReadOnly.Dispose();
	}

	private async Task ExecuteHubMethodAsync(string methodName, Func<HubConnection, Task> hubMethod)
	{
		HubConnection connection = ((ReadOnlyReactiveProperty<HubConnection>)(object)_hubConnection).CurrentValue;
		if (connection == null)
		{
			_logger.LogError("{class}[{guid}] {method} HubConnection is null. Cannot invoke method '{methodName}' on endpoint: {endpoint}", "ConnectionManager", _guid, "ExecuteHubMethodAsync", methodName, Endpoint);
			return;
		}
		if ((int)connection.State != 1)
		{
			_connectionState.Value = connection.State;
			_logger.LogWarning("{class}[{guid}] {method} HubConnection is not active (State: {state}). Skipping method '{methodName}' on endpoint: {endpoint}", "ConnectionManager", _guid, "ExecuteHubMethodAsync", connection.State, methodName, Endpoint);
			return;
		}
		try
		{
			await hubMethod(connection);
			_logger.LogDebug("{class}[{guid}] {method} Successfully invoked method '{methodName}' on endpoint: {endpoint}", "ConnectionManager", _guid, "ExecuteHubMethodAsync", methodName, Endpoint);
		}
		catch (InvalidOperationException ex) when (ex.Message.Contains("not active"))
		{
			_connectionState.Value = connection.State;
			_logger.LogWarning("{class}[{guid}] {method} Connection became inactive while invoking '{methodName}' on endpoint: {endpoint}. Error: {message}", "ConnectionManager", _guid, "ExecuteHubMethodAsync", methodName, Endpoint, ex.Message);
		}
		catch (OperationCanceledException)
		{
			_logger.LogWarning("{class}[{guid}] {method} Invocation of '{methodName}' was canceled on endpoint: {endpoint}", "ConnectionManager", _guid, "ExecuteHubMethodAsync", methodName, Endpoint);
		}
		catch (Exception ex3)
		{
			_logger.LogError(ex3, "{class}[{guid}] {method} Failed to invoke method '{methodName}' on endpoint: {endpoint}. Error: {message}", "ConnectionManager", _guid, "ExecuteHubMethodAsync", methodName, Endpoint, ex3.Message);
			throw;
		}
	}

	private async Task<TResult> ExecuteHubMethodAsync<TResult>(string methodName, Func<HubConnection, Task<TResult>> hubMethod)
	{
		HubConnection connection = ((ReadOnlyReactiveProperty<HubConnection>)(object)_hubConnection).CurrentValue;
		if (connection == null)
		{
			_logger.LogError("{class}[{guid}] {method} HubConnection is null. Cannot invoke method '{methodName}' on endpoint: {endpoint}", "ConnectionManager", _guid, "ExecuteHubMethodAsync", methodName, Endpoint);
			return default(TResult);
		}
		if ((int)connection.State != 1)
		{
			_logger.LogWarning("{class}[{guid}] {method} HubConnection is not active (State: {state}). Skipping method '{methodName}' on endpoint: {endpoint}", "ConnectionManager", _guid, "ExecuteHubMethodAsync", connection.State, methodName, Endpoint);
			return default(TResult);
		}
		try
		{
			TResult result = await hubMethod(connection);
			_logger.LogDebug("{class}[{guid}] {method} Successfully invoked method '{methodName}' on endpoint: {endpoint}", "ConnectionManager", _guid, "ExecuteHubMethodAsync", methodName, Endpoint);
			return result;
		}
		catch (InvalidOperationException ex) when (ex.Message.Contains("not active"))
		{
			_connectionState.Value = connection.State;
			_logger.LogWarning("{class}[{guid}] {method} Connection became inactive while invoking '{methodName}' on endpoint: {endpoint}. Error: {message}", "ConnectionManager", _guid, "ExecuteHubMethodAsync", methodName, Endpoint, ex.Message);
			return default(TResult);
		}
		catch (OperationCanceledException)
		{
			_logger.LogWarning("{class}[{guid}] {method} Invocation of '{methodName}' was canceled on endpoint: {endpoint}", "ConnectionManager", _guid, "ExecuteHubMethodAsync", methodName, Endpoint);
			return default(TResult);
		}
		catch (Exception ex3)
		{
			_logger.LogError(ex3, "{class}[{guid}] {method} Failed to invoke method '{methodName}' on endpoint: {endpoint}. Error: {message}", "ConnectionManager", _guid, "ExecuteHubMethodAsync", methodName, Endpoint, ex3.Message);
			throw;
		}
	}

	private void CancelInternalToken(bool dispose = false)
	{
		if (internalCts != null)
		{
			if (!internalCts.IsCancellationRequested)
			{
				internalCts.Cancel();
			}
			if (dispose)
			{
				internalCts.Dispose();
				internalCts = null;
			}
		}
	}

	public async Task Disconnect(CancellationToken cancellationToken = default(CancellationToken))
	{
		if (_isDisposed.Value)
		{
			_logger.LogWarning("{class}[{guid}] {method} called but already disposed, ignored.", "ConnectionManager", _guid, "Disconnect");
			return;
		}
		_logger.LogDebug("{class}[{guid}] {method} called.", "ConnectionManager", _guid, "Disconnect");
		if (cancellationToken.IsCancellationRequested)
		{
			_logger.LogWarning("{class}[{guid}] {method} canceled before it gets started.", "ConnectionManager", _guid, "Disconnect");
			return;
		}
		CancelInternalToken();
		_continueToReconnect.Value = false;
		try
		{
			_logger.LogDebug("{class}[{guid}] {method} acquiring gate.", "ConnectionManager", _guid, "Disconnect");
			await _gate.WaitAsync(cancellationToken);
			_logger.LogDebug("{class}[{guid}] {method} acquired gate.", "ConnectionManager", _guid, "Disconnect");
		}
		catch (OperationCanceledException)
		{
			_logger.LogWarning("{class}[{guid}] {method} canceled while waiting for gate for endpoint: {endpoint}", "ConnectionManager", _guid, "Disconnect", Endpoint);
			return;
		}
		try
		{
			await DisconnectInternal(cancellationToken);
		}
		finally
		{
			_logger.LogDebug("{class}[{guid}] {method} releasing gate.", "ConnectionManager", _guid, "Disconnect");
			_gate.Release();
		}
	}

	public void DisconnectImmediate()
	{
		if (_isDisposed.Value)
		{
			_logger.LogWarning("{class}[{guid}] {method} called but already disposed, ignored.", "ConnectionManager", _guid, "DisconnectImmediate");
			return;
		}
		CancelInternalToken();
		_continueToReconnect.Value = false;
		bool flag = _gate.Wait(TimeSpan.FromSeconds(1.0));
		try
		{
			_logger.LogDebug("{class}[{guid}] {method} Gate acquired: {acquired}", "ConnectionManager", _guid, "DisconnectImmediate", flag);
			DisconnectImmediateCore();
		}
		finally
		{
			if (flag)
			{
				_logger.LogDebug("{class}[{guid}] {method} Releasing gate.", "ConnectionManager", _guid, "DisconnectImmediate");
				_gate.Release();
			}
			else
			{
				_logger.LogWarning("{class}[{guid}] {method} Could not acquire gate within timeout. Proceeding without gate protection.", "ConnectionManager", _guid, "DisconnectImmediate");
			}
		}
	}

	private void DisconnectImmediateCore()
	{
		if (_isDisposed.Value)
		{
			_logger.LogWarning("{class}[{guid}] {method} called but already disposed, ignored.", "ConnectionManager", _guid, "DisconnectImmediateCore");
			return;
		}
		_logger.LogDebug("{class}[{guid}] {method}.", "ConnectionManager", _guid, "DisconnectImmediateCore");
		if (_ongoingConnectionGate.Wait(TimeSpan.Zero))
		{
			_ongoingConnectionTask = null;
			_ongoingConnectionGate.Release();
		}
		else
		{
			_logger.LogWarning("{class}[{guid}] {method} Could not acquire ongoingConnectionGate (held by another thread). Connect's finally block will clear _ongoingConnectionTask after cancellation propagates.", "ConnectionManager", _guid, "DisconnectImmediateCore");
		}
		HubConnection val = ClearConnectionState();
		if (val == null)
		{
			return;
		}
		_logger.LogDebug("{class}[{guid}] {method} Performing immediate disconnect without waiting for cleanup.", "ConnectionManager", _guid, "DisconnectImmediateCore");
		HubConnection conn = val;
		_pendingImmediateTeardown = Task.Run(async delegate
		{
			try
			{
				await conn.DisposeAsync().ConfigureAwait(continueOnCapturedContext: false);
			}
			catch
			{
			}
		});
	}

	public bool WaitForImmediateTeardown(TimeSpan timeout)
	{
		Task pendingImmediateTeardown = _pendingImmediateTeardown;
		if (pendingImmediateTeardown == null)
		{
			return true;
		}
		try
		{
			return pendingImmediateTeardown.Wait(timeout);
		}
		catch
		{
			return false;
		}
	}

	private async Task DisconnectInternal(CancellationToken cancellationToken)
	{
		if (_isDisposed.Value)
		{
			_logger.LogWarning("{class}[{guid}] {method} called but already disposed, ignored.", "ConnectionManager", _guid, "DisconnectInternal");
			return;
		}
		_logger.LogDebug("{class}[{guid}] {method}.", "ConnectionManager", _guid, "DisconnectInternal");
		await _ongoingConnectionGate.WaitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		_ongoingConnectionTask = null;
		_ongoingConnectionGate.Release();
		HubConnection val = ClearConnectionState();
		if (val != null)
		{
			await DisconnectGracefulAsync(val, cancellationToken);
		}
	}

	private HubConnection? ClearConnectionState()
	{
		hubConnectionLogger?.Dispose();
		hubConnectionObservable?.Dispose();
		hubConnectionLogger = null;
		hubConnectionObservable = null;
		_connectionState.Value = (HubConnectionState)0;
		HubConnection currentValue = ((ReadOnlyReactiveProperty<HubConnection>)(object)_hubConnection).CurrentValue;
		_hubConnection.Value = null;
		return currentValue;
	}

	private async Task DisconnectGracefulAsync(HubConnection hubConnection, CancellationToken cancellationToken)
	{
		_ = 1;
		try
		{
			await hubConnection.StopAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			await hubConnection.DisposeAsync().ConfigureAwait(continueOnCapturedContext: false);
			_logger.LogDebug("{class}[{guid}] {method} HubConnection stopped and disposed successfully.", "ConnectionManager", _guid, "DisconnectGracefulAsync");
		}
		catch (OperationCanceledException ex)
		{
			_logger.LogWarning("{class}[{guid}] {method} HubConnection stop was canceled: {message}", "ConnectionManager", _guid, "DisconnectGracefulAsync", ex.Message);
		}
		catch (InvalidOperationException ex2)
		{
			_logger.LogError("{class}[{guid}] {method} Invalid operation while stopping HubConnection: {message}\n{stackTrace}", "ConnectionManager", _guid, "DisconnectGracefulAsync", ex2.Message, ex2.StackTrace);
		}
		catch (Exception ex3)
		{
			_logger.LogCritical("{class}[{guid}] {method} Unexpected error while stopping HubConnection: {message}\n{stackTrace}", "ConnectionManager", _guid, "DisconnectGracefulAsync", ex3.Message, ex3.StackTrace);
			throw;
		}
	}
}
