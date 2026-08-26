using System;
using System.Threading.Tasks;

namespace Feeder.McpPlugin.Common
{
public interface IDisposableAsync : IDisposable
{
	Task DisposeAsync();
}
}
