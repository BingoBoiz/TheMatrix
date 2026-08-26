using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;

namespace Feeder.McpPlugin
{
public interface IHubConnectionProvider
{
	Task<HubConnection> CreateConnectionAsync(string endpoint);
}
}
