using System.Net;
using Feeder.Bridge;
using Feeder.Bridge.Backends;
using Feeder.Bridge.Protocol;
using Feeder.Bridge.Unity;
using Microsoft.AspNetCore.Http.Connections;
using ModelContextProtocol.Protocol;
using Serilog;
using Serilog.Events;

if (StdioMode.ShouldRun(args))
    return await StdioMode.RunAsync(args);

var logDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FeederBridge", "logs");
Directory.CreateDirectory(logDir);

// stderr + rolling file only. MCP stdio framing lives in the shim process, but the rule holds
// bridge-wide: nothing is ever written to stdout.
var loggerConfiguration = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .WriteTo.File(Path.Combine(logDir, "bridge-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14);

// Unity redirects child stderr while its AppDomain is alive. A domain reload closes that pipe;
// writing to it can terminate an otherwise persistent bridge. The Unity launcher therefore opts
// into file-only logging. Manual bridge launches still get stderr diagnostics.
if (Environment.GetEnvironmentVariable("FEEDER_BRIDGE_FILE_LOG_ONLY") != "1")
    loggerConfiguration.WriteTo.Console(standardErrorFromLevel: LogEventLevel.Verbose);

Log.Logger = loggerConfiguration.CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();
    builder.Services.Configure<BridgeOptions>(builder.Configuration.GetSection(BridgeOptions.SectionName));

    var bridgeOptions = builder.Configuration.GetSection(BridgeOptions.SectionName).Get<BridgeOptions>() ?? new BridgeOptions();

    builder.WebHost.ConfigureKestrel(kestrel =>
    {
        // Loopback only — never 0.0.0.0 (ADR-0005).
        kestrel.Listen(IPAddress.Loopback, bridgeOptions.Port);
        kestrel.Limits.MaxRequestBodySize = bridgeOptions.MaxRequestBodyBytes;
    });

    builder.Services.AddSingleton<UnityLinkRegistry>();
    builder.Services.AddSingleton<McpClientTracker>();
    builder.Services.AddSingleton<MockUnityBackend>();
    builder.Services.AddSingleton<UnityLinkBackend>();
    builder.Services.AddSingleton<IUnityBackend, BackendRouter>();

    builder.Services
        .AddSignalR(options => options.MaximumReceiveMessageSize = bridgeOptions.MaxRequestBodyBytes)
        .AddMessagePackProtocol();

    builder.Services
        .AddMcpServer(options =>
        {
            options.ServerInfo = new Implementation { Name = "feeder-mcp-server", Version = BridgeVersion.Value };
        })
        .WithHttpTransport(options =>
        {
            // Wrap each MCP session so connected AI clients are mirrored to Unity (FBP McpClientsChanged).
#pragma warning disable MCPEXP002 // RunSessionHandler is experimental in the MCP SDK; revisit on SDK upgrades.
            options.RunSessionHandler = (context, server, ct) =>
                context.RequestServices.GetRequiredService<McpClientTracker>().RunSessionAsync(server, ct);
#pragma warning restore MCPEXP002
        })
        .WithListToolsHandler(async (context, ct) =>
        {
            var backend = context.Services!.GetRequiredService<IUnityBackend>();
            var tools = await backend.ListToolsAsync(ct);
            return new ListToolsResult { Tools = [.. tools] };
        })
        .WithCallToolHandler(async (context, ct) =>
        {
            var backend = context.Services!.GetRequiredService<IUnityBackend>();
            return await backend.CallToolAsync(context.Params!, ct);
        })
        .WithListPromptsHandler((_, _) => ValueTask.FromResult(new ListPromptsResult()))
        .WithGetPromptHandler((context, _) =>
            ValueTask.FromResult<GetPromptResult>(throwPromptNotFound(context.Params?.Name)))
        .WithListResourcesHandler((_, _) => ValueTask.FromResult(new ListResourcesResult()))
        .WithListResourceTemplatesHandler((_, _) => ValueTask.FromResult(new ListResourceTemplatesResult()))
        .WithReadResourceHandler((context, _) =>
            ValueTask.FromResult<ReadResourceResult>(throwResourceNotFound(context.Params?.Uri)));

    var app = builder.Build();

    // DNS-rebinding defense: loopback callers must present a loopback Host header (ADR-0005).
    app.Use(async (context, next) =>
    {
        var host = context.Request.Host.Host;
        if (host is not ("127.0.0.1" or "localhost" or "[::1]"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Forbidden host.");
            return;
        }

        if (context.Request.Path.StartsWithSegments(FbpConstants.McpPath) &&
            bridgeOptions.RequireAuth &&
            !BridgeAuthentication.HasValidBearerToken(context.Request.Headers.Authorization, bridgeOptions.Token))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Unauthorized.");
            return;
        }
        await next();
    });

    app.MapMcp(FbpConstants.McpPath);
    app.MapHub<FbpHub>(FbpConstants.HubPath, o => o.Transports = HttpTransportType.WebSockets);

    // Local health endpoint — intentionally outside the MCP surface.
    app.MapGet("/healthz", (UnityLinkRegistry registry, McpClientTracker mcpClients) => Results.Json(new
    {
        status = "ok",
        version = BridgeVersion.Value,
        mcpClients = mcpClients.Snapshot().Select(c => new { c.SessionId, c.ClientName, c.ClientVersion }),
        unityLinks = registry.Links.Select(l => new
        {
            l.ProjectId,
            l.UnityInstanceId,
            editorState = l.EditorState.ToString(),
            l.Connected,
            lastSeen = l.LastSeen.UtcDateTime,
            toolCount = l.Registry?.Tools.Length ?? 0,
        }),
    }));

    Log.Information("Feeder Local Bridge {Version} listening on 127.0.0.1:{Port} (MCP {McpPath}, FBP {HubPath})",
        BridgeVersion.Value, bridgeOptions.Port, FbpConstants.McpPath, FbpConstants.HubPath);

    app.Run();
    return 0;
}
catch (Exception e)
{
    Log.Fatal(e, "Bridge terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

static GetPromptResult throwPromptNotFound(string? name)
    => throw new ModelContextProtocol.McpProtocolException($"Unknown prompt: '{name}'", ModelContextProtocol.McpErrorCode.InvalidParams);

static ReadResourceResult throwResourceNotFound(string? uri)
    => throw new ModelContextProtocol.McpProtocolException($"Unknown resource: '{uri}'", ModelContextProtocol.McpErrorCode.InvalidParams);

public static class BridgeVersion
{
    public const string Value = "0.2.1";
}

// Exposes the entry point to WebApplicationFactory-based integration tests.
public partial class Program;
