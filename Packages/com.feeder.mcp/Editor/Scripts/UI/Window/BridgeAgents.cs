#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Feeder.MCP.Utils;
using Microsoft.Extensions.Logging;
using AgentConfig = Feeder.McpPlugin.AgentConfig;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using static Feeder.McpPlugin.Common.Consts.MCP.Server;

namespace Feeder.MCP.Editor.UI
{
    internal enum AgentWiring
    {
        Unwired,
        Wired,
        Stale,
    }

    internal static class BridgeAgents
    {
        static readonly ILogger _logger = UnityLoggerFactory.LoggerFactory.CreateLogger(nameof(BridgeAgents));

        static readonly string[] _chipIds = { "claude-code", "codex", "cursor", "deepseek", "gemini" };
        const string SkippedId = "other-custom";

        public static IReadOnlyList<AgentConfig.AiAgentConfigurator> Chips()
        {
            var list = new List<AgentConfig.AiAgentConfigurator>(_chipIds.Length);
            foreach (var id in _chipIds)
            {
                var agent = AiAgentCatalog.GetByAgentId(id);
                if (agent == null)
                    _logger.LogError("Bridge chip agent '{id}' is not in the agent catalog.", id);
                else
                    list.Add(agent);
            }
            return list;
        }

        public static IReadOnlyList<AgentConfig.AiAgentConfigurator> Overflow() => AiAgentCatalog.All
            .Where(agent => agent.AgentId != SkippedId && Array.IndexOf(_chipIds, agent.AgentId) < 0)
            .ToList();

        public static string ShortName(AgentConfig.AiAgentConfigurator agent)
        {
            var name = agent.AgentName;
            var space = name.IndexOf(' ');
            return (space > 0 ? name.Substring(0, space) : name).ToLowerInvariant();
        }

        public static AgentWiring GetState(AgentConfig.AiAgentConfigurator agent)
        {
            var status = agent.GetStatus(AgentConfiguratorSettingsFactory.Create(), UnityMcpPluginEditor.TransportMethod);
            return status switch
            {
                AgentConfig.ConfiguratorStatus.Configured => AgentWiring.Wired,
                AgentConfig.ConfiguratorStatus.ReconfigureNeeded => AgentWiring.Stale,
                _ => AgentWiring.Unwired,
            };
        }

        public static string Describe(AgentConfig.AiAgentConfigurator agent)
        {
            var transport = UnityMcpPluginEditor.TransportMethod;
            var path = Config(agent, AgentConfiguratorSettingsFactory.Create(), transport).ConfigPath;
            return $"{agent.AgentName}\n{path}";
        }

        public static void Toggle(AgentConfig.AiAgentConfigurator agent)
        {
            if (GetState(agent) == AgentWiring.Wired)
                Unwire(agent);
            else
                Wire(agent);
        }

        static AgentConfig.AiAgentConfig Config(AgentConfig.AiAgentConfigurator agent, AgentConfig.AgentConfiguratorSettings settings, TransportMethod transport)
            => transport == TransportMethod.stdio
                ? agent.GetStdioConfig(settings)
                : agent.GetHttpConfig(settings);

        static void Wire(AgentConfig.AiAgentConfigurator agent)
        {
            var settings = AgentConfiguratorSettingsFactory.Create();
            Config(agent, settings, UnityMcpPluginEditor.TransportMethod).Configure();
            UnityMcpPluginEditor.SetAutoConfigureAgent(agent.AgentId, true);
            if (agent.SupportsSkills)
            {
                UnityMcpPluginEditor.SetAutoGenerateSkills(agent.AgentId, true);
                GenerateSkills(agent);
            }
            UnityMcpPluginEditor.Instance.Save();
        }

        public static void GenerateSkills(AgentConfig.AiAgentConfigurator agent)
        {
            var previous = UnityMcpPluginEditor.SkillsPath;
            try
            {
                UnityMcpPluginEditor.SkillsPath = agent.SkillsPath!;
                UnityMcpPluginEditor.Instance.McpPluginInstance?.GenerateSkillFiles(UnityMcpPluginEditor.ProjectRootPath);
            }
            finally
            {
                UnityMcpPluginEditor.SkillsPath = previous;
            }
        }

        public static int RegenerateSkills()
        {
            var count = 0;
            foreach (var agentId in UnityMcpPluginEditor.AutoConfigureAgentIds)
            {
                var agent = AiAgentCatalog.GetByAgentId(agentId);
                if (agent?.SupportsSkills != true || !UnityMcpPluginEditor.IsAutoGenerateSkills(agent.AgentId))
                    continue;

                GenerateSkills(agent);
                count++;
            }
            return count;
        }

        static void Unwire(AgentConfig.AiAgentConfigurator agent)
        {
            var settings = AgentConfiguratorSettingsFactory.Create();
            Config(agent, settings, UnityMcpPluginEditor.TransportMethod).Unconfigure();
            UnityMcpPluginEditor.SetAutoConfigureAgent(agent.AgentId, false);
            if (agent.SupportsSkills)
                UnityMcpPluginEditor.SetAutoGenerateSkills(agent.AgentId, false);
            UnityMcpPluginEditor.Instance.Save();
        }
    }
}
