using System;
using Microsoft.Extensions.Logging;

namespace Feeder.ReflectorNet.Utils;

public class ConsoleLoggerProvider : ILoggerProvider, IDisposable
{
	public void Dispose()
	{
	}

	ILogger ILoggerProvider.CreateLogger(string categoryName)
	{
		return new ConsoleLogger(categoryName);
	}
}
