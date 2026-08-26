using Microsoft.AspNetCore.SignalR.Client;

namespace Feeder.McpPlugin;

public static class HubConnectionObservableExtensions
{
	public static HubConnectionObservable ToObservable(this HubConnection hubConnection)
	{
		return new HubConnectionObservable(hubConnection);
	}
}
