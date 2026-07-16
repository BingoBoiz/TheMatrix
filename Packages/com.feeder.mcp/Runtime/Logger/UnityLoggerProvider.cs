#nullable enable
using Microsoft.Extensions.Logging;

namespace Feeder.MCP.Utils
{
    public class UnityLoggerProvider : ILoggerProvider
    {
        public void Dispose() { /* No resources to dispose of */ }
        ILogger ILoggerProvider.CreateLogger(string categoryName) => new UnityLogger(categoryName);
    }
}
