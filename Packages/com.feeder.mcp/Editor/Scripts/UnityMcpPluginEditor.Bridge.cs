#nullable enable
using System.Threading;
using System.Threading.Tasks;
using Feeder.McpPlugin.Common.Model;
using Feeder.MCP.Editor.Bridge;
using Microsoft.AspNetCore.SignalR.Client;

namespace Feeder.MCP
{
    public partial class UnityMcpPluginEditor
    {
        FeederBridgeAdapter? _bridgeAdapter;

        bool UsesLocalBridge => unityConnectionConfig.ConnectionMode == ConnectionMode.Custom;
        FeederBridgeAdapter BridgeAdapter => _bridgeAdapter ??= new FeederBridgeAdapter(this);

        internal void ConfigureBridgeTransport()
        {
            ConnectTransportOverride = () => UsesLocalBridge
                ? BridgeAdapter.ConnectAsync()
                : ConnectDefaultTransport();
            DisconnectTransportOverride = () => UsesLocalBridge && _bridgeAdapter != null
                ? _bridgeAdapter.DisconnectAsync()
                : DisconnectDefaultTransport();
            DisconnectImmediateTransportOverride = () =>
            {
                if (UsesLocalBridge && _bridgeAdapter != null)
                {
                    _bridgeAdapter.Dispose();
                    _bridgeAdapter = null;
                    SetBridgeConnectionState(HubConnectionState.Disconnected);
                }
                else
                {
                    DisconnectImmediateDefaultTransport();
                }
            };
            NotifyCompletedTransportOverride = (request, cancellationToken) => UsesLocalBridge
                ? BridgeAdapter.CompleteDeferredAsync(request, cancellationToken)
                : NotifyToolRequestCompletedDefaultTransport(request, cancellationToken);
        }

        internal void SetBridgeConnectionState(HubConnectionState state) => _connectionState.Value = state;

        void DisposeBridgeAdapter()
        {
            _bridgeAdapter?.Dispose();
            _bridgeAdapter = null;
        }
    }
}
