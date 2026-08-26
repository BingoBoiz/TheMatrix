---
name: script-update-or-create
description: Write a `.cs` script file (create or overwrite) with the provided C# code. Validates syntax via Roslyn before write — invalid code is rejected with error details and the file is left untouched. Refreshes the AssetDatabase and delivers the final result via `requestId` after Unity finishes the triggered compilation. Use 'script-read' to inspect existing content first.
---

# Script / Update or Create

Updates or creates script file with the provided C# code. Does AssetDatabase.Refresh() at the end. Provides compilation error details if the code has syntax errors. Use 'script-read' tool to read existing script files first.

## Inputs

- `filePath` — required `.cs` path.
- `content` — C# source. MUST pass `ScriptUtils.IsValidCSharpSyntax`.
- `requestId` — required for the processing/delivered-later contract.

## Behavior

Creates any missing parent directories, writes the file, then calls `AssetDatabase.Refresh` and schedules a post-compilation notification so the final response is delivered after Unity finishes the recompile.

## How to Call

Call this tool through the MCP client connected to the local Matrix AI Connector server.

Example input:
```json
{
  "filePath": "Assets/Scripts/MyScript.cs",
  "content": "value"
}
```

> For complex input, keep the payload as valid JSON and send it through your connected MCP client.

Read the /feeder-mcp-initial-setup skill for local connection setup.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `filePath` | `string` | Yes | The path to the file. Sample: "Assets/Scripts/MyScript.cs". |
| `content` | `string` | Yes | C# code - content of the file. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "filePath": {
      "type": "string"
    },
    "content": {
      "type": "string"
    }
  },
  "required": [
    "filePath",
    "content"
  ]
}
```

## Output

This tool does not return structured output.

