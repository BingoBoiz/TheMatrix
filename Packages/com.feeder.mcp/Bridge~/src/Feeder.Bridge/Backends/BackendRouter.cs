using Feeder.Bridge.Unity;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace Feeder.Bridge.Backends;

/// <summary>
/// Picks the live Unity link when one is registered; otherwise falls back to the mock backend
/// when enabled (tests / bridge-only development), else fails fast with a clear MCP error.
/// </summary>
public sealed class BackendRouter(
    UnityLinkRegistry registry,
    UnityLinkBackend unityBackend,
    MockUnityBackend mockBackend,
    IOptions<BridgeOptions> options) : IUnityBackend
{
    private IUnityBackend Resolve()
    {
        if (registry.GetDefaultLink() is not null)
            return unityBackend;
        if (options.Value.EnableMockBackend)
            return mockBackend;
        throw new McpProtocolException("No Unity editor is connected to the bridge.", McpErrorCode.InternalError);
    }

    public ValueTask<IReadOnlyList<Tool>> ListToolsAsync(CancellationToken ct) => Resolve().ListToolsAsync(ct);

    public ValueTask<CallToolResult> CallToolAsync(CallToolRequestParams request, CancellationToken ct)
        => Resolve().CallToolAsync(request, ct);
}
