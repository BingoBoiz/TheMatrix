using System.Threading.Tasks;
using Feeder.McpPlugin.Common.Model;

namespace Feeder.McpPlugin.Common.Hub.Client
{
public interface IClientMcpRpc : IClientDisconnectable
{
	Task OnInitialClientData(McpClientData[] allActiveClients);

	Task OnMcpClientConnected(McpClientData connectedClient, McpClientData[] allActiveClients);

	Task OnMcpClientDisconnected(McpClientData disconnectedClient, McpClientData[] remainingClients);
}
}
