#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Feeder.MCP.Editor.MatrixSpace
{
    /// <summary>
    /// Parses one stdout line of `claude -p --output-format stream-json --verbose` into
    /// <see cref="AgentEvent"/>s. Lenient by design: anything unrecognized becomes a
    /// <see cref="AgentEventKind.RawLine"/> event so a CLI version drift never crashes a pane.
    /// </summary>
    public static class ClaudeStreamJsonParser
    {
        private const int ToolResultPreviewMaxChars = 200;

        public static List<AgentEvent> ParseLine(string line)
        {
            var events = new List<AgentEvent>();
            if (string.IsNullOrWhiteSpace(line))
                return events;

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(line);
            }
            catch (JsonException)
            {
                events.Add(new AgentEvent(AgentEventKind.RawLine) { Text = line });
                return events;
            }

            using (doc)
            {
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object ||
                    !root.TryGetProperty("type", out var typeProp))
                {
                    events.Add(new AgentEvent(AgentEventKind.RawLine) { Text = line });
                    return events;
                }

                switch (typeProp.GetString())
                {
                    case "system":
                        ParseSystem(root, events, line);
                        break;
                    case "assistant":
                        ParseAssistant(root, events);
                        break;
                    case "user":
                        ParseUser(root, events);
                        break;
                    case "result":
                        ParseResult(root, events);
                        break;
                    default:
                        events.Add(new AgentEvent(AgentEventKind.RawLine) { Text = line });
                        break;
                }
            }

            return events;
        }

        private static void ParseSystem(JsonElement root, List<AgentEvent> events, string line)
        {
            var subtype = GetString(root, "subtype");
            if (subtype == "init")
            {
                events.Add(new AgentEvent(AgentEventKind.SystemInit)
                {
                    Subtype = subtype,
                    SessionId = GetString(root, "session_id"),
                });
            }
            else
            {
                events.Add(new AgentEvent(AgentEventKind.RawLine) { Subtype = subtype, Text = line });
            }
        }

        private static void ParseAssistant(JsonElement root, List<AgentEvent> events)
        {
            if (!TryGetContent(root, out var content))
                return;

            foreach (var block in content.EnumerateArray())
            {
                switch (GetString(block, "type"))
                {
                    case "text":
                        var text = GetString(block, "text");
                        if (!string.IsNullOrEmpty(text))
                            events.Add(new AgentEvent(AgentEventKind.AssistantText) { Text = text });
                        break;
                    case "tool_use":
                        events.Add(new AgentEvent(AgentEventKind.ToolUse)
                        {
                            ToolName = GetString(block, "name"),
                        });
                        break;
                }
            }
        }

        private static void ParseUser(JsonElement root, List<AgentEvent> events)
        {
            if (!TryGetContent(root, out var content))
                return;

            foreach (var block in content.EnumerateArray())
            {
                if (GetString(block, "type") != "tool_result")
                    continue;

                var isError = block.TryGetProperty("is_error", out var errProp) &&
                              errProp.ValueKind == JsonValueKind.True;
                events.Add(new AgentEvent(AgentEventKind.ToolResult)
                {
                    IsError = isError,
                    Text = ExtractToolResultPreview(block),
                });
            }
        }

        private static void ParseResult(JsonElement root, List<AgentEvent> events)
        {
            var evt = new AgentEvent(AgentEventKind.Result)
            {
                Subtype = GetString(root, "subtype"),
                SessionId = GetString(root, "session_id"),
                Text = GetString(root, "result"),
            };

            if (root.TryGetProperty("is_error", out var isErrProp))
                evt.IsError = isErrProp.ValueKind == JsonValueKind.True;
            else
                evt.IsError = evt.Subtype != null && evt.Subtype.StartsWith("error", StringComparison.Ordinal);

            if (root.TryGetProperty("total_cost_usd", out var costProp) &&
                costProp.ValueKind == JsonValueKind.Number)
            {
                evt.CostUsd = costProp.GetDecimal();
            }

            events.Add(evt);
        }

        private static bool TryGetContent(JsonElement root, out JsonElement content)
        {
            content = default;
            return root.TryGetProperty("message", out var message) &&
                   message.ValueKind == JsonValueKind.Object &&
                   message.TryGetProperty("content", out content) &&
                   content.ValueKind == JsonValueKind.Array;
        }

        private static string? ExtractToolResultPreview(JsonElement toolResultBlock)
        {
            if (!toolResultBlock.TryGetProperty("content", out var content))
                return null;

            string? text = null;
            if (content.ValueKind == JsonValueKind.String)
            {
                text = content.GetString();
            }
            else if (content.ValueKind == JsonValueKind.Array)
            {
                foreach (var block in content.EnumerateArray())
                {
                    if (GetString(block, "type") == "text")
                    {
                        text = GetString(block, "text");
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(text))
                return null;

            text = text!.Replace("\r", "").Replace("\n", " ");
            return text.Length <= ToolResultPreviewMaxChars
                ? text
                : text.Substring(0, ToolResultPreviewMaxChars) + "…";
        }

        private static string? GetString(JsonElement element, string property)
        {
            return element.ValueKind == JsonValueKind.Object &&
                   element.TryGetProperty(property, out var prop) &&
                   prop.ValueKind == JsonValueKind.String
                ? prop.GetString()
                : null;
        }
    }
}
