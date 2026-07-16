---
name: profiler-clear-data
description: Discard all frames currently held by the Editor Profiler (UnityEditorInternal.ProfilerDriver.ClearAllFrames). Cannot be undone.
---

# Profiler / Clear Data

Invokes `UnityEditorInternal.ProfilerDriver.ClearAllFrames()` on the main thread. `UnityEditorInternal` is a built-in editor namespace — no external Unity package is required.

## Behavior

After this call, the Profiler window's frame history is empty; subsequent recording starts from frame 0. Returns `true` on success.

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

