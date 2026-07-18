using System.Text;
using System.Text.Json;
using Feeder.Matrix.Protocol;
using Feeder.Matrix.Unity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace Feeder.Matrix.Backends;

/// <summary>Routes MCP tool traffic to the linked Unity editor over the FMP hub.</summary>
public sealed class UnityLinkBackend(
    UnityLinkRegistry registry,
    IHubContext<FmpHub, IFmpUnityClient> hub,
    IOptions<MatrixOptions> options,
    ILogger<UnityLinkBackend> logger) : IUnityBackend
{
    public ValueTask<IReadOnlyList<Tool>> ListToolsAsync(CancellationToken ct)
    {
        var link = RequireLink();
        var snapshot = link.Registry
            ?? throw new McpProtocolException("Unity editor is linked but has not published its tool registry yet.", McpErrorCode.InternalError);

        // Registry-hash-keyed conversion cache lands in Phase 6; snapshot conversion is already reflection-free.
        IReadOnlyList<Tool> tools = snapshot.Tools.Select(t => new Tool
        {
            Name = t.Name,
            Title = t.Title,
            Description = t.Description,
            InputSchema = JsonSerializer.Deserialize<JsonElement>(t.InputSchemaJsonUtf8),
        }).ToArray();
        return ValueTask.FromResult(tools);
    }

    public async ValueTask<CallToolResult> CallToolAsync(CallToolRequestParams request, CancellationToken ct)
    {
        var link = RequireLink();
        var operationId = Guid.NewGuid().ToString("N");
        var deadline = DateTimeOffset.UtcNow + options.Value.DefaultToolTimeout;

        var argsJson = request.Arguments is null
            ? "{}"u8.ToArray()
            : JsonSerializer.SerializeToUtf8Bytes(request.Arguments);

        var tcs = registry.CreateOperation(operationId);
        try
        {
            await hub.Clients.Client(link.ConnectionId).InvokeTool(new ToolInvoke
            {
                OperationId = operationId,
                ToolName = request.Name,
                ArgumentsJsonUtf8 = argsJson,
                DeadlineUnixMs = deadline.ToUnixTimeMilliseconds(),
            });

            ToolResult result;
            try
            {
                result = await tcs.Task.WaitAsync(options.Value.DefaultToolTimeout, ct);
            }
            catch (TimeoutException)
            {
                await TryCancelAsync(link, operationId);
                throw new McpProtocolException($"Tool '{request.Name}' timed out after {options.Value.DefaultToolTimeout}.", McpErrorCode.InternalError);
            }
            catch (OperationCanceledException)
            {
                await TryCancelAsync(link, operationId);
                throw;
            }

            return ToCallToolResult(request.Name, result);
        }
        finally
        {
            registry.AbandonOperation(operationId);
        }
    }

    private UnityLink RequireLink()
        => registry.GetDefaultLink()
           ?? throw new McpProtocolException("No Unity editor is connected to the matrix.", McpErrorCode.InternalError);

    private async Task TryCancelAsync(UnityLink link, string operationId)
    {
        try
        {
            await hub.Clients.Client(link.ConnectionId).CancelTool(operationId);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed to send cancel for operation {OperationId}", operationId);
        }
    }

    private static CallToolResult ToCallToolResult(string toolName, ToolResult result)
    {
        if (!result.Success)
        {
            var error = result.Error;
            return new CallToolResult
            {
                IsError = true,
                Content = [new TextContentBlock { Text = error is null ? $"Tool '{toolName}' failed." : $"[{error.Code}] {error.Message}" }],
            };
        }

        if (result.ContentJsonUtf8 is { Length: > 0 } json)
        {
            // Unity sends a full CallToolResult-shaped JSON payload (content blocks + structuredContent).
            var parsed = JsonSerializer.Deserialize<CallToolResult>(json, McpJsonUtilities.DefaultOptions);
            if (parsed is not null)
                return parsed;
        }

        return new CallToolResult { Content = [new TextContentBlock { Text = string.Empty }] };
    }
}
