using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Feeder.McpPlugin.Common.Hub.Client;
using Feeder.McpPlugin.Common.Model;
using Feeder.ReflectorNet;
using Microsoft.Extensions.Logging;
using R3;

namespace Feeder.McpPlugin;

public class McpManager : IMcpManager, IDisposable, IClientMcpManager, IClientDisconnectable
{
	protected readonly ILogger _logger;

	protected readonly Reflector _reflector;

	private volatile IReadOnlyList<McpClientData> _activeClients = Array.Empty<McpClientData>();

	private readonly Subject<Unit> _onForceDisconnect = new Subject<Unit>();

	private readonly Subject<McpClientData> _onClientConnected = new Subject<McpClientData>();

	private readonly Subject<McpClientData> _onClientDisconnected = new Subject<McpClientData>();

	private readonly Subject<IReadOnlyList<McpClientData>> _onClientsChanged = new Subject<IReadOnlyList<McpClientData>>();

	private readonly IToolManager? _tools;

	private readonly IPromptManager? _prompts;

	private readonly IResourceManager? _resources;

	private readonly ISystemToolManager? _systemTools;

	public Reflector Reflector => _reflector;

	public IToolManager? ToolManager => _tools;

	public IPromptManager? PromptManager => _prompts;

	public IResourceManager? ResourceManager => _resources;

	public ISystemToolManager? SystemToolManager => _systemTools;

	public IClientToolHub? ToolHub => _tools;

	public IClientPromptHub? PromptHub => _prompts;

	public IClientResourceHub? ResourceHub => _resources;

	public IClientSystemToolHub? SystemToolHub => _systemTools;

	public IReadOnlyList<McpClientData> ActiveClients => _activeClients;

	public Observable<Unit> OnForceDisconnect => ObservableExtensions.AsObservable<Unit>((Observable<Unit>)(object)_onForceDisconnect);

	public Observable<McpClientData> OnClientConnected => ObservableExtensions.AsObservable<McpClientData>((Observable<McpClientData>)(object)_onClientConnected);

	public Observable<McpClientData> OnClientDisconnected => ObservableExtensions.AsObservable<McpClientData>((Observable<McpClientData>)(object)_onClientDisconnected);

	public Observable<IReadOnlyList<McpClientData>> OnClientsChanged => ObservableExtensions.AsObservable<IReadOnlyList<McpClientData>>((Observable<IReadOnlyList<McpClientData>>)(object)_onClientsChanged);

	public McpManager(ILogger<McpManager> logger, Reflector reflector, IToolManager? tools = null, IPromptManager? prompts = null, IResourceManager? resources = null, ISystemToolManager? systemTools = null)
	{
		_logger = logger ?? throw new ArgumentNullException("logger");
		_logger.LogTrace("Ctor");
		_reflector = reflector ?? throw new ArgumentNullException("reflector");
		_tools = tools;
		_prompts = prompts;
		_resources = resources;
		_systemTools = systemTools;
	}

	public Task OnMcpClientConnected(McpClientData connectedClient, McpClientData[] allActiveClients)
	{
		_activeClients = allActiveClients;
		_onClientConnected.OnNext(connectedClient);
		_onClientsChanged.OnNext((IReadOnlyList<McpClientData>)allActiveClients);
		return Task.CompletedTask;
	}

	public Task OnMcpClientDisconnected(McpClientData disconnectedClient, McpClientData[] remainingClients)
	{
		_activeClients = remainingClients;
		_onClientDisconnected.OnNext(disconnectedClient);
		_onClientsChanged.OnNext((IReadOnlyList<McpClientData>)remainingClients);
		return Task.CompletedTask;
	}

	public Task OnInitialClientData(McpClientData[] allActiveClients)
	{
		_activeClients = allActiveClients;
		_onClientsChanged.OnNext((IReadOnlyList<McpClientData>)allActiveClients);
		return Task.CompletedTask;
	}

	public void Dispose()
	{
		_logger.LogDebug("{method} called.", "Dispose");
		_tools?.Dispose();
		_prompts?.Dispose();
		_resources?.Dispose();
		_logger.LogDebug("{method} completed.", "Dispose");
	}

	public Task ForceDisconnect(string? reason = null)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		_onForceDisconnect.OnNext(Unit.Default);
		return Task.CompletedTask;
	}
}
