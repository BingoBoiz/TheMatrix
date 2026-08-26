using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using R3;

namespace Feeder.McpPlugin
{
public class HubConnectionLogger : HubConnectionObservable, IDisposable
{
	private readonly string? _guid;

	private readonly ILogger _logger;

	private readonly CompositeDisposable _disposables;

	public HubConnectionLogger(ILogger logger, HubConnection hubConnection, string? guid = null)
		: base(hubConnection)
	{
		_disposables = new CompositeDisposable();
		_logger = logger ?? throw new ArgumentNullException("logger");
		_guid = guid;
		_logger.LogTrace("{0} HubConnectionLogger.Ctor.", _guid);
		Disposable.AddTo<IDisposable>(ObservableSubscribeExtensions.Subscribe<Exception>(ObservableExtensions.Where<Exception>(base.Closed, (Func<Exception, bool>)((Exception x) => _logger.IsEnabled(LogLevel.Debug))), (Action<Exception>)delegate(Exception ex)
		{
			_logger.LogTrace("{0} HubConnectionLogger HubConnection OnClosed. Exception: {1}", _guid, ex?.Message);
			if (ex != null)
			{
				_logger.LogError(ex, "{0} HubConnectionLogger Error in Closed event subscription: {1}", _guid, ex.Message);
			}
		}), (ICollection<IDisposable>)_disposables);
		Disposable.AddTo<IDisposable>(ObservableSubscribeExtensions.Subscribe<Exception>(ObservableExtensions.Where<Exception>(base.Reconnecting, (Func<Exception, bool>)((Exception x) => _logger.IsEnabled(LogLevel.Debug))), (Action<Exception>)delegate(Exception ex)
		{
			_logger.LogTrace("{0} HubConnectionLogger HubConnection OnReconnecting.", _guid);
			if (ex != null)
			{
				_logger.LogError(ex, "{0} HubConnectionLogger Error during reconnecting: {1}", _guid, ex.Message);
			}
		}), (ICollection<IDisposable>)_disposables);
		Disposable.AddTo<IDisposable>(ObservableSubscribeExtensions.Subscribe<string>(ObservableExtensions.Where<string>(base.Reconnected, (Func<string, bool>)((string x) => _logger.IsEnabled(LogLevel.Debug))), (Action<string>)delegate(string connectionId)
		{
			_logger.LogTrace("{0} HubConnectionLogger HubConnection OnReconnected with id {1}.", _guid, connectionId);
		}), (ICollection<IDisposable>)_disposables);
	}

	public override void Dispose()
	{
		_logger.LogTrace("{0} HubConnectionLogger.Dispose.", _guid);
		base.Dispose();
		_disposables.Dispose();
	}
}
}
