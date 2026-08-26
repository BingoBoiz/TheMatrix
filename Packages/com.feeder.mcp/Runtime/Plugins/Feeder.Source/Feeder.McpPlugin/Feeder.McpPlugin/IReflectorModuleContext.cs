using System.Reflection;
using Feeder.ReflectorNet;
using Microsoft.Extensions.Logging;

namespace Feeder.McpPlugin
{
public interface IReflectorModuleContext
{
	Reflector Reflector { get; }

	IScanIgnoreBuilder Scan { get; }

	Assembly OwningAssembly { get; }

	ILogger Logger { get; }
}
}
