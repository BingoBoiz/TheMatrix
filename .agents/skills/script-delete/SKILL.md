---
name: script-delete
description: Delete one or more `.cs` script files from disk, refresh the AssetDatabase, and wait for Unity compilation to settle before delivering the final result via the request's `requestId`. IRREVERSIBLE — the file and its `.meta` are removed permanently (no undo), so inspect with 'script-read' before deleting.
---

# Script / Delete

Delete the script file(s). Does AssetDatabase.Refresh() and waits for Unity compilation to complete before reporting results. Use 'script-read' tool to read existing script files first.

## Warning

This is IRREVERSIBLE: each `.cs` file and its sibling `.meta` are deleted from disk permanently — no undo and no trash. Read the file with 'script-read' and confirm the exact path first.

## Inputs

- `files` — non-empty array of `.cs` paths. Every entry must exist on disk.
- `requestId` — required for the processing/delivered-later contract.

## Behavior

Validates the array (non-empty, every entry ends with `.cs`, every entry exists). Deletes each file plus its sibling `.meta` (when present). Calls `AssetDatabase.Refresh` and schedules a post-compilation notification — the final response is delivered after Unity finishes the recompile triggered by the delete.

## How to Call

Call this tool through the MCP client connected to the local Matrix AI Connector server.

Example input:
```json
{
  "files": {}
}
```

> For complex input, keep the payload as valid JSON and send it through your connected MCP client.

Read the /feeder-mcp-initial-setup skill for local connection setup.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `files` | `any` | Yes | File paths to the files. Sample: "Assets/Scripts/MyScript.cs". |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "files": {
      "$ref": "#/$defs/System.String-1"
    }
  },
  "$defs": {
    "System.String-1": {
      "type": "array",
      "items": {
        "type": "string"
      }
    }
  },
  "required": [
    "files"
  ]
}
```

## Output

This tool does not return structured output.

