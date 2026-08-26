using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Feeder.McpPlugin.Common;
using Feeder.McpPlugin.Common.Model;
using Feeder.McpPlugin.Skills;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using R3;

namespace Feeder.McpPlugin
{
public class McpPlugin : IMcpPlugin, IConnection, IDisposable
{
	private readonly ILogger<McpPlugin> _logger;

	private readonly IMcpManagerHub _mcpManagerHub;

	private readonly CompositeDisposable _disposables;

	private readonly CancellationTokenSource _cancellationTokenSource;

	private readonly ThreadSafeBool _isDisposed;

	private readonly Feeder.McpPlugin.Common.Version _version;

	private readonly ISkillFileGenerator _skillFileGenerator;

	private readonly SkillContentCollection _skillContentCollection;

	private readonly ConnectionConfig _connectionConfig;

	public ILogger Logger => _logger;

	public IMcpManager McpManager { get; private set; }

	public IMcpManagerHub McpManagerHub => _mcpManagerHub;

	public Feeder.McpPlugin.Common.Version Version => _version;

	public VersionHandshakeResponse? VersionHandshakeStatus => _mcpManagerHub?.VersionHandshakeStatus;

	public ulong ToolCallsCount => McpManager.ToolManager?.ToolCallsCount ?? 0;

	public ReadOnlyReactiveProperty<HubConnectionState> ConnectionState => (ReadOnlyReactiveProperty<HubConnectionState>)(((object)_mcpManagerHub?.ConnectionState) ?? ((object)new ReactiveProperty<HubConnectionState>((HubConnectionState)0)));

	public ReadOnlyReactiveProperty<bool> KeepConnected => (ReadOnlyReactiveProperty<bool>)(((object)_mcpManagerHub?.KeepConnected) ?? ((object)new ReactiveProperty<bool>(false)));

	public Observable<Unit> OnAuthorizationRejected => _mcpManagerHub?.OnAuthorizationRejected ?? Observable.Empty<Unit>();

	public McpPlugin(ILogger<McpPlugin> logger, IMcpManager mcpManager, IMcpManagerHub mcpManagerHub, Feeder.McpPlugin.Common.Version version, ISkillFileGenerator skillFileGenerator, SkillContentCollection skillContentCollection, IOptions<ConnectionConfig>? connectionConfig = null)
	{
		_disposables = new CompositeDisposable();
		_isDisposed = new ThreadSafeBool();
		_logger = logger ?? throw new ArgumentNullException("logger");
		_logger.LogTrace("{class} Ctor.", "McpPlugin");
		McpManager = mcpManager ?? throw new ArgumentNullException("mcpManager");
		_cancellationTokenSource = _disposables.ToCancellationTokenSource();
		_mcpManagerHub = mcpManagerHub ?? throw new ArgumentNullException("mcpManagerHub");
		_version = version ?? throw new ArgumentNullException("version");
		_connectionConfig = connectionConfig?.Value ?? new ConnectionConfig();
		_skillFileGenerator = skillFileGenerator ?? throw new ArgumentNullException("skillFileGenerator");
		_skillContentCollection = skillContentCollection ?? throw new ArgumentNullException("skillContentCollection");
		Disposable.AddTo<IDisposable>(ObservableSubscribeExtensions.Subscribe<HubConnectionState>(ObservableExtensions.Where<HubConnectionState>(ObservableExtensions.Where<HubConnectionState>((Observable<HubConnectionState>)(object)_mcpManagerHub.ConnectionState, (Func<HubConnectionState, bool>)delegate(HubConnectionState state)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0002: Invalid comparison between Unknown and I4
			return (int)state == 1;
		}), (Func<HubConnectionState, bool>)((HubConnectionState state) => !_cancellationTokenSource.Token.IsCancellationRequested)), (Action<HubConnectionState>)async delegate(HubConnectionState state)
		{
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			_logger.LogDebug("{method}, connection state: {state}", "ConnectionState", state);
			Enumerable.Empty<Task>();
			await _mcpManagerHub.NotifyAboutUpdatedTools(new RequestToolsUpdated());
			_logger.LogDebug("{method}, initial notifications sent.", "ConnectionState");
		}), (ICollection<IDisposable>)_disposables);
		Disposable.AddTo<IDisposable>(ObservableSubscribeExtensions.Subscribe<Unit>(McpManager.OnForceDisconnect, (Action<Unit>)delegate
		{
			_logger.LogDebug("{method}, force disconnect requested.", "OnForceDisconnect");
			_mcpManagerHub.Disconnect();
		}), (ICollection<IDisposable>)_disposables);
		IToolManager? toolManager = McpManager.ToolManager;
		if (toolManager != null)
		{
			Disposable.AddTo<IDisposable>(ObservableSubscribeExtensions.Subscribe<Unit>(ObservableExtensions.ThrottleFirst<Unit>(toolManager.OnToolsUpdated, TimeSpan.FromMilliseconds(100.0)), (Action<Unit>)async delegate
			{
				_logger.LogDebug("{method}, tools updated event received.", "OnToolsUpdated");
				if (!_cancellationTokenSource.Token.IsCancellationRequested)
				{
					try
					{
						GenerateSkillFilesIfNeeded();
					}
					catch (InvalidOperationException exception2)
					{
						_logger.LogError(exception2, "{method}: skill auto-generation skipped — host did not provide a project root. Set ConnectionConfig.ProjectRootPath or pass basePath to GenerateSkillFiles.", "OnToolsUpdated");
					}
					if (_mcpManagerHub == null)
					{
						_logger.LogWarning("{method}, RPC Router is not initialized, cannot notify about updated tools.", "OnToolsUpdated");
					}
					else
					{
						await _mcpManagerHub.NotifyAboutUpdatedTools(new RequestToolsUpdated());
					}
				}
			}), (ICollection<IDisposable>)_disposables);
		}
		try
		{
			GenerateSkillFilesIfNeeded();
		}
		catch (InvalidOperationException exception)
		{
			_logger.LogError(exception, "{ctor}: initial skill generation skipped — host did not provide a project root. Set ConnectionConfig.ProjectRootPath or pass basePath to GenerateSkillFiles.", "McpPlugin");
		}
		IPromptManager? promptManager = McpManager.PromptManager;
		if (promptManager != null)
		{
			Disposable.AddTo<IDisposable>(ObservableSubscribeExtensions.Subscribe<Unit>(ObservableExtensions.ThrottleFirst<Unit>(promptManager.OnPromptsUpdated, TimeSpan.FromMilliseconds(100.0)), (Action<Unit>)async delegate
			{
				_logger.LogDebug("{method}, prompts updated event received.", "OnPromptsUpdated");
				if (!_cancellationTokenSource.Token.IsCancellationRequested)
				{
					if (_mcpManagerHub == null)
					{
						_logger.LogWarning("{method}, RPC Router is not initialized, cannot notify about updated prompts.", "OnPromptsUpdated");
					}
					else
					{
						await _mcpManagerHub.NotifyAboutUpdatedPrompts(new RequestPromptsUpdated());
					}
				}
			}), (ICollection<IDisposable>)_disposables);
		}
		IResourceManager? resourceManager = McpManager.ResourceManager;
		if (resourceManager == null)
		{
			return;
		}
		Disposable.AddTo<IDisposable>(ObservableSubscribeExtensions.Subscribe<Unit>(ObservableExtensions.ThrottleFirst<Unit>(resourceManager.OnResourcesUpdated, TimeSpan.FromMilliseconds(100.0)), (Action<Unit>)async delegate
		{
			_logger.LogDebug("{method}, resources updated event received.", "OnResourcesUpdated");
			if (!_cancellationTokenSource.Token.IsCancellationRequested)
			{
				if (_mcpManagerHub == null)
				{
					_logger.LogWarning("{method}, RPC Router is not initialized, cannot notify about updated resources.", "OnResourcesUpdated");
				}
				else
				{
					await _mcpManagerHub.NotifyAboutUpdatedResources(new RequestResourcesUpdated());
				}
			}
		}), (ICollection<IDisposable>)_disposables);
	}

	public bool GenerateSkillFilesIfNeeded(string? path = null)
	{
		if (!_connectionConfig.GenerateSkillFiles)
		{
			return false;
		}
		return GenerateSkillFiles(path);
	}

	public bool GenerateSkillFiles(string? path = null)
	{
		string skillsPath = ResolveSkillsPath(path);
		bool result = true;
		IEnumerable<IRunTool> enumerable = McpManager.ToolManager?.GetAllTools();
		if (enumerable == null)
		{
			result = false;
		}
		else
		{
			IEnumerable<IRunTool> enumerable2 = McpManager.SystemToolManager?.GetAllTools();
			IEnumerable<IRunTool> enumerable4;
			if (enumerable2 == null)
			{
				IEnumerable<IRunTool> enumerable3 = enumerable;
				enumerable4 = enumerable3;
			}
			else
			{
				enumerable4 = enumerable.Concat(enumerable2);
			}
			IEnumerable<IRunTool> tools = enumerable4;
			if (!_skillFileGenerator.Generate(tools, skillsPath, _connectionConfig.Host))
			{
				result = false;
			}
		}
		if (_skillContentCollection.Count > 0 && !_skillFileGenerator.Generate(_skillContentCollection.Values, skillsPath))
		{
			result = false;
		}
		return result;
	}

	public bool DeleteSkillFiles(string? path = null)
	{
		string skillsPath = ResolveSkillsPath(path);
		bool result = true;
		IEnumerable<IRunTool> enumerable = McpManager.ToolManager?.GetAllTools();
		if (enumerable == null)
		{
			result = false;
		}
		else
		{
			IEnumerable<IRunTool> enumerable2 = McpManager.SystemToolManager?.GetAllTools();
			IEnumerable<IRunTool> enumerable4;
			if (enumerable2 == null)
			{
				IEnumerable<IRunTool> enumerable3 = enumerable;
				enumerable4 = enumerable3;
			}
			else
			{
				enumerable4 = enumerable.Concat(enumerable2);
			}
			IEnumerable<IRunTool> tools = enumerable4;
			if (!_skillFileGenerator.Delete(tools, skillsPath))
			{
				result = false;
			}
		}
		if (_skillContentCollection.Count > 0 && !_skillFileGenerator.Delete(_skillContentCollection.Values, skillsPath))
		{
			result = false;
		}
		return result;
	}

	private string ResolveSkillsPath(string? basePath)
	{
		string skillsPath = _connectionConfig.SkillsPath;
		if (Path.IsPathRooted(skillsPath))
		{
			return Path.GetFullPath(skillsPath);
		}
		string? obj = basePath ?? _connectionConfig.ProjectRootPath;
		if (string.IsNullOrEmpty(obj))
		{
			throw new InvalidOperationException("Cannot resolve relative SkillsPath '" + skillsPath + "': no basePath was supplied and ConnectionConfig.ProjectRootPath is not set. Host applications must either pass an explicit basePath to GenerateSkillFiles / DeleteSkillFiles, or set ConnectionConfig.ProjectRootPath at construction time. Silent fallback to Environment.CurrentDirectory has been removed to prevent skill files landing outside the host project (see GitHub issue #107).");
		}
		return Path.GetFullPath(Path.Combine(obj, skillsPath));
	}

	public Task<bool> Connect(CancellationToken cancellationToken = default(CancellationToken))
	{
		if (_isDisposed.Value)
		{
			_logger.LogWarning("{method} called but already disposed, ignored.", "Connect");
			return Task.FromResult(result: false);
		}
		_logger.LogDebug("{method} called.", "Connect");
		if (_mcpManagerHub == null)
		{
			return Task.FromResult(result: false);
		}
		return _mcpManagerHub.Connect(cancellationToken);
	}

	public Task Disconnect(CancellationToken cancellationToken = default(CancellationToken))
	{
		if (_isDisposed.Value)
		{
			_logger.LogWarning("{method} called but already disposed, ignored.", "Disconnect");
			return Task.CompletedTask;
		}
		_logger.LogDebug("{method} called.", "Disconnect");
		if (_mcpManagerHub == null)
		{
			return Task.CompletedTask;
		}
		return _mcpManagerHub.Disconnect(cancellationToken);
	}

	public void DisconnectImmediate()
	{
		if (_isDisposed.Value)
		{
			_logger.LogWarning("{method} called but already disposed, ignored.", "DisconnectImmediate");
		}
		else
		{
			_logger.LogDebug("{method} called.", "DisconnectImmediate");
			_mcpManagerHub?.DisconnectImmediate();
		}
	}

	public bool WaitForImmediateTeardown(TimeSpan timeout)
	{
		if (_isDisposed.Value)
		{
			_logger.LogWarning("{method} called but already disposed, ignored.", "WaitForImmediateTeardown");
			return true;
		}
		_logger.LogDebug("{method} called.", "WaitForImmediateTeardown");
		return _mcpManagerHub?.WaitForImmediateTeardown(timeout) ?? true;
	}

	public void Dispose()
	{
		if (_isDisposed.TrySetTrue())
		{
			_logger.LogDebug("{method} called.", "Dispose");
			_disposables.Dispose();
			try
			{
				_mcpManagerHub?.DisconnectImmediate();
			}
			catch (Exception ex)
			{
				_logger.LogError("Error during async disposal: {message}\n{stackTrace}", ex.Message, ex.StackTrace);
			}
			try
			{
				_mcpManagerHub?.Dispose();
			}
			catch (Exception ex2)
			{
				_logger.LogError("Error during async disposal: {message}\n{stackTrace}", ex2.Message, ex2.StackTrace);
			}
			McpManager.Dispose();
			_logger.LogDebug("{method} completed.", "Dispose");
		}
	}

	~McpPlugin()
	{
		Dispose();
	}
}
}
