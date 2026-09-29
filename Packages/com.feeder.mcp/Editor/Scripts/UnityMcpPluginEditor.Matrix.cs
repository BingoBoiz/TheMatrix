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

        FeederMatrixAdapter MatrixAdapter => _matrixAdapter ??= new FeederMatrixAdapter(this);

        internal void ConfigureMatrixTransport()
        {
            ConnectTransportOverride = () => MatrixAdapter.ConnectAsync();
            DisconnectTransportOverride = () => _matrixAdapter != null
                ? _matrixAdapter.DisconnectAsync()
                : DisconnectDefaultTransport();
            DisconnectImmediateTransportOverride = () =>
            {
                if (_matrixAdapter != null)
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
            NotifyCompletedTransportOverride = (request, cancellationToken) =>
                MatrixAdapter.CompleteDeferredAsync(request, cancellationToken);
        }

        internal void SetMatrixConnectionState(HubConnectionState state) => _connectionState.Value = state;

        void DisposeMatrixAdapter()
        {
            _matrixAdapter?.Dispose();
            _matrixAdapter = null;
        }
    }
}
