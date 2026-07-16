---
name: assets-move
description: Move or rename assets at the given project paths. Refreshes the AssetDatabase at the end. Use 'assets-find' to locate the assets first.
---

# Assets / Move

Move the assets at paths in the project. Should be used for asset rename. Does AssetDatabase.Refresh() at the end. Use 'assets-find' tool to find assets before moving.

## Inputs

- `sourcePaths` — paths of the assets to move.
- `destinationPaths` — target paths (must match `sourcePaths` length).

## Behavior

Each pair is moved independently via `AssetDatabase.MoveAsset`. Per-pair failures (Unity's `MoveAsset` returns a non-empty error string) are surfaced in `response.Errors`; successful moves accumulate into `response.MovedPaths`.

## How to Call

Call this tool through the MCP client connected to the local Matrix AI Connector server.

Example input:
```json
{
  "sourcePaths": "string_value",
  "destinationPaths": "string_value"
}
```

> For complex input, keep the payload as valid JSON and send it through your connected MCP client.

Read the /feeder-mcp-initial-setup skill for local connection setup.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `sourcePaths` | `any` | Yes | The paths of the assets to move. |
| `destinationPaths` | `any` | Yes | The paths of moved assets. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "sourcePaths": {
      "$ref": "#/$defs/System.String-1"
    },
    "destinationPaths": {
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
    "sourcePaths",
    "destinationPaths"
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
      "$ref": "#/$defs/AIGD.MoveAssetsResponse"
    }
  },
  "$defs": {
    "System.Collections.Generic.List(System.String)": {
      "type": "array",
      "items": {
        "type": "string"
      }
    },
    "AIGD.MoveAssetsResponse": {
      "type": "object",
      "properties": {
        "MovedPaths": {
          "$ref": "#/$defs/System.Collections.Generic.List(System.String)",
          "description": "List of destination paths of successfully moved assets."
        },
        "Errors": {
          "$ref": "#/$defs/System.Collections.Generic.List(System.String)",
          "description": "List of errors encountered during move operations."
        }
      }
    }
  },
  "required": [
    "result"
  ]
}
```

