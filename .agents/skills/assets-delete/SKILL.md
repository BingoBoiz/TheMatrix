---
name: assets-delete
description: Delete the assets at the given project paths. IRREVERSIBLE — deleted files are permanently removed from disk (no trash/undo), so verify with 'assets-find' first. Refreshes the AssetDatabase at the end.
---

# Assets / Delete

Delete the assets at paths from the project. Does AssetDatabase.Refresh() at the end. Use 'assets-find' tool to find assets before deleting.

## Warning

This is IRREVERSIBLE: `AssetDatabase.DeleteAssets` removes the files from disk permanently — there is no trash and no undo. Double-check each path with 'assets-find' before calling.

## Inputs

- `paths` — project-relative asset paths to delete. Must be non-empty.

## Behavior

Routes through `AssetDatabase.DeleteAssets`, which deletes the batch atomically. Paths Unity reports as failed are surfaced in `response.Errors`; successfully deleted paths are surfaced in `response.DeletedPaths`. The tool is destructive (removes files from disk).

## How to Call

Call this tool through the MCP client connected to the local Matrix AI Connector server.

Example input:
```json
{
  "paths": {}
}
```

> For complex input, keep the payload as valid JSON and send it through your connected MCP client.

Read the /feeder-mcp-initial-setup skill for local connection setup.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `paths` | `any` | Yes | The paths of the assets |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "paths": {
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
    "paths"
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
      "$ref": "#/$defs/AIGD.DeleteAssetsResponse"
    }
  },
  "$defs": {
    "System.Collections.Generic.List(System.String)": {
      "type": "array",
      "items": {
        "type": "string"
      }
    },
    "AIGD.DeleteAssetsResponse": {
      "type": "object",
      "properties": {
        "DeletedPaths": {
          "$ref": "#/$defs/System.Collections.Generic.List(System.String)",
          "description": "List of paths of deleted assets."
        },
        "Errors": {
          "$ref": "#/$defs/System.Collections.Generic.List(System.String)",
          "description": "List of errors encountered during delete operations."
        }
      }
    }
  },
  "required": [
    "result"
  ]
}
```

