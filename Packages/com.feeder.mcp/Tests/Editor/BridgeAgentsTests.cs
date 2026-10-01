#nullable enable

using System;
using System.IO;
using Feeder.MCP.Editor.UI;
using NUnit.Framework;
using AgentConfig = Feeder.McpPlugin.AgentConfig;
using TransportMethod = Feeder.McpPlugin.Common.Consts.MCP.Server.TransportMethod;

namespace Feeder.MCP.Editor.Tests
{
    public sealed class BridgeAgentsTests
    {
        const string ClaudeId = "claude-code";

        string _root = string.Empty;
        bool _deepSeekWasOn;
        AgentConfig.AgentConfiguratorSettings _settings = null!;
        AgentConfig.AiAgentConfigurator _claude = null!;
        AgentConfig.AiAgentConfigurator _deepSeek = null!;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "matrix-bridge-agents-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _settings = AgentConfig.AgentConfiguratorSettings.CreateForHost(_root, "C:/matrix/server.exe", 25999, 10000, "http://localhost:25999/mcp");
            _claude = Agent(ClaudeId);
            _deepSeek = Agent(DeepSeekAiAgentConfigurator.Id);
            _deepSeekWasOn = UnityMcpPluginEditor.IsAutoConfigureAgent(DeepSeekAiAgentConfigurator.Id);
            UnityMcpPluginEditor.SetAutoConfigureAgent(DeepSeekAiAgentConfigurator.Id, false);
        }

        [TearDown]
        public void TearDown()
        {
            UnityMcpPluginEditor.SetAutoConfigureAgent(DeepSeekAiAgentConfigurator.Id, _deepSeekWasOn);
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }

        [TestCase(TransportMethod.stdio)]
        [TestCase(TransportMethod.streamableHttp)]
        public void WiringClaude_LeavesDeepSeekOff(TransportMethod transport)
        {
            Config(_claude, transport).Configure();

            Assert.AreEqual(AgentConfig.ConfiguratorStatus.Configured, Status(_claude, transport));
            Assert.AreEqual(AgentConfig.ConfiguratorStatus.NotConfigured, Status(_deepSeek, transport));
        }

        [TestCase(TransportMethod.stdio)]
        [TestCase(TransportMethod.streamableHttp)]
        public void WiringDeepSeek_LeavesClaudeOff_AndWritesNoMcpJson(TransportMethod transport)
        {
            UnityMcpPluginEditor.SetAutoConfigureAgent(DeepSeekAiAgentConfigurator.Id, true);

            Config(_deepSeek, transport).Configure();

            Assert.AreEqual(AgentConfig.ConfiguratorStatus.Configured, Status(_deepSeek, transport));
            Assert.AreEqual(AgentConfig.ConfiguratorStatus.NotConfigured, Status(_claude, transport));
            Assert.IsFalse(File.Exists(Path.Combine(_root, ".mcp.json")));
        }

        [TestCase(TransportMethod.stdio)]
        [TestCase(TransportMethod.streamableHttp)]
        public void UnwiringDeepSeek_KeepsClaudesEntry(TransportMethod transport)
        {
            Config(_claude, transport).Configure();
            UnityMcpPluginEditor.SetAutoConfigureAgent(DeepSeekAiAgentConfigurator.Id, true);

            Config(_deepSeek, transport).Unconfigure();
            UnityMcpPluginEditor.SetAutoConfigureAgent(DeepSeekAiAgentConfigurator.Id, false);

            Assert.AreEqual(AgentConfig.ConfiguratorStatus.Configured, Status(_claude, transport));
            Assert.AreEqual(AgentConfig.ConfiguratorStatus.NotConfigured, Status(_deepSeek, transport));
        }

        [TestCase(TransportMethod.stdio)]
        [TestCase(TransportMethod.streamableHttp)]
        public void UnwiringClaude_KeepsDeepSeekOn(TransportMethod transport)
        {
            Config(_claude, transport).Configure();
            UnityMcpPluginEditor.SetAutoConfigureAgent(DeepSeekAiAgentConfigurator.Id, true);

            Config(_claude, transport).Unconfigure();

            Assert.AreEqual(AgentConfig.ConfiguratorStatus.NotConfigured, Status(_claude, transport));
            Assert.AreEqual(AgentConfig.ConfiguratorStatus.Configured, Status(_deepSeek, transport));
        }

        static AgentConfig.AiAgentConfigurator Agent(string agentId)
        {
            var agent = AiAgentCatalog.GetByAgentId(agentId);
            Assert.IsNotNull(agent, agentId);
            return agent!;
        }

        AgentConfig.AiAgentConfig Config(AgentConfig.AiAgentConfigurator agent, TransportMethod transport)
            => transport == TransportMethod.stdio ? agent.GetStdioConfig(_settings) : agent.GetHttpConfig(_settings);

        AgentConfig.ConfiguratorStatus Status(AgentConfig.AiAgentConfigurator agent, TransportMethod transport)
            => agent.GetStatus(_settings, transport);
    }
}
