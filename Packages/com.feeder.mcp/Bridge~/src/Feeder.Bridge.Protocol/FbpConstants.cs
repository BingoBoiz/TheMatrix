namespace Feeder.Bridge.Protocol;

public static class FbpConstants
{
    public const string ProtocolVersion = "1";

    /// <summary>SignalR hub path on the bridge (loopback only).</summary>
    public const string HubPath = "/fbp";

    /// <summary>MCP Streamable HTTP path on the bridge.</summary>
    public const string McpPath = "/mcp";

    public const int DefaultPort = 20270;
    public const int DefaultQueueCapacity = 32;
    public const int HeartbeatIntervalSeconds = 5;
    public const int LinkResumeTtlSeconds = 120;
}

/// <summary>Editor state reported in handshake and heartbeats.</summary>
public enum FbpEditorState
{
    Ready = 0,
    Compiling = 1,
    Reloading = 2,
    Playing = 3,
    Paused = 4,
}

/// <summary>Stable FBP error codes (see protocol spec §4).</summary>
public static class FbpErrorCodes
{
    public const string VersionUnsupported = "FBP_VERSION_UNSUPPORTED";
    public const string AuthFailed = "FBP_AUTH_FAILED";
    public const string QueueFull = "FBP_QUEUE_FULL";
    public const string DeadlineExceeded = "FBP_DEADLINE_EXCEEDED";
    public const string Cancelled = "FBP_CANCELLED";
    public const string ToolNotFound = "FBP_TOOL_NOT_FOUND";
    public const string ToolFailed = "FBP_TOOL_FAILED";
    public const string UnityUnavailable = "FBP_UNITY_UNAVAILABLE";
    public const string Internal = "FBP_INTERNAL";
}
