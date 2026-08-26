---
name: profiler-load-data
description: Read back a previously-saved JSON snapshot from `profiler-save-data` and return its raw text.
---

# Profiler / Load Data

Reads `filePath` as UTF-8 text and returns the file body unchanged. Caller is responsible for parsing.

## Inputs

- `filePath` (required) — path written by `profiler-save-data`.

## Errors

- Returns `[Error]` when `filePath` is empty, the file does not exist, exceeds the 10 MB size cap, or the read fails.

## Behavior

Uses `System.IO.File.ReadAllText` (BCL) — no external Unity package is required. Files larger than 10 MB are rejected up-front to avoid OOM on a stray call.

## How to Call

Call this tool through the MCP client connected to the local Matrix AI Connector server.

Example input:
```json
{
  "filePath": "value"
}
```

> For complex input, keep the payload as valid JSON and send it through your connected MCP client.

Read the /feeder-mcp-initial-setup skill for local connection setup.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `filePath` | `string` | Yes | Path to a profiler snapshot file previously written by 'profiler-save-data'. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "filePath": {
      "type": "string"
    }
  },
  "required": [
    "filePath"
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

