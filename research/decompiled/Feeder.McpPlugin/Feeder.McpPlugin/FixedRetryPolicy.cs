using System;
using Microsoft.AspNetCore.SignalR.Client;

namespace Feeder.McpPlugin;

public class FixedRetryPolicy : IRetryPolicy
{
	private readonly TimeSpan _delay;

	private readonly int? _maxRetries;

	public FixedRetryPolicy(TimeSpan delay, int? maxRetries = null)
	{
		if (delay < TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException("delay", delay, "delay must be non-negative.");
		}
		if (maxRetries.HasValue && maxRetries.Value < 0)
		{
			throw new ArgumentOutOfRangeException("maxRetries", maxRetries, "maxRetries must be null or >= 0.");
		}
		_delay = delay;
		_maxRetries = maxRetries;
	}

	public TimeSpan? NextRetryDelay(RetryContext retryContext)
	{
		if (_maxRetries.HasValue && retryContext.PreviousRetryCount >= _maxRetries.Value)
		{
			return null;
		}
		return _delay;
	}
}
