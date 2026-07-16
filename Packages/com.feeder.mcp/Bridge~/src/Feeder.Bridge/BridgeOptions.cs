namespace Feeder.Bridge;

public sealed class BridgeOptions
{
    public const string SectionName = "FeederBridge";

    /// <summary>Loopback port for both the MCP endpoint and the FBP hub.</summary>
    public int Port { get; set; } = Protocol.FbpConstants.DefaultPort;

    /// <summary>When true and no Unity editor is linked, tool calls are served by the in-process mock backend (tests, bridge-only development).</summary>
    public bool EnableMockBackend { get; set; }

    /// <summary>Default per-request timeout when the client sets no deadline.</summary>
    public TimeSpan DefaultToolTimeout { get; set; } = TimeSpan.FromSeconds(60);

    public long MaxRequestBodyBytes { get; set; } = 16 * 1024 * 1024;

    /// <summary>Require a bearer token on MCP requests and the same token in the Unity FBP handshake.</summary>
    public bool RequireAuth { get; set; }

    /// <summary>Per-project token supplied by the Unity package when authentication is enabled.</summary>
    public string? Token { get; set; }
}
