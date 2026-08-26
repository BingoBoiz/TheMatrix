---
name: profiler-start
description: "Enable Unity's runtime profiler and open the Profiler window. Idempotent: calling when already enabled returns the current enabled state without error."
---

# Profiler / Start

Enables `UnityEngine.Profiling.Profiler.enabled = true` and opens `Window > Analysis > Profiler` via `EditorApplication.ExecuteMenuItem`. Returns `true` once the profiler is enabled.

## Behavior

Uses only built-in Unity APIs (`UnityEngine.Profiling`, `UnityEditor.EditorApplication`). No external Unity package is required.

Snapshot-based: this tool does not stream historical frame data — use Unity's Profiler window directly for that.

## How to Call

Call this tool through the MCP client connected to the local Matrix AI Connector server.

Example input:
```json
{
  "nothing": "value"
}
```

> For complex input, keep the payload as valid JSON and send it through your connected MCP client.

Read the /feeder-mcp-initial-setup skill for local connection setup.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `nothing` | `string` | No |  |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "nothing": {
      "type": "string"
    }
  }
}
```

## Output

### Output JSON Schema

```json
{
  "type": "object",
  "properties": {
    "result": {
      "type": "boolean"
    }
  },
  "required": [
    "result"
  ]
}
```

