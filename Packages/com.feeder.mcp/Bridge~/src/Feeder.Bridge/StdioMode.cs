using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Feeder.StdioShim;

namespace Feeder.Bridge;

/// <summary>
/// Compatibility entry point for stdio MCP clients. The distributed server remains one executable:
/// an stdio invocation relays to a persistent HTTP bridge and starts a bridge-mode sibling process
/// when Unity has not already done so.
/// </summary>
public static class StdioMode
{
    public static bool ShouldRun(string[] args)
        => args.Any(a => a.Equals("--stdio", StringComparison.OrdinalIgnoreCase) ||
                         a.Equals("client-transport=stdio", StringComparison.OrdinalIgnoreCase) ||
                         a.Equals("--client-transport=stdio", StringComparison.OrdinalIgnoreCase));

    public static async Task<int> RunAsync(string[] args)
    {
        var port = ReadInt(args, "port", Protocol.FbpConstants.DefaultPort);
        var url = ReadValue(args, "url") ?? $"http://127.0.0.1:{port}{Protocol.FbpConstants.McpPath}";
        var token = ReadValue(args, "token");
        var requireAuth = string.Equals(ReadValue(args, "authorization"), "required", StringComparison.OrdinalIgnoreCase) ||
                          ReadBool(args, "require-auth");

        using var stdout = Console.OpenStandardOutput();
        var stdoutLock = new SemaphoreSlim(1, 1);

        async Task WriteLineToStdoutAsync(string json)
        {
            var bytes = Encoding.UTF8.GetBytes(json + "\n");
            await stdoutLock.WaitAsync();
            try
            {
                await stdout.WriteAsync(bytes);
                await stdout.FlushAsync();
            }
            finally
            {
                stdoutLock.Release();
            }
        }

        using var relay = new BridgeRelay(
            url,
            WriteLineToStdoutAsync,
            message => Console.Error.WriteLine($"[feeder-mcp-server] {message}"),
            requireAuth ? token : null);

        if (!await relay.WaitForBridgeAsync(TimeSpan.FromMilliseconds(500)))
        {
            if (!TryStartBridge(port, requireAuth, token) ||
                !await relay.WaitForBridgeAsync(TimeSpan.FromSeconds(10)))
            {
                Console.Error.WriteLine($"[feeder-mcp-server] bridge unreachable at {url} and could not be started.");
                return 1;
            }
        }

        using var stdin = Console.OpenStandardInput();
        using var reader = new StreamReader(stdin, Encoding.UTF8);
        while (await reader.ReadLineAsync() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                await relay.ForwardAsync(line, CancellationToken.None);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"[feeder-mcp-server] forward failed: {e.Message}");
                if (TryGetRequestId(line, out var id))
                {
                    var message = JsonSerializer.Serialize("Feeder bridge unreachable: " + e.Message);
                    await WriteLineToStdoutAsync(
                        "{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"error\":{\"code\":-32603,\"message\":" + message + "}}");
                }
            }
        }

        return 0;
    }

    private static bool TryStartBridge(int port, bool requireAuth, string? token)
    {
        var executable = Environment.ProcessPath;
        if (string.IsNullOrEmpty(executable) || !File.Exists(executable))
            return false;

        try
        {
            var startInfo = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = AppContext.BaseDirectory,
            };
            startInfo.ArgumentList.Add($"--FeederBridge:Port={port}");
            startInfo.ArgumentList.Add($"--FeederBridge:RequireAuth={requireAuth}");
            if (!string.IsNullOrEmpty(token))
                startInfo.ArgumentList.Add($"--FeederBridge:Token={token}");
            Process.Start(startInfo);
            return true;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"[feeder-mcp-server] failed to start bridge: {e.Message}");
            return false;
        }
    }

    private static string? ReadValue(string[] args, string key)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            foreach (var prefix in new[] { key + "=", "--" + key + "=" })
                if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return arg[prefix.Length..];

            if (arg.Equals("--" + key, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                return args[i + 1];
        }
        return null;
    }

    private static int ReadInt(string[] args, string key, int fallback)
        => int.TryParse(ReadValue(args, key), out var value) ? value : fallback;

    private static bool ReadBool(string[] args, string key)
        => bool.TryParse(ReadValue(args, key), out var value) && value;

    private static bool TryGetRequestId(string json, out string idLiteral)
    {
        idLiteral = "null";
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("id", out var id))
                return false;
            idLiteral = id.GetRawText();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
