using System.Collections.Concurrent;
using Feeder.Bridge.Protocol;
using Microsoft.AspNetCore.SignalR;
using ModelContextProtocol.Server;

namespace Feeder.Bridge.Unity;

/// <summary>
/// Tracks live MCP client sessions on the bridge and mirrors them to linked Unity editors via the
/// FBP <c>McpClientsChanged</c> callback, so the editor's "AI agent" indicator reflects reality.
/// </summary>
public sealed class McpClientTracker(
    IHubContext<FbpHub, IFbpUnityClient> hub,
    ILogger<McpClientTracker> logger)
{
    private readonly ConcurrentDictionary<string, McpServer> _sessions = new();

    /// <summary>Runs one MCP session while keeping the tracker (and Unity) informed of its lifetime.</summary>
    public async Task RunSessionAsync(McpServer server, CancellationToken cancellationToken)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        _sessions[sessionId] = server;
        try
        {
            var run = server.RunAsync(cancellationToken);
            _ = AnnounceWhenInitializedAsync(sessionId, server, cancellationToken);
            await run;
        }
        finally
        {
            _sessions.TryRemove(sessionId, out _);
            await BroadcastAsync();
        }
    }

    public McpClientInfo[] Snapshot()
        => _sessions.Select(pair => new McpClientInfo
        {
            SessionId = pair.Key,
            ClientName = pair.Value.ClientInfo?.Name ?? string.Empty,
            ClientTitle = pair.Value.ClientInfo?.Title ?? string.Empty,
            ClientVersion = pair.Value.ClientInfo?.Version ?? string.Empty,
            IsConnected = true,
        }).ToArray();

    public async Task BroadcastAsync()
    {
        try
        {
            await hub.Clients.All.McpClientsChanged(Snapshot());
        }
        catch (Exception e)
        {
            logger.LogDebug(e, "Failed to broadcast MCP client change to Unity links");
        }
    }

    // ClientInfo materializes once the MCP initialize request lands; wait briefly for it so the
    // notice carries the real client name instead of an empty placeholder.
    private async Task AnnounceWhenInitializedAsync(string sessionId, McpServer server, CancellationToken cancellationToken)
    {
        try
        {
            for (var i = 0; i < 40 && server.ClientInfo is null && !cancellationToken.IsCancellationRequested; i++)
                await Task.Delay(50, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }

        if (_sessions.ContainsKey(sessionId))
            await BroadcastAsync();
    }
}
