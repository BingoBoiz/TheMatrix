#nullable enable
using System;
using Feeder.MCP.Editor.UI;
using Feeder.MCP.Runtime.Utils;
using Microsoft.Extensions.Logging;
using UnityEditor;
using UnityEngine;
using static Feeder.McpPlugin.Common.Consts.MCP.Server;

namespace Feeder.MCP.Editor
{
    public static partial class Startup
    {
        const string AutoConfigureSessionKey = "Feeder.MCP.AutoConfigured";

        /// <summary>
        /// Writes the MCP config file for every agent with the auto-configure flag enabled
        /// (default: Claude Code's project-root <c>.mcp.json</c>) once Matrix has been turned
        /// on for the project, so the file keeps following the directory-derived port without
        /// opening the Matrix Bridge window. Runs once per editor session
        /// (SessionState survives domain reloads); Configure() is idempotent so the
        /// unconditional write self-heals stale ports.
        /// </summary>
        static void AutoConfigureAgents()
        {
            try
            {
                if (Application.isBatchMode || EnvironmentUtils.IsCi())
                    return;

                if (SessionState.GetBool(AutoConfigureSessionKey, false))
                    return;
                SessionState.SetBool(AutoConfigureSessionKey, true);

                var settings = AgentConfiguratorSettingsFactory.Create();
                var transport = UnityMcpPluginEditor.TransportMethod;

                foreach (var agentId in UnityMcpPluginEditor.AutoConfigureAgentIds)
                {
                    var configurator = AiAgentCatalog.GetByAgentId(agentId);
                    if (configurator == null)
                        continue;

                    var config = transport == TransportMethod.stdio
                        ? configurator.GetStdioConfig(settings)
                        : configurator.GetHttpConfig(settings);
                    config.Configure();
                    _logger.LogInformation("Auto-configured MCP for agent '{agentId}' at '{path}'.", agentId, config.ConfigPath);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "MCP agent auto-configure failed; editor load is unaffected. Wire the agent manually in Tools/Feeder/Bridge.");
            }
        }
    }
}
