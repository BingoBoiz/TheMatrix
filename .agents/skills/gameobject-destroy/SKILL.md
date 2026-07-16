---
name: gameobject-destroy
description: Destroy a GameObject (and all nested children) in the currently opened Prefab or active Scene. Returns the destroyed GameObject's name, path, and instance ID for confirmation. Use 'gameobject-find' to locate the target first.
---

# GameObject / Destroy

Destroy GameObject and all nested GameObjects recursively in opened Prefab or in a Scene. Use 'gameobject-find' tool to find the target GameObject first.

## Behavior

Validates the `gameObjectRef`, resolves it on the main thread, then calls `Object.DestroyImmediate` (the immediate variant is required for Editor-mode operations). Returns a `DestroyGameObjectResult` containing `DestroyedName`, `DestroyedPath`, and `DestroyedInstanceId` so the caller has a record of what was removed.

## How to Call

Call this tool through the MCP client connected to the local Matrix AI Connector server.

Example input:
```json
{
  "gameObjectRef": "string_value"
}
```

> For complex input, keep the payload as valid JSON and send it through your connected MCP client.

Read the /feeder-mcp-initial-setup skill for local connection setup.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `gameObjectRef` | `any` | Yes | Find GameObject in opened Prefab or in the active Scene. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "gameObjectRef": {
      "$ref": "#/$defs/AIGD.GameObjectRef"
    }
  },
  "$defs": {
    "System.Type": {
      "type": "string"
    },
    "AIGD.GameObjectRef": {
      "type": "object",
      "properties": {
        "instanceID": {
          "type": "integer",
          "description": "instanceID of the UnityEngine.Object. If it is '0' and 'path', 'name', 'assetPath' and 'assetGuid' is not provided, empty or null, then it will be used as 'null'. Priority: 1 (Recommended)"
        },
        "path": {
          "type": "string",
          "description": "Path of a GameObject in the hierarchy Sample 'character/hand/finger/particle'. Priority: 2."
        },
        "name": {
          "type": "string",
          "description": "Name of a GameObject in hierarchy. Priority: 3."
        },
        "assetType": {
          "$ref": "#/$defs/System.Type",
          "description": "Type of the asset."
        },
        "assetPath": {
          "type": "string",
          "description": "Path to the asset within the project. Starts with 'Assets/'"
        },
        "assetGuid": {
          "type": "string",
          "description": "Unique identifier for the asset."
        }
      },
      "required": [
        "instanceID"
      ],
      "description": "Find GameObject in opened Prefab or in the active Scene."
    }
  },
  "required": [
    "gameObjectRef"
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
      "$ref": "#/$defs/AIGD.DestroyGameObjectResult"
    }
  },
  "$defs": {
    "AIGD.DestroyGameObjectResult": {
      "type": "object",
      "properties": {
        "DestroyedName": {
          "type": "string",
          "description": "Name of the destroyed GameObject."
        },
        "DestroyedPath": {
          "type": "string",
          "description": "Hierarchy path of the destroyed GameObject."
        },
        "DestroyedInstanceId": {
          "type": "integer",
          "description": "Instance ID of the destroyed GameObject."
        }
      },
      "required": [
        "DestroyedInstanceId"
      ]
    }
  },
  "required": [
    "result"
  ]
}
```

