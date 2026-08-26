using System;
using System.Threading.Tasks;
using Feeder.McpPlugin.Common;
using Feeder.ReflectorNet;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Feeder.McpPlugin;

public class HubConnectionProvider : IHubConnectionProvider
{
	private readonly ILogger _logger;

	private readonly Reflector _reflector;

	private readonly IServiceProvider _serviceProvider;

	public HubConnectionProvider(ILogger<HubConnection> logger, Reflector reflector, IServiceProvider serviceProvider)
	{
		_logger = logger ?? throw new ArgumentNullException("logger");
		_reflector = reflector ?? throw new ArgumentNullException("reflector");
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException("serviceProvider");
	}

	public Task<HubConnection> CreateConnectionAsync(string endpoint)
	{
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Expected O, but got Unknown
		_logger.LogInformation("Creating HubConnection to " + endpoint);
		try
		{
			ConnectionConfig connectionConfig = _serviceProvider.GetRequiredService<IOptions<ConnectionConfig>>().Value;
			return Task.FromResult<HubConnection>(HubConnectionBuilderExtensions.ConfigureLogging(HubConnectionBuilderExtensions.WithServerTimeout(HubConnectionBuilderExtensions.WithKeepAliveInterval(HubConnectionBuilderExtensions.WithAutomaticReconnect(HubConnectionBuilderHttpExtensions.WithUrl((IHubConnectionBuilder)new HubConnectionBuilder(), connectionConfig.Host + endpoint, (Action<HttpConnectionOptions>)delegate(HttpConnectionOptions options)
			{
				options.AccessTokenProvider = delegate
				{
					string token = connectionConfig.Token;
					return string.IsNullOrWhiteSpace(token) ? Task.FromResult<string>(null) : Task.FromResult(token);
				};
			}), (IRetryPolicy)(object)new FixedRetryPolicy(TimeSpan.FromSeconds(10.0), 3)), TimeSpan.FromSeconds(30.0)), TimeSpan.FromMinutes(5.0)).AddJsonProtocol<IHubConnectionBuilder>((Action<JsonHubProtocolOptions>)delegate(JsonHubProtocolOptions options)
			{
				SignalR_JsonConfiguration.ConfigureJsonSerializer(_reflector, options);
			}), (Action<ILoggingBuilder>)delegate(ILoggingBuilder logging)
			{
				logging.ClearProviders();
				logging.AddProvider(new ForwardLoggerProvider(_logger, "To stop seeing the error, please <b>Stop</b> the connection to MCP server in <b>AI Game Developer</b> window."));
				logging.SetMinimumLevel(LogLevel.Trace);
			}).Build());
		}
		catch (Exception ex)
		{
			_logger.LogError("Failed to create HubConnection. Exception: " + ex.Message);
			if (ex.InnerException != null)
			{
				_logger.LogError("Inner Exception: " + ex.InnerException.Message);
			}
			if (ex is TypeInitializationException { InnerException: not null } ex2)
			{
				_logger.LogError($"TypeInitializer Inner Exception: {ex2.InnerException}");
			}
			throw;
		}
	}
}
