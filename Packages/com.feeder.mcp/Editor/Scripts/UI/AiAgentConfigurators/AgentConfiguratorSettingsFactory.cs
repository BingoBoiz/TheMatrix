#nullable enable
using AgentConfig = Feeder.McpPlugin.AgentConfig;
using UnityConnectionMode = Feeder.MCP.ConnectionMode;

namespace Feeder.MCP.Editor.UI
{
    /// <summary>
    /// Adapts Unity's editor/connection state to the engine-agnostic
    /// <see cref="AgentConfig.AgentConfiguratorSettings"/> consumed by the shared
    /// <c>Feeder.McpPlugin.AgentConfig</c> module. This is the single place that maps
    /// Unity's statics (<see cref="UnityMcpPluginEditor.Port"/>, <c>.Host</c>, <c>.Token</c>, …,
    /// and <see cref="McpServerManager.ExecutableFullPath"/>) onto the shared settings record.
    /// The shared library detects the host OS at runtime, so per-OS config-file paths work on
    /// Win/Mac/Linux without a compile-time branch here.
    /// </summary>
    internal static class AgentConfiguratorSettingsFactory
    {
        /// <summary>
        /// Builds an <see cref="AgentConfig.AgentConfiguratorSettings"/> snapshot from the current
        /// Unity editor connection state, auto-detecting the host OS.
        /// </summary>
        public static AgentConfig.AgentConfiguratorSettings Create()
        {
            return AgentConfig.AgentConfiguratorSettings.CreateForHost(
                projectRootPath: UnityMcpPluginEditor.ProjectRootPath,
                executableFullPath: McpServerManager.ExecutableFullPath,
                port: UnityMcpPluginEditor.Port,
                timeoutMs: UnityMcpPluginEditor.TimeoutMs,
                host: UnityMcpPluginEditor.ConnectionMode == UnityConnectionMode.Custom
                    ? McpServerManager.GetMcpEndpointUrl(UnityMcpPluginEditor.Host)
                    : UnityMcpPluginEditor.Host,
                token: UnityMcpPluginEditor.Token,
                connectionMode: MapConnectionMode(UnityMcpPluginEditor.ConnectionMode),
                authOption: UnityMcpPluginEditor.AuthOption,
                // Pass Unity's authoritative server identity explicitly so the shared module tracks
                // McpServerManager's pin instead of silently coinciding with the shared library's own
                // defaults — which would drift the moment ServerVersion is bumped here.
                serverExecutableName: McpServerManager.ExecutableName,
                serverVersion: McpServerManager.ServerVersion);
        }

        /// <summary>
        /// Maps Unity's <see cref="UnityConnectionMode"/> (<c>Custom</c> = local server / <c>Cloud</c>)
        /// onto the shared <see cref="AgentConfig.ConnectionMode"/> (<c>Local</c> / <c>Cloud</c>).
        /// Only <c>Cloud</c> changes auth behaviour (cloud always requires it); everything else is local.
        /// </summary>
        public static AgentConfig.ConnectionMode MapConnectionMode(UnityConnectionMode mode)
            => mode == UnityConnectionMode.Cloud
                ? AgentConfig.ConnectionMode.Cloud
                : AgentConfig.ConnectionMode.Local;
    }
}
