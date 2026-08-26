using System.Threading.Tasks;
using Feeder.McpPlugin.Common.Model;

namespace Feeder.McpPlugin.Common.Hub.Client;

public interface IClientMcpManager : IClientDisconnectable
{
	IClientToolHub? ToolHub { get; }

	IClientPromptHub? PromptHub { get; }

	IClientResourceHub? ResourceHub { get; }

	IClientSystemToolHub? SystemToolHub { get; }

	Task OnMcpClientConnected(McpClientData connectedClient, McpClientData[] allActiveClients);

	Task OnMcpClientDisconnected(McpClientData disconnectedClient, McpClientData[] remainingClients);

	Task OnInitialClientData(McpClientData[] allActiveClients);
}
