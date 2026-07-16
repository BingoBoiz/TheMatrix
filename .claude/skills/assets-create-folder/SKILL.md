---
name: assets-create-folder
description: Create a new folder under a parent folder inside 'Assets/'. The parent path must start with 'Assets/' and every intermediate folder in it must already exist. Refreshes the AssetDatabase at the end and returns the GUID(s) of the created folder(s).
---

# Assets / Create Folder

Creates a new folder in the specified parent folder. The parent folder string must start with the 'Assets' folder, and all folders within the parent folder string must already exist. For example, when specifying 'Assets/ParentFolder1/ParentFolder2/', the new folder will be created in 'ParentFolder2' only if ParentFolder1 and ParentFolder2 already exist. Use it to organize scripts and assets in the project. Does AssetDatabase.Refresh() at the end. Returns the GUID of the newly created folder, if successful.

## Inputs

- `inputs` — list of `{ParentFolderPath, NewFolderName}` entries. Each entry is processed independently; per-entry errors are collected in the response so a single bad input does not abort the batch.

## Validation

- `NewFolderName` must be non-empty and must not contain any of `/`, `\`, `<`, `>`, `:`, `"`, `|`, `?`, `*`, or control characters (these checks are cross-platform even on Linux/Mac).
- `ParentFolderPath` must already exist as an `AssetDatabase.IsValidFolder` path.
- A folder with the same target name must not already exist under the parent.

## How to Call

Call this tool through the MCP client connected to the local Matrix AI Connector server.

Example input:
```json
{
  "inputs": "string_value"
}
```

> For complex input, keep the payload as valid JSON and send it through your connected MCP client.

Read the /feeder-mcp-initial-setup skill for local connection setup.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `inputs` | `any` | Yes | The paths for the folders to create. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "inputs": {
      "$ref": "#/$defs/AIGD.CreateFolderInput-1"
    }
  },
  "$defs": {
    "AIGD.CreateFolderInput": {
      "type": "object",
      "properties": {
        "ParentFolderPath": {
          "type": "string",
          "description": "The parent folder path where the new folder will be created."
        },
        "NewFolderName": {
          "type": "string",
          "description": "The name of the new folder to create."
        }
      }
    },
    "AIGD.CreateFolderInput-1": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/AIGD.CreateFolderInput"
      }
    }
  },
  "required": [
    "inputs"
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
      "$ref": "#/$defs/AIGD.CreateFolderResponse"
    }
  },
  "$defs": {
    "System.Collections.Generic.List(System.String)": {
      "type": "array",
      "items": {
        "type": "string"
      }
    },
    "AIGD.CreateFolderResponse": {
      "type": "object",
      "properties": {
        "CreatedFolderGuids": {
          "$ref": "#/$defs/System.Collections.Generic.List(System.String)",
          "description": "List of GUIDs of created folders."
        },
        "Errors": {
          "$ref": "#/$defs/System.Collections.Generic.List(System.String)",
          "description": "List of errors encountered during folder creation."
        }
      }
    }
  },
  "required": [
    "result"
  ]
}
```

