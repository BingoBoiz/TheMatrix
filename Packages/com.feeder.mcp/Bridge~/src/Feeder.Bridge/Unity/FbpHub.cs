using Feeder.Bridge.Protocol;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace Feeder.Bridge.Unity;

/// <summary>Client (Unity adapter) callback methods invoked by the bridge.</summary>
public interface IFbpUnityClient
{
    Task InvokeTool(ToolInvoke invoke);
    Task CancelTool(string operationId);
}

/// <summary>FBP/1 hub — the Unity side of the bridge. WebSocket + MessagePack only.</summary>
public sealed class FbpHub(
    UnityLinkRegistry registry,
    IOptions<BridgeOptions> options,
    ILogger<FbpHub> logger) : Hub<IFbpUnityClient>
{
    public UnityRegisterResponse Register(UnityRegisterRequest request)
    {
        if (request.ProtocolVersion != FbpConstants.ProtocolVersion &&
            !request.SupportedProtocolVersions.Contains(FbpConstants.ProtocolVersion))
        {
            return new UnityRegisterResponse
            {
                SessionId = string.Empty,
                ChosenProtocolVersion = FbpConstants.ProtocolVersion,
                Error = new FbpError
                {
                    Code = FbpErrorCodes.VersionUnsupported,
                    Message = $"Bridge speaks FBP/{FbpConstants.ProtocolVersion}; Unity offered [{string.Join(", ", request.SupportedProtocolVersions)}].",
                },
            };
        }

        if (options.Value.RequireAuth &&
            !BridgeAuthentication.FixedTimeEquals(request.AuthToken, options.Value.Token))
        {
            return new UnityRegisterResponse
            {
                SessionId = string.Empty,
                ChosenProtocolVersion = FbpConstants.ProtocolVersion,
                Error = new FbpError
                {
                    Code = FbpErrorCodes.AuthFailed,
                    Message = "Unity bridge authentication failed.",
                },
            };
        }

        var link = registry.Register(request, Context.ConnectionId);
        logger.LogInformation(
            "Unity registered: project {ProjectId}, instance {InstanceId}, editor {EditorState}, plugin {PluginVersion}, unity {UnityVersion}",
            request.ProjectId, request.UnityInstanceId, request.EditorState, request.PluginVersion, request.UnityVersion);

        return new UnityRegisterResponse
        {
            SessionId = link.SessionId,
            ChosenProtocolVersion = FbpConstants.ProtocolVersion,
            RegistryUpToDate = link.Registry?.RegistryHash == request.ToolRegistryHash,
            AwaitedOperations = registry.AwaitedOperationIds,
        };
    }

    public void Unregister(string projectId, string unityInstanceId)
    {
        registry.Unregister(projectId, unityInstanceId);
        logger.LogInformation("Unity unregistered: project {ProjectId}, instance {InstanceId}", projectId, unityInstanceId);
    }

    public void PublishRegistry(RegistrySnapshot snapshot)
    {
        var link = registry.GetLink(snapshot.ProjectId);
        if (link is null)
        {
            logger.LogWarning("Registry snapshot for unknown project {ProjectId} dropped", snapshot.ProjectId);
            return;
        }
        link.Registry = snapshot;
        logger.LogInformation("Registry updated for {ProjectId}: {ToolCount} tools, hash {Hash}",
            snapshot.ProjectId, snapshot.Tools.Length, snapshot.RegistryHash);
    }

    public void CompleteTool(ToolResult result)
    {
        if (!registry.CompleteOperation(result))
            logger.LogWarning("Tool result for unknown/expired operation {OperationId} dropped", result.OperationId);
    }

    public void ReportProgress(ToolProgress progress)
        => logger.LogDebug("Progress {OperationId}: {Progress} {Message}", progress.OperationId, progress.Progress, progress.Message);

    public void Heartbeat(HeartbeatMessage heartbeat)
    {
        foreach (var link in registry.Links)
        {
            if (link.UnityInstanceId == heartbeat.UnityInstanceId)
            {
                link.EditorState = heartbeat.EditorState;
                link.LastSeen = DateTimeOffset.UtcNow;
            }
        }
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        registry.MarkDisconnected(Context.ConnectionId);
        logger.LogInformation(exception, "Unity link disconnected ({ConnectionId})", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
