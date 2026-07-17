#nullable enable
using System;
using System.Linq;
using System.Threading;
using Feeder.McpPlugin.Common.Utils;
using Feeder.ReflectorNet.Utils;
using Feeder.MCP.Editor.UI.Controls;
using Feeder.MCP.Editor.Utils;
using Microsoft.Extensions.Logging;
using R3;
using UnityEngine.UIElements;
using TransportMethod = Feeder.McpPlugin.Common.Consts.MCP.Server.TransportMethod;

namespace Feeder.MCP.Editor.UI
{
    public partial class MainWindowEditor
    {
        internal static bool IsMcpServerControlEnabled(TransportMethod transport) =>
            transport != TransportMethod.stdio;

        private void SetupAiAgentSection(VisualElement root)
        {
            UnityMcpPluginEditor.PluginProperty
                .WhereNotNull()
                .Subscribe(plugin =>
            {
                plugin.McpManager.OnClientConnected
                    .Subscribe(data =>
                    {
                        Logger.LogInformation("On AI agent connected: {clientName} ({clientVersion})",
                            data.ClientName, data.ClientVersion);

                        if (Logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Trace))
                            Logger.LogTrace("AI Agent Data: {data}", data.ToPrettyJson());
                    })
                    .AddTo(_disposables);

                plugin.McpManager.OnClientDisconnected
                    .Subscribe(mcpClientData =>
                    {
                        Logger.LogInformation("On AI agent disconnected: {clientName} ({clientVersion})",
                            mcpClientData.ClientName, mcpClientData.ClientVersion);
                    })
                    .AddTo(_disposables);

                plugin.McpManager.OnClientsChanged
                    .ObserveOnCurrentSynchronizationContext()
                    .Subscribe(mcpClients =>
                    {
                        Logger.LogDebug("On AI agents changed: {count} clients", mcpClients.Count);

                        var connectedAgents = mcpClients.Where(c => c.IsConnected).ToList();
                        if (connectedAgents.Count == 0)
                        {
                            Logger.LogDebug("No connected AI agents found in clients list.");
                            SetAiAgentStatus(false);
                            return;
                        }

                        SetAiAgentStatus(true, connectedAgents.Select(a => $"AI agent: {a.ClientName} ({a.ClientVersion})"));
                    })
                    .AddTo(_disposables);

                FetchAiAgentData();
            }).AddTo(_disposables);

            UnityMcpPluginEditor.IsConnected
                .Where(isConnected => isConnected)
                .ObserveOnCurrentSynchronizationContext()
                .Subscribe(_ => FetchAiAgentData())
                .AddTo(_disposables);

            var containerMcpServer = root.Q<VisualElement>("mcpServerStatusControl") ?? throw new InvalidOperationException("mcpServerStatusControl element not found.");
            var btnStartStopMcpServer = root.Q<Button>("btnStartStopServer") ?? throw new InvalidOperationException("MCP Server start/stop button not found.");

            var segmentTransport = root.Q<VisualElement>("segmentTransport") ?? throw new InvalidOperationException("segmentTransport element not found.");
            var transportControl = new SegmentedControl("stdio", "http");
            transportControl.SetTooltips(Tooltip_ToggleStdio, Tooltip_ToggleHttp);
            segmentTransport.Add(transportControl);

            var labelTransport = root.Q<Label>("labelTransport");
            if (labelTransport != null) labelTransport.tooltip = Tooltip_LabelTransport;

            // Initialize: index 0 = stdio, index 1 = http
            transportControl.SetValueWithoutNotify(UnityMcpPluginEditor.TransportMethod == TransportMethod.stdio ? 0 : 1);
            currentAiAgentConfigurator?.SetTransportMethod(UnityMcpPluginEditor.TransportMethod);

            void UpdateMcpServerState()
            {
                containerMcpServer.SetEnabled(IsMcpServerControlEnabled(UnityMcpPluginEditor.TransportMethod));
                btnStartStopMcpServer.tooltip = IsMcpServerControlEnabled(UnityMcpPluginEditor.TransportMethod)
                    ? "Start or stop the local MCP server."
                    : "Local MCP server is disabled in STDIO mode. AI agent will launch its own MCP server instance.";
            }
            UpdateMcpServerState();

            transportControl.RegisterCallback<ChangeEvent<int>>(evt =>
            {
                if (evt.newValue == 0)
                {
                    UnityMcpPluginEditor.TransportMethod = TransportMethod.stdio;
                    UnityMcpPluginEditor.Instance.Save();
                    currentAiAgentConfigurator?.SetTransportMethod(TransportMethod.stdio);

                    // Stop MCP server if running to switch to stdio mode
                    if (McpServerManager.IsRunning)
                    {
                        UnityMcpPluginEditor.KeepServerRunning = false;
                        UnityMcpPluginEditor.Instance.Save();
                        McpServerManager.StopServer();
                    }
                }
                else
                {
                    UnityMcpPluginEditor.TransportMethod = TransportMethod.streamableHttp;
                    UnityMcpPluginEditor.Instance.Save();
                    currentAiAgentConfigurator?.SetTransportMethod(TransportMethod.streamableHttp);
                }
                UpdateMcpServerState();

                // Refresh AI agent config UI so the MCP status reflects the new transport
                InvalidateAndReloadAgentUI();
            });
        }

        private void FetchAiAgentData(int retryCount = 3, int retryDelayMs = 3000)
        {
            // The Feeder Local Bridge pushes MCP client changes into McpManager over FBP
            // (see FeederBridgeAdapter.PublishMcpClients), so the authoritative client set is the
            // local ActiveClients list — no RPC round-trip needed.
            var manager = UnityMcpPluginEditor.Instance.McpPluginInstance?.McpManager;
            if (manager == null)
            {
                Logger.LogDebug("Cannot fetch AI agent data: McpManager is null");
                return;
            }

            var connectedAgents = manager.ActiveClients.Where(c => c.IsConnected).ToList();
            var isConnected = connectedAgents.Count > 0;
            SetAiAgentStatus(isConnected, isConnected
                ? connectedAgents.Select(a => $"AI agent: {a.ClientName} ({a.ClientVersion})")
                : null);

            // If AI agent is not connected but Unity is, retry after delay.
            // The AI agent may need time to re-establish its session after Unity reconnects.
            if (!isConnected && retryCount > 0 && UnityMcpPluginEditor.IsConnected.CurrentValue)
            {
                Logger.LogDebug("AI agent not connected yet, scheduling retry ({retriesLeft} left)", retryCount);
                Observable.Timer(TimeSpan.FromMilliseconds(retryDelayMs))
                    .ObserveOnCurrentSynchronizationContext()
                    .Subscribe(_ => FetchAiAgentData(retryCount - 1, retryDelayMs))
                    .AddTo(_disposables);
            }
        }

        private void SetupToolsSection(VisualElement root)
        {
            var btn = root.Q<Button>("btnOpenTools");

            btn.RegisterCallback<ClickEvent>(evt => McpToolsWindow.ShowWindow());

            SubscribeToFeatureStats(btn, "Tools", Tooltip_ToolsCountLabel,
                computeStats: () =>
                {
                    var manager = UnityMcpPluginEditor.PluginProperty.CurrentValue?.McpManager.ToolManager;
                    if (manager == null) return (0, 0, 0);
                    var all = manager.GetAllTools();
                    var totalCount = all.Count();
                    var enabledCount = all.Count(t => manager.IsToolEnabled(t.Name));
                    var totalTokens = all.Where(t => manager.IsToolEnabled(t.Name)).Sum(t => t.TokenCount);
                    return (totalCount, enabledCount, totalTokens);
                },
                getOnUpdated: plugin => plugin.McpManager.ToolManager?.OnToolsUpdated);
        }

        private void SetupPromptsSection(VisualElement root)
        {
            var btn = root.Q<Button>("btnOpenPrompts");

            btn.RegisterCallback<ClickEvent>(evt => McpPromptsWindow.ShowWindow());

            SubscribeToFeatureStats(btn, "Prompts", Tooltip_PromptsCountLabel,
                computeStats: () =>
                {
                    var manager = UnityMcpPluginEditor.PluginProperty.CurrentValue?.McpManager.PromptManager;
                    if (manager == null) return (0, 0, 0);
                    var all = manager.GetAllPrompts();
                    var totalCount = all.Count();
                    var enabledCount = all.Count(p => manager.IsPromptEnabled(p.Name));
                    return (totalCount, enabledCount, 0);
                },
                getOnUpdated: plugin => plugin.McpManager.PromptManager?.OnPromptsUpdated);
        }

        private void SetupResourcesSection(VisualElement root)
        {
            var btn = root.Q<Button>("btnOpenResources");

            btn.RegisterCallback<ClickEvent>(evt => McpResourcesWindow.ShowWindow());

            SubscribeToFeatureStats(btn, "Resources", Tooltip_ResourcesCountLabel,
                computeStats: () =>
                {
                    var manager = UnityMcpPluginEditor.PluginProperty.CurrentValue?.McpManager.ResourceManager;
                    if (manager == null) return (0, 0, 0);
                    var all = manager.GetAllResources();
                    var totalCount = all.Count();
                    var enabledCount = all.Count(r => manager.IsResourceEnabled(r.Name));
                    return (totalCount, enabledCount, 0);
                },
                getOnUpdated: plugin => plugin.McpManager.ResourceManager?.OnResourcesUpdated);
        }

        private static void SetupDebugButtons(VisualElement root)
        {
            var btnCheckSerialization = root.Q<Button>("btnCheckSerialization");
            if (btnCheckSerialization != null)
            {
                btnCheckSerialization.tooltip = "Open Serialization Check window";
                btnCheckSerialization.RegisterCallback<ClickEvent>(evt => SerializationCheckWindow.ShowWindow());
            }
        }
    }
}
