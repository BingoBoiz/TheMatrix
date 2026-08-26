#nullable enable
using Feeder.MCP.Editor.UI;
using Feeder.MCP.Editor.Utils;
using Feeder.MCP.Utils;
using UnityEditor;
using UnityEngine;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Feeder.MCP.Editor
{
    [InitializeOnLoad]
    public static partial class Startup
    {
        static readonly ILogger _logger = UnityLoggerFactory.LoggerFactory.CreateLogger(nameof(Startup));

        static Startup()
        {
            UnityMcpPluginEditor.Instance.BuildMcpPluginIfNeeded();
            UnityMcpPluginEditor.Instance.AddUnityLogCollectorIfNeeded(() => new BufferedFileLogStorage());

            if (Application.dataPath.Contains(" "))
                Debug.LogError("The project path contains spaces, which may cause issues during usage of Matrix AI Connector. Please consider the move the project to a folder without spaces.");

            SubscribeOnEditorEvents();

            // Initialize sub-systems
            API.Tool_Tests.Init();
            PackageUtils.Init();

            // Auto-generate skill files for the selected agent if enabled. selectedAiAgentId is
            // PlayerPrefs-backed and empty until the connector window is opened, so fall back to
            // Claude Code (same default the window uses) for the zero-setup install path.
            var savedAgentId = MainWindowEditor.selectedAiAgentId.Value;
            if (string.IsNullOrEmpty(savedAgentId))
                savedAgentId = "claude-code";
            var agent = AiAgentCatalog.GetByAgentId(savedAgentId);
            if (agent?.SupportsSkills == true && UnityMcpPluginEditor.IsAutoGenerateSkills(agent.AgentId))
            {
                UnityMcpPluginEditor.SkillsPath = agent.SkillsPath!;
                UnityMcpPluginEditor.Instance.McpPluginInstance!.GenerateSkillFiles(UnityMcpPluginEditor.ProjectRootPath);
            }

            // Write MCP config files for auto-configure-enabled agents (once per session).
            EditorApplication.delayCall += AutoConfigureAgents;
        }
    }
}
