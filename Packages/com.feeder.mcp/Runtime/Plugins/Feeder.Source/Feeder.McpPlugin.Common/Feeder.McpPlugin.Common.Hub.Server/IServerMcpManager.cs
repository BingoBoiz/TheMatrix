using System.Threading.Tasks;
using Feeder.McpPlugin.Common.Model;

namespace Feeder.McpPlugin.Common.Hub.Server
{
public interface IServerMcpManager : IServerToolHub, IServerPromptHub, IServerResourceHub
{
	Task<VersionHandshakeResponse> PerformVersionHandshake(RequestVersionHandshake request);

	Task<McpClientData[]> GetMcpClientData();

	Task<McpServerData> GetMcpServerData();
}
}
