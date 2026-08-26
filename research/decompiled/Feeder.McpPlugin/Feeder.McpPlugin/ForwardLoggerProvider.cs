using System;
using Microsoft.Extensions.Logging;

namespace Feeder.McpPlugin;

public class ForwardLoggerProvider : ILoggerProvider, IDisposable
{
	private class ForwardingLogger : ILogger
	{
		private readonly ILogger _logger;

		private readonly string _categoryName;

		private readonly string? _additionalErrorMessage;

		public ForwardingLogger(ILogger logger, string categoryName, string? additionalErrorMessage)
		{
			_logger = logger;
			_categoryName = categoryName;
			_additionalErrorMessage = additionalErrorMessage;
		}

		public IDisposable? BeginScope<TState>(TState state) where TState : notnull
		{
			return _logger.BeginScope(state);
		}

		public bool IsEnabled(LogLevel logLevel)
		{
			return _logger.IsEnabled(logLevel);
		}

		void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			if (IsEnabled(logLevel))
			{
				string state2 = formatter(state, exception) ?? "";
				_logger.Log(logLevel, eventId, state2, exception, (object s, Exception? e) => s?.ToString() ?? string.Empty);
				if (logLevel >= LogLevel.Error)
				{
					_logger.Log(logLevel, _additionalErrorMessage);
				}
			}
		}
	}

	private readonly ILogger _logger;

	private readonly string? _additionalErrorMessage;

	public ForwardLoggerProvider(ILogger logger, string? additionalErrorMessage = null)
	{
		_logger = logger ?? throw new ArgumentNullException("logger");
		_additionalErrorMessage = additionalErrorMessage;
	}

	public ILogger CreateLogger(string categoryName)
	{
		return new ForwardingLogger(_logger, categoryName, _additionalErrorMessage);
	}

	public void Dispose()
	{
	}
}
