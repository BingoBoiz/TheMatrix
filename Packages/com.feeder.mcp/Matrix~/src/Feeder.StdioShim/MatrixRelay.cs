using System.Text;
using System.Text.Json;

namespace Feeder.StdioShim;

/// <summary>
/// Streamable-HTTP side of the shim. POSTs each client message to the matrix, relays the JSON-RPC
/// response back, tracks the Mcp-Session-Id, and (after initialize) holds one GET SSE stream open
/// for server-initiated messages.
/// </summary>
public sealed class MatrixRelay : IDisposable
{
    private readonly HttpClient _http;
    private readonly Uri _endpoint;
    private readonly Func<string, Task> _writeToClient;
    private readonly Action<string> _logError;
    private readonly string? _bearerToken;
    private string? _sessionId;
    private Task? _listenTask;
    private readonly CancellationTokenSource _cts = new();

    public MatrixRelay(string url, Func<string, Task> writeToClient, Action<string> logError, string? bearerToken = null)
    {
        _endpoint = new Uri(url);
        _writeToClient = writeToClient;
        _logError = logError;
        _bearerToken = bearerToken;
        _http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
    }

    public async Task<bool> WaitForMatrixAsync(TimeSpan timeout)
    {
        var health = new Uri(_endpoint, "/healthz");
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                using var response = await _http.GetAsync(health, cts.Token);
                if (response.IsSuccessStatusCode)
                    return true;
            }
            catch
            {
                // matrix not up yet
            }
            await Task.Delay(100);
        }
        return false;
    }

    public async Task ForwardAsync(string jsonRpcLine, CancellationToken ct)
    {
        var isInitialize = false;
        try
        {
            using var doc = JsonDocument.Parse(jsonRpcLine);
            isInitialize = doc.RootElement.TryGetProperty("method", out var m) && m.GetString() == "initialize";
        }
        catch
        {
            // forward as-is; the matrix produces the parse error response
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(jsonRpcLine, Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        AddAuthorization(request);
        if (_sessionId is not null)
            request.Headers.Add("Mcp-Session-Id", _sessionId);

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        if (response.Headers.TryGetValues("Mcp-Session-Id", out var values))
            _sessionId = values.FirstOrDefault() ?? _sessionId;

        if (response.StatusCode == System.Net.HttpStatusCode.Accepted)
            return; // notification — no body

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType == "text/event-stream")
        {
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            while (await reader.ReadLineAsync(ct) is { } line)
            {
                if (line.StartsWith("data:", StringComparison.Ordinal))
                    await _writeToClient(line[5..].TrimStart());
            }
        }
        else
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!string.IsNullOrWhiteSpace(body))
                await _writeToClient(body);
        }

        if (isInitialize && _sessionId is not null && _listenTask is null)
            _listenTask = ListenForServerMessagesAsync(_cts.Token);
    }

    /// <summary>Standing GET stream: server-initiated requests/notifications (sampling, list_changed, …).</summary>
    private async Task ListenForServerMessagesAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, _endpoint);
                request.Headers.Accept.ParseAdd("text/event-stream");
                request.Headers.Add("Mcp-Session-Id", _sessionId);
                AddAuthorization(request);

                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!response.IsSuccessStatusCode)
                {
                    await Task.Delay(1000, ct);
                    continue;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(ct);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                while (await reader.ReadLineAsync(ct) is { } line)
                {
                    if (line.StartsWith("data:", StringComparison.Ordinal))
                        await _writeToClient(line[5..].TrimStart());
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                _logError($"server-message stream dropped: {e.Message}; retrying");
                try { await Task.Delay(1000, ct); } catch (OperationCanceledException) { return; }
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _http.Dispose();
    }

    private void AddAuthorization(HttpRequestMessage request)
    {
        if (!string.IsNullOrEmpty(_bearerToken))
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _bearerToken);
    }
}
