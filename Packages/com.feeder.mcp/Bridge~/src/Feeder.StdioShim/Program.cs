using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Feeder.StdioShim;

// Feeder stdio shim: relays newline-delimited JSON-RPC between an stdio-only MCP client and the
// persistent Feeder Local Bridge (Streamable HTTP). Zero business logic; all diagnostics go to
// stderr — stdout carries exclusively JSON-RPC.
//
//   feeder-stdio-shim [--url http://127.0.0.1:20270/mcp] [--bridge-exe <path-to-feeder-bridge.exe>]

var url = "http://127.0.0.1:20270/mcp";
string? bridgeExe = Environment.GetEnvironmentVariable("FEEDER_BRIDGE_EXE");
for (var i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--url") url = args[i + 1];
    if (args[i] == "--bridge-exe") bridgeExe = args[i + 1];
}

using var stdout = Console.OpenStandardOutput();
var stdoutLock = new SemaphoreSlim(1, 1);
using var shutdown = new CancellationTokenSource();

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

using var relay = new BridgeRelay(url, WriteLineToStdoutAsync, msg => Console.Error.WriteLine($"[feeder-stdio-shim] {msg}"));

if (!await relay.WaitForBridgeAsync(TimeSpan.FromMilliseconds(500)))
{
    if (!TryStartBridge(bridgeExe) || !await relay.WaitForBridgeAsync(TimeSpan.FromSeconds(10)))
    {
        Console.Error.WriteLine($"[feeder-stdio-shim] bridge unreachable at {url} and could not be started.");
        return 1;
    }
}

using var stdin = Console.OpenStandardInput();
using var reader = new StreamReader(stdin, Encoding.UTF8);

while (await reader.ReadLineAsync(shutdown.Token) is { } line)
{
    if (string.IsNullOrWhiteSpace(line))
        continue;
    try
    {
        await relay.ForwardAsync(line, shutdown.Token);
    }
    catch (Exception e)
    {
        Console.Error.WriteLine($"[feeder-stdio-shim] forward failed: {e.Message}");
        if (TryGetRequestId(line, out var id))
        {
            var message = JsonSerializer.Serialize("Feeder bridge unreachable: " + e.Message);
            await WriteLineToStdoutAsync(
                "{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"error\":{\"code\":-32603,\"message\":" + message + "}}");
        }
    }
}

// stdin closed → client is gone; drop the session and exit.
shutdown.Cancel();
return 0;

static bool TryGetRequestId(string json, out string idLiteral)
{
    idLiteral = "null";
    try
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("id", out var id))
        {
            idLiteral = id.GetRawText();
            return true;
        }
        return false; // notification — nothing to answer
    }
    catch
    {
        return false;
    }
}

static bool TryStartBridge(string? bridgeExe)
{
    bridgeExe ??= Path.Combine(AppContext.BaseDirectory, "feeder-bridge.exe");
    if (!File.Exists(bridgeExe))
    {
        Console.Error.WriteLine($"[feeder-stdio-shim] bridge executable not found: {bridgeExe}");
        return false;
    }
    try
    {
        Process.Start(new ProcessStartInfo(bridgeExe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(bridgeExe)!,
        });
        Console.Error.WriteLine($"[feeder-stdio-shim] started bridge: {bridgeExe}");
        return true;
    }
    catch (Exception e)
    {
        Console.Error.WriteLine($"[feeder-stdio-shim] failed to start bridge: {e.Message}");
        return false;
    }
}
