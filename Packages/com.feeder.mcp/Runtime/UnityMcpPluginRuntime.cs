#nullable enable
using System.Diagnostics;
using Feeder.McpPlugin;
using Microsoft.Extensions.Logging;
using R3;

namespace Feeder.MCP
{
    /// <summary>
    /// Runtime singleton for game builds. Provides the developer-facing
    /// <see cref="Initialize"/> API and maintains a separate MCP connection
    /// alongside the Editor's connection.
    /// </summary>
    public partial class UnityMcpPluginRuntime : UnityMcpPlugin
    {
        protected UnityMcpPluginRuntime(UnityConnectionConfig? config = null) : base()
        {
            unityConnectionConfig = config ?? new UnityConnectionConfig();
            IncrementSingletonCount();
        }

        internal IMcpPlugin BuildFromBuilder(IMcpPluginBuilder builder)
        {
            _logger.LogTrace("{method} called.", nameof(BuildFromBuilder));

            var stopwatch = Stopwatch.StartNew();
            var built = _plugin.Set(builder.Build(CreateDefaultReflector()));
            stopwatch.Stop();
            _logger.LogDebug("Runtime MCP Plugin built in {elapsedMilliseconds} ms.",
                stopwatch.ElapsedMilliseconds);

            ApplyConfigToMcpPlugin(built);

            built.ConnectionState
                .Subscribe(state => _connectionState.Value = state)
                .AddTo(_disposables);

            _logger.LogTrace("{method} completed.", nameof(BuildFromBuilder));
            return built;
        }

        public override void Dispose()
        {
            DecrementSingletonCount();
            base.Dispose();
            _plugin.Dispose();
        }
    }
}
