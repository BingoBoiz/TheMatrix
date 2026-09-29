#nullable enable
using Feeder.MCP.Editor.Utils;
using Microsoft.Extensions.Logging;

namespace Feeder.MCP
{
    public partial class UnityMcpPluginEditor
    {
        /// <summary>
        /// Editor-only test accessor for the underlying <see cref="UnityConnectionConfig"/>.
        /// The field is <c>protected</c> on <see cref="UnityMcpPlugin"/>; this accessor exposes
        /// it to the Editor test assembly (via <c>InternalsVisibleTo</c>) so tests can verify
        /// state that is written through the plugin's build path.
        /// </summary>
        internal UnityConnectionConfig ConnectionConfigForTests => unityConnectionConfig;

        public UnityMcpPluginEditor BuildMcpPluginIfNeeded()
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var loggerProvider = BuildLoggerProvider();

            // Feed the host project root into the connection config BEFORE the plugin is built.
            // McpPlugin's ctor calls GenerateSkillFilesIfNeeded() internally; it needs an anchor
            // for the relative SkillsPath. Without this, the McpPlugin ctor logs an
            // InvalidOperationException every Editor open / domain reload.
            unityConnectionConfig.ProjectRootPath = ProjectRootPath;
            _logger.LogTrace("Seeded ConnectionConfig.ProjectRootPath={path}", ProjectRootPath);

            var built = _plugin.BuildOnce(() => BuildMcpPlugin(
                version: BuildVersion(),
                reflector: CreateDefaultReflector(),
                loggerProvider: loggerProvider,
                configure: builder =>
                {
                    builder.WithSkillFileGenerator(new UnitySkillFileGenerator(loggerProvider?.CreateLogger(nameof(UnitySkillFileGenerator))));
                }
            ));
            stopwatch.Stop();

            if (built == null)
                return this; // already built, nothing to wire up

            _logger.LogDebug("MCP Plugin built in {elapsedMilliseconds} ms.",
                stopwatch.ElapsedMilliseconds);

            SetCurrentPlugin(built);
            ApplyConfigToMcpPlugin(built);

            return this;
        }

        public void DisposeMcpPluginInstance()
        {
            var oldInstance = _plugin.TakeInstance();
            if (oldInstance == null)
                return;

            SetCurrentPlugin(null);

            // Dispose on a background thread to avoid blocking Unity's main thread.
            // The dispose path calls ConnectionManager.DisconnectImmediate() which
            // internally blocks on a semaphore (_gate) held by a pending Connect()
            // task whose continuation is queued on the main thread SynchronizationContext —
            // a deadlock if we block the main thread here.
            _ = System.Threading.Tasks.Task.Run(() => oldInstance.Dispose());
        }
    }
}
