#nullable enable
using System.Threading;
using System.Threading.Tasks;
using Feeder.McpPlugin.Common.Model;
using Feeder.MCP.Editor.Matrix;
using Microsoft.AspNetCore.SignalR.Client;

namespace Feeder.MCP
{
    public partial class UnityMcpPluginEditor
    {
        FeederMatrixAdapter? _matrixAdapter;

        bool UsesLocalMatrix => unityConnectionConfig.ConnectionMode == ConnectionMode.Custom;
        FeederMatrixAdapter MatrixAdapter => _matrixAdapter ??= new FeederMatrixAdapter(this);

        internal void ConfigureMatrixTransport()
        {
            ConnectTransportOverride = () => UsesLocalMatrix
                ? MatrixAdapter.ConnectAsync()
                : ConnectDefaultTransport();
            DisconnectTransportOverride = () => UsesLocalMatrix && _matrixAdapter != null
                ? _matrixAdapter.DisconnectAsync()
                : DisconnectDefaultTransport();
            DisconnectImmediateTransportOverride = () =>
            {
                if (UsesLocalMatrix && _matrixAdapter != null)
                {
                    _matrixAdapter.Dispose();
                    _matrixAdapter = null;
                    SetMatrixConnectionState(HubConnectionState.Disconnected);
                }
                else
                {
                    DisconnectImmediateDefaultTransport();
                }
            };
            NotifyCompletedTransportOverride = (request, cancellationToken) => UsesLocalMatrix
                ? MatrixAdapter.CompleteDeferredAsync(request, cancellationToken)
                : NotifyToolRequestCompletedDefaultTransport(request, cancellationToken);
        }

        internal void SetMatrixConnectionState(HubConnectionState state) => _connectionState.Value = state;

        void DisposeMatrixAdapter()
        {
            _matrixAdapter?.Dispose();
            _matrixAdapter = null;
        }
    }
}
