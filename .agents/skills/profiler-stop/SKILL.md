---
name: profiler-stop
description: Disable Unity's runtime profiler. Idempotent — calling when already disabled returns the current disabled state.
---

# Profiler / Stop

Sets `UnityEngine.Profiling.Profiler.enabled = false`. Returns the post-call value of `Profiler.enabled` (expected `false`).

## Behavior

Uses only built-in Unity APIs (`UnityEngine.Profiling`). No external Unity package is required.

## How to Call

Call this tool through the MCP client connected to the local Matrix AI Connector server.

Example input:
```json
{
  "nothing": "string_value"
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

