using System;
using System.Threading;
using R3;

namespace Feeder.McpPlugin.Common
{
public static class ExtensionsCompositeDisposable
{
	public static CancellationTokenSource ToCancellationTokenSource(this CompositeDisposable disposables)
	{
		CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
		disposables.Add((IDisposable)cancellationTokenSource);
		return cancellationTokenSource;
	}
}
}
