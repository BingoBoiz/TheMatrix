---
name: profiler-enable-module
description: Toggle the wrapper's local 'enabled' flag for a named profiler module. Bookkeeping only — Unity's runtime API does not expose direct module control; for real module visibility use the Profiler window.
---

# Profiler / Enable Module

Adds or removes the given module name from the wrapper's `EnabledModules` set. This is local bookkeeping consumed by `profiler-get-status` and `profiler-list-modules`; Unity's runtime API does not allow programmatic toggling of Profiler-window modules from a built-in namespace, so this tool intentionally does not pretend to.

## Inputs

- `moduleName` (required) — one of the names returned by `profiler-list-modules`.
- `enabled` (default `true`) — set to `false` to mark the module disabled.

## Errors

- Returns an `[Error]` string when `moduleName` is empty or unknown.

## How to Call

Call this tool through the MCP client connected to the local Matrix AI Connector server.

Example input:
```json
{
  "moduleName": "value",
  "enabled": false
}
```

> For complex input, keep the payload as valid JSON and send it through your connected MCP client.

Read the /feeder-mcp-initial-setup skill for local connection setup.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `moduleName` | `string` | Yes | Profiler module name (e.g. 'CPU', 'GPU', 'Memory'). |
| `enabled` | `boolean` | No | True to mark the module enabled in local bookkeeping; false to mark disabled. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "moduleName": {
      "type": "string"
    },
    "enabled": {
      "type": "boolean"
    }
  },
  "required": [
    "moduleName"
  ]
}
```

## Output

### Output JSON Schema

```json
{
  "type": "object",
  "properties": {
    "result": {
      "type": "string"
    }
  },
  "required": [
    "result"
  ]
}
```

