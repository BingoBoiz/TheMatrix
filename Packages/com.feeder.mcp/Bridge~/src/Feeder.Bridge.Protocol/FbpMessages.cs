using MessagePack;

namespace Feeder.Bridge.Protocol;

// FBP/1 message DTOs. Integer keys keep MessagePack frames compact; only append keys - never
// renumber - within protocol v1 (unknown keys are ignored by both sides).

[MessagePackObject]
public sealed record FbpError
{
    [Key(0)] public required string Code { get; init; }
    [Key(1)] public required string Message { get; init; }
    [Key(2)] public bool Retriable { get; init; }
    [Key(3)] public byte[]? DetailsJsonUtf8 { get; init; }
}

[MessagePackObject]
public sealed record UnityRegisterRequest
{
    [Key(0)] public required string ProtocolVersion { get; init; }
    [Key(1)] public required string ProjectId { get; init; }
    [Key(2)] public required string UnityInstanceId { get; init; }
    [Key(3)] public required string ProjectPathHash { get; init; }
    [Key(4)] public required string UnityVersion { get; init; }
    [Key(5)] public required string PluginVersion { get; init; }
    [Key(6)] public required string[] SupportedProtocolVersions { get; init; }
    [Key(7)] public required string ToolRegistryHash { get; init; }
    [Key(8)] public required string AuthToken { get; init; }
    [Key(9)] public FbpEditorState EditorState { get; init; }
    [Key(10)] public string[] PendingOperations { get; set; } = [];
}

[MessagePackObject]
public sealed record UnityRegisterResponse
{
    [Key(0)] public required string SessionId { get; init; }
    [Key(1)] public required string ChosenProtocolVersion { get; init; }
    [Key(2)] public bool RegistryUpToDate { get; init; }
    [Key(3)] public string[] AwaitedOperations { get; set; } = [];
    [Key(4)] public int QueueCapacity { get; set; } = FbpConstants.DefaultQueueCapacity;
    [Key(5)] public int HeartbeatIntervalSeconds { get; set; } = FbpConstants.HeartbeatIntervalSeconds;
    [Key(6)] public FbpError? Error { get; init; }
}

[MessagePackObject]
public sealed record ToolDescriptor
{
    [Key(0)] public required string Name { get; init; }
    [Key(1)] public string? Title { get; init; }
    [Key(2)] public string? Description { get; init; }
    [Key(3)] public required byte[] InputSchemaJsonUtf8 { get; init; }
    [Key(4)] public bool ReadOnly { get; init; }
    [Key(5)] public bool Destructive { get; init; }
    [Key(6)] public bool Idempotent { get; init; }
    [Key(7)] public bool ThreadSafe { get; init; }
}

[MessagePackObject]
public sealed record RegistrySnapshot
{
    [Key(0)] public required string ProjectId { get; init; }
    [Key(1)] public required string RegistryHash { get; init; }
    [Key(2)] public ToolDescriptor[] Tools { get; set; } = [];
    // Prompts/resources descriptors follow the same pattern when the plugin exposes them (Phase 6).
}

[MessagePackObject]
public sealed record ToolInvoke
{
    [Key(0)] public required string OperationId { get; init; }
    [Key(1)] public required string ToolName { get; init; }
    [Key(2)] public required byte[] ArgumentsJsonUtf8 { get; init; }
    [Key(3)] public long DeadlineUnixMs { get; init; }
    [Key(4)] public string? TraceId { get; init; }
}

[MessagePackObject]
public sealed record ToolResult
{
    [Key(0)] public required string OperationId { get; init; }
    [Key(1)] public bool Success { get; init; }
    [Key(2)] public byte[]? ContentJsonUtf8 { get; init; }
    [Key(3)] public FbpError? Error { get; init; }
}

[MessagePackObject]
public sealed record ToolProgress
{
    [Key(0)] public required string OperationId { get; init; }
    [Key(1)] public double? Progress { get; init; }
    [Key(2)] public string? Message { get; init; }
}

[MessagePackObject]
public sealed record HeartbeatMessage
{
    [Key(0)] public required string UnityInstanceId { get; init; }
    [Key(1)] public FbpEditorState EditorState { get; init; }
}

[MessagePackObject]
public sealed record McpClientInfo
{
    [Key(0)] public required string SessionId { get; init; }
    [Key(1)] public string ClientName { get; set; } = string.Empty;
    [Key(2)] public string ClientTitle { get; set; } = string.Empty;
    [Key(3)] public string ClientVersion { get; set; } = string.Empty;
    [Key(4)] public bool IsConnected { get; init; }
}
