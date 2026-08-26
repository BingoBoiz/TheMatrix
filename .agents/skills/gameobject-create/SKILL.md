---
name: gameobject-create
description: Create a new GameObject in the currently opened Prefab or active Scene, optionally parented under another GameObject and pre-positioned. Pass `primitiveType` to spawn a Unity primitive (Cube, Sphere, etc.) instead of an empty GameObject.
---

# GameObject / Create

Create a new GameObject in opened Prefab or in a Scene. If needed - provide proper 'position', 'rotation' and 'scale' to reduce amount of operations.

## Inputs

- `name` — required non-empty name.
- `parentGameObjectRef` (optional) — when provided, the new GameObject is parented under this one (`SetParent(parent, worldPositionStays: false)`); otherwise it's created at scene/prefab root.
- `position` / `rotation` / `scale` — optional transform; default to zero / zero / one.
- `isLocalSpace` — when `true`, applies the transform in local space relative to the parent.
- `primitiveType` (optional) — when set, the GameObject is created via `GameObject.CreatePrimitive` (adds the appropriate renderer/collider for the primitive shape).

## How to Call

Call this tool through the MCP client connected to the local Matrix AI Connector server.

Example input:
```json
{
  "name": "value",
  "parentGameObjectRef": {
    "instanceID": 0
  },
  "position": {},
  "rotation": {},
  "scale": {},
  "isLocalSpace": false,
  "primitiveType": {}
}
```

> For complex input, keep the payload as valid JSON and send it through your connected MCP client.

Read the /feeder-mcp-initial-setup skill for local connection setup.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `name` | `string` | Yes | Name of the new GameObject. |
| `parentGameObjectRef` | `any` | No | Parent GameObject reference. If not provided, the GameObject will be created at the root of the scene or prefab. |
| `position` | `any` | No | Transform position of the GameObject. |
| `rotation` | `any` | No | Transform rotation of the GameObject. Euler angles in degrees. |
| `scale` | `any` | No | Transform scale of the GameObject. |
| `isLocalSpace` | `boolean` | No | World or Local space of transform. |
| `primitiveType` | `any` | No |  |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "name": {
      "type": "string"
    },
    "parentGameObjectRef": {
      "$ref": "#/$defs/AIGD.GameObjectRef"
    },
    "position": {
      "$ref": "#/$defs/UnityEngine.Vector3"
    },
    "rotation": {
      "$ref": "#/$defs/UnityEngine.Vector3"
    },
    "scale": {
      "$ref": "#/$defs/UnityEngine.Vector3"
    },
    "isLocalSpace": {
      "type": "boolean"
    },
    "primitiveType": {
      "$ref": "#/$defs/UnityEngine.PrimitiveType"
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
    },
    "UnityEngine.Vector3": {
      "type": "object",
      "properties": {
        "x": {
          "type": "number"
        },
        "y": {
          "type": "number"
        },
        "z": {
          "type": "number"
        }
      },
      "required": [
        "x",
        "y",
        "z"
      ],
      "additionalProperties": false
    },
    "UnityEngine.PrimitiveType": {
      "type": "string",
      "enum": [
        "Sphere",
        "Capsule",
        "Cylinder",
        "Cube",
        "Plane",
        "Quad"
      ]
    }
  },
  "required": [
    "name"
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
      "$ref": "#/$defs/AIGD.GameObjectRef",
      "description": "Find GameObject in opened Prefab or in the active Scene."
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
    "result"
  ]
}
```

