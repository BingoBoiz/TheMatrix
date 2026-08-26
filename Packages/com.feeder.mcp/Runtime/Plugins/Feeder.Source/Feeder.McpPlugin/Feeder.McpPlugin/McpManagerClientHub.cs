using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Feeder.McpPlugin.Common;
using Feeder.McpPlugin.Common.Hub.Client;
using Feeder.McpPlugin.Common.Hub.Server;
using Feeder.McpPlugin.Common.Model;
using Feeder.McpPlugin.Common.Utils;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using R3;

namespace Feeder.McpPlugin
{
public class McpManagerClientHub : BaseHubConnector, IMcpManagerHub, IConnectServerHub, IDisposable, IServerMcpManager, IServerToolHub, IServerPromptHub, IServerResourceHub
{
	private readonly IClientMcpManager _mcpManager;

	private readonly IOptions<ConnectionConfig> _connectionOptions;

	private volatile int _liveNotificationEpoch;

	public McpManagerClientHub(ILogger<McpManagerClientHub> logger, Feeder.McpPlugin.Common.Version apiVersion, IHubConnectionProvider hubConnectionProvider, IClientMcpManager mcpManager, IOptions<ConnectionConfig> connectionConfig)
		: base(logger, apiVersion, "/hub/mcp-server", hubConnectionProvider, (connectionConfig?.Value?.MaxConsecutiveConnectionFailures).GetValueOrDefault())
	{
		_mcpManager = mcpManager ?? throw new ArgumentNullException("mcpManager");
		_connectionOptions = connectionConfig;
	}

	protected override void OnBeforeSubscribeToServerEvents()
	{
		_liveNotificationEpoch = 0;
	}

	protected override void SubscribeOnServerEvents(HubConnection hubConnection, CompositeDisposable disposables)
	{
		Disposable.AddTo<IDisposable>(HubConnectionExtensions.On<McpClientData[]>(hubConnection, "OnInitialClientData", (Func<McpClientData[], Task>)delegate(McpClientData[] allActiveClients)
		{
			_logger.LogDebug("{class}.{method}", "IClientMcpRpc", "OnInitialClientData");
			if (_liveNotificationEpoch != 0)
			{
				_logger.LogDebug("{class}.{method} Discarding stale initial snapshot: live notifications already received.", "McpManagerClientHub", "OnInitialClientData");
				return Task.CompletedTask;
			}
			return _mcpManager.OnInitialClientData(allActiveClients);
		}), (ICollection<IDisposable>)_serverEventsDisposables);
		Disposable.AddTo<IDisposable>(HubConnectionExtensions.On<McpClientData, McpClientData[]>(hubConnection, "OnMcpClientConnected", (Func<McpClientData, McpClientData[], Task>)delegate(McpClientData connectedClient, McpClientData[] allActiveClients)
		{
			_logger.LogDebug("{class}.{method}", "IClientMcpRpc", "OnMcpClientConnected");
			Interlocked.Increment(ref _liveNotificationEpoch);
			return _mcpManager.OnMcpClientConnected(connectedClient, allActiveClients);
		}), (ICollection<IDisposable>)_serverEventsDisposables);
		Disposable.AddTo<IDisposable>(HubConnectionExtensions.On<McpClientData, McpClientData[]>(hubConnection, "OnMcpClientDisconnected", (Func<McpClientData, McpClientData[], Task>)delegate(McpClientData disconnectedClient, McpClientData[] remainingClients)
		{
			_logger.LogDebug("{class}.{method}", "IClientMcpRpc", "OnMcpClientDisconnected");
			Interlocked.Increment(ref _liveNotificationEpoch);
			return _mcpManager.OnMcpClientDisconnected(disconnectedClient, remainingClients);
		}), (ICollection<IDisposable>)_serverEventsDisposables);
		Disposable.AddTo<IDisposable>(HubConnectionExtensions.On<string>(hubConnection, "ForceDisconnect", (Func<string, Task>)async delegate(string? reason)
		{
			_logger.LogDebug("{class}.{method}", "IClientMcpRpc", "ForceDisconnect");
			if (_connectionManager.HubConnection.CurrentValue != hubConnection)
			{
				_logger.LogWarning("{class}.{method} Received ForceDisconnect on a stale connection — ignoring to protect the active connection.", "McpManagerClientHub", "ForceDisconnect");
			}
			else
			{
				if (!string.IsNullOrEmpty(reason))
				{
					_logger.LogError("Server forcefully disconnected this plugin. Reason: {Reason}", reason);
				}
				else
				{
					_logger.LogError("Server forcefully disconnected this plugin.");
				}
				bool isAuthFailure = reason != null && (reason.Contains("Authorization", StringComparison.OrdinalIgnoreCase) || reason.Contains("Token", StringComparison.OrdinalIgnoreCase));
				await _mcpManager.ForceDisconnect();
				await _connectionManager.Disconnect();
				if (isAuthFailure)
				{
					_logger.LogWarning("Server rejected authorization. Firing OnAuthorizationRejected event.");
					_connectionManager.NotifyAuthorizationRejected();
				}
			}
		}), (ICollection<IDisposable>)_serverEventsDisposables);
		if (_mcpManager.ToolHub != null)
		{
			Disposable.AddTo<IDisposable>(HubConnectionExtensions.On<RequestCallTool, ResponseData<ResponseCallTool>>(hubConnection, "RunCallTool", (Func<RequestCallTool, Task<ResponseData<ResponseCallTool>>>)delegate(RequestCallTool data)
			{
				_logger.LogDebug("{class}.{method}", "IClientToolHub", "RunCallTool");
				return _mcpManager.ToolHub.RunCallTool(data);
			}), (ICollection<IDisposable>)_serverEventsDisposables);
			Disposable.AddTo<IDisposable>(HubConnectionExtensions.On<RequestListTool, ResponseData<ResponseListTool[]>>(hubConnection, "RunListTool", (Func<RequestListTool, Task<ResponseData<ResponseListTool[]>>>)delegate(RequestListTool data)
			{
				_logger.LogDebug("{class}.{method}", "IClientToolHub", "RunListTool");
				return _mcpManager.ToolHub.RunListTool(data);
			}), (ICollection<IDisposable>)_serverEventsDisposables);
		}
		if (_mcpManager.PromptHub != null)
		{
			Disposable.AddTo<IDisposable>(HubConnectionExtensions.On<RequestGetPrompt, ResponseData<ResponseGetPrompt>>(hubConnection, "RunGetPrompt", (Func<RequestGetPrompt, Task<ResponseData<ResponseGetPrompt>>>)delegate(RequestGetPrompt data)
			{
				_logger.LogDebug("{class}.{method}", "IClientPromptHub", "RunGetPrompt");
				return _mcpManager.PromptHub.RunGetPrompt(data);
			}), (ICollection<IDisposable>)_serverEventsDisposables);
			Disposable.AddTo<IDisposable>(HubConnectionExtensions.On<RequestListPrompts, ResponseData<ResponseListPrompts>>(hubConnection, "RunListPrompts", (Func<RequestListPrompts, Task<ResponseData<ResponseListPrompts>>>)delegate(RequestListPrompts data)
			{
				_logger.LogDebug("{class}.{method}", "IClientPromptHub", "RunListPrompts");
				return _mcpManager.PromptHub.RunListPrompts(data);
			}), (ICollection<IDisposable>)_serverEventsDisposables);
		}
		if (_mcpManager.ResourceHub != null)
		{
			Disposable.AddTo<IDisposable>(HubConnectionExtensions.On<RequestResourceContent, ResponseData<ResponseResourceContent[]>>(hubConnection, "RunResourceContent", (Func<RequestResourceContent, Task<ResponseData<ResponseResourceContent[]>>>)delegate(RequestResourceContent data)
			{
				_logger.LogDebug("{class}.{method}", "IClientResourceHub", "RunResourceContent");
				return _mcpManager.ResourceHub.RunResourceContent(data);
			}), (ICollection<IDisposable>)_serverEventsDisposables);
			Disposable.AddTo<IDisposable>(HubConnectionExtensions.On<RequestListResources, ResponseData<ResponseListResource[]>>(hubConnection, "RunListResources", (Func<RequestListResources, Task<ResponseData<ResponseListResource[]>>>)delegate(RequestListResources data)
			{
				_logger.LogDebug("{class}.{method}", "IClientResourceHub", "RunListResources");
				return _mcpManager.ResourceHub.RunListResources(data);
			}), (ICollection<IDisposable>)_serverEventsDisposables);
			Disposable.AddTo<IDisposable>(HubConnectionExtensions.On<RequestListResourceTemplates, ResponseData<ResponseResourceTemplate[]>>(hubConnection, "RunResourceTemplates", (Func<RequestListResourceTemplates, Task<ResponseData<ResponseResourceTemplate[]>>>)delegate(RequestListResourceTemplates data)
			{
				_logger.LogDebug("{class}.{method}", "IClientResourceHub", "RunResourceTemplates");
				return _mcpManager.ResourceHub.RunResourceTemplates(data);
			}), (ICollection<IDisposable>)_serverEventsDisposables);
		}
		if (_mcpManager.SystemToolHub != null)
		{
			Disposable.AddTo<IDisposable>(HubConnectionExtensions.On<RequestCallTool, ResponseData<ResponseCallTool>>(hubConnection, "RunSystemTool", (Func<RequestCallTool, Task<ResponseData<ResponseCallTool>>>)delegate(RequestCallTool data)
			{
				_logger.LogDebug("{class}.{method}", "IClientSystemToolHub", "RunSystemTool");
				return _mcpManager.SystemToolHub.RunSystemTool(data);
			}), (ICollection<IDisposable>)_serverEventsDisposables);
			Disposable.AddTo<IDisposable>(HubConnectionExtensions.On<RequestListTool, ResponseData<ResponseListTool[]>>(hubConnection, "RunListSystemTool", (Func<RequestListTool, Task<ResponseData<ResponseListTool[]>>>)delegate(RequestListTool data)
			{
				_logger.LogDebug("{class}.{method}", "IClientSystemToolHub", "RunListSystemTool");
				return _mcpManager.SystemToolHub.RunListSystemTool(data);
			}), (ICollection<IDisposable>)_serverEventsDisposables);
		}
	}

	public Task<ResponseData> NotifyAboutUpdatedTools(RequestToolsUpdated request)
	{
		return NotifyAboutUpdatedTools(request, _cancellationTokenSource.Token);
	}

	public Task<ResponseData> NotifyAboutUpdatedTools(RequestToolsUpdated request, CancellationToken cancellationToken = default(CancellationToken))
	{
		_logger.LogTrace("{class}.{method}", "IServerMcpManager", "NotifyAboutUpdatedTools");
		return _connectionManager.InvokeAsync<RequestToolsUpdated, ResponseData>("NotifyAboutUpdatedTools", request, cancellationToken);
	}

	public Task<ResponseData> NotifyAboutUpdatedPrompts(RequestPromptsUpdated request)
	{
		return NotifyAboutUpdatedPrompts(request, _cancellationTokenSource.Token);
	}

	public Task<ResponseData> NotifyAboutUpdatedPrompts(RequestPromptsUpdated request, CancellationToken cancellationToken = default(CancellationToken))
	{
		_logger.LogTrace("{class}.{method}", "IServerMcpManager", "NotifyAboutUpdatedPrompts");
		return _connectionManager.InvokeAsync<RequestPromptsUpdated, ResponseData>("NotifyAboutUpdatedPrompts", request, cancellationToken);
	}

	public Task<ResponseData> NotifyAboutUpdatedResources(RequestResourcesUpdated request)
	{
		return NotifyAboutUpdatedResources(request, _cancellationTokenSource.Token);
	}

	public Task<ResponseData> NotifyAboutUpdatedResources(RequestResourcesUpdated request, CancellationToken cancellationToken = default(CancellationToken))
	{
		_logger.LogTrace("{class}.{method}", "IServerMcpManager", "NotifyAboutUpdatedResources");
		return _connectionManager.InvokeAsync<RequestResourcesUpdated, ResponseData>("NotifyAboutUpdatedResources", request, cancellationToken);
	}

	public Task<ResponseData> NotifyToolRequestCompleted(RequestToolCompletedData request)
	{
		return NotifyToolRequestCompleted(request, _cancellationTokenSource.Token);
	}

	public Task<ResponseData> NotifyToolRequestCompleted(RequestToolCompletedData request, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (_logger.IsEnabled(LogLevel.Trace))
		{
			_logger.LogTrace("{class}.{method} request: {RequestId}\n{Json}", "IServerMcpManager", "NotifyToolRequestCompleted", request.RequestId, request.ToPrettyJson());
		}
		return _connectionManager.InvokeAsync<RequestToolCompletedData, ResponseData>("NotifyToolRequestCompleted", request, cancellationToken);
	}

	public Task<McpClientData[]> GetMcpClientData()
	{
		_logger.LogTrace("{class}.{method}", "IServerMcpManager", "GetMcpClientData");
		return _connectionManager.InvokeAsync<McpClientData[]>("GetMcpClientData", _cancellationTokenSource.Token);
	}

	public async Task<McpServerData> GetMcpServerData()
	{
		_logger.LogTrace("{class}.{method}", "IServerMcpManager", "GetMcpServerData");
		try
		{
			return await _connectionManager.InvokeAsync<McpServerData>("GetMcpServerData", _cancellationTokenSource.Token);
		}
		catch (Exception ex)
		{
			// The Feeder Matrix server (v0.2.1+) does not expose the legacy "/hub/mcp-server" hub —
			// it serves the FMP hub at "/fmp" and MCP at "/mcp". Fall back to the /healthz endpoint so
			// the connector UI still reports live server data.
			_logger.LogDebug(ex, "{class}.{method}: MCP manager hub unavailable — falling back to /healthz.", "McpManagerClientHub", "GetMcpServerData");
			return await FetchServerDataFromHealthzAsync();
		}
	}

	async Task<McpServerData> FetchServerDataFromHealthzAsync()
	{
		try
		{
			string host = _connectionOptions?.Value?.Host ?? string.Empty;
			if (string.IsNullOrWhiteSpace(host))
			{
				_logger.LogWarning("{class}.{method}: ConnectionConfig.Host is empty — cannot query /healthz.", "McpManagerClientHub", "FetchServerDataFromHealthzAsync");
				return new McpServerData();
			}

			using (var client = new HttpClient())
			{
				client.Timeout = TimeSpan.FromSeconds(3);
				string json = await client.GetStringAsync(host.TrimEnd('/') + "/healthz");
				using (JsonDocument doc = JsonDocument.Parse(json))
				{
					JsonElement root = doc.RootElement;
					McpServerData data = new McpServerData
					{
						ServerVersion = root.TryGetProperty("version", out JsonElement version) ? version.GetString() : null,
						ServerApiVersion = root.TryGetProperty("apiVersion", out JsonElement api) ? api.GetString() : null,
						ServerTransport = Consts.MCP.Server.TransportMethod.streamableHttp,
					};
					if (root.TryGetProperty("unityLinks", out JsonElement links) && links.ValueKind == JsonValueKind.Array && links.GetArrayLength() > 0)
					{
						JsonElement first = links[0];
						if (first.TryGetProperty("connected", out JsonElement connected))
						{
							data.IsAiAgentConnected = connected.GetBoolean();
						}
					}
					return data;
				}
			}
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "{class}.{method}: Failed to fetch server data from /healthz.", "McpManagerClientHub", "FetchServerDataFromHealthzAsync");
			return new McpServerData();
		}
	}

	protected override Task OnConnectedAsync(CancellationToken cancellationToken)
	{
		_logger.LogDebug("{class}.{method} Connected. Waiting for server-pushed initial client data snapshot.", "McpManagerClientHub", "OnConnectedAsync");
		return Task.CompletedTask;
	}
}
}
