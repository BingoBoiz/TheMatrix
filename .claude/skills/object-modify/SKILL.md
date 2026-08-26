---
name: object-modify
description: Modify a Unity `UnityEngine.Object`'s serializable fields/properties. Three modification surfaces are available (`objectDiff`, `pathPatches`, `jsonPatch`) — see the skill body. Use 'object-get-data' first to inspect the object structure. Changes are written directly into the object (no undo) — keep them minimal and verify with 'object-get-data' afterwards.
---

# Object / Modify

Modify the specified Unity Object. Allows direct modification of object fields and properties. Use 'object-get-data' first to inspect the object structure before modifying.

## Warning

Changes are applied directly to the serialized object — there is no undo. Inspect first, patch minimally, and re-read with 'object-get-data' to confirm the result.

## Three modification surfaces

Use whichever fits the task:

1. `objectDiff` — full `SerializedMember` diff (legacy, backwards compatible).
2. `pathPatches` — list of `{path, value}` pairs routed through `Reflector.TryModifyAt`; atomic per-path modification, multiple entries can target different depths.
3. `jsonPatch` — a JSON Merge Patch (RFC 7396, extended with `[i]`/`[key]` notation) routed through `Reflector.TryPatch`; multiple fields at any depth in a single call.

When more than one is supplied they run in this order: `jsonPatch` → `pathPatches` → `objectDiff`. At least one is required.

## Path syntax

`fieldName`, `nested/field`, `arrayField/[i]`, `dictField/[key]`. Leading `#/` is stripped.

## How to Call

Call this tool through the MCP client connected to the local Matrix AI Connector server.

Example input:
```json
{
  "objectRef": {
    "instanceID": 0
  },
  "objectDiff": {},
  "pathPatches": [],
  "jsonPatch": "value"
}
```

> For complex input, keep the payload as valid JSON and send it through your connected MCP client.

Read the /feeder-mcp-initial-setup skill for local connection setup.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `objectRef` | `any` | Yes | Reference to UnityEngine.Object instance. It could be GameObject, Component, Asset, etc. Anything extended from UnityEngine.Object. |
| `objectDiff` | `any` | No | Optional. The full object data to apply (legacy path). Should contain 'fields' and/or 'props' with the values to modify.
Only include the fields/properties you want to change.
Any unknown or invalid fields and properties will be reported in the response. |
| `pathPatches` | `any` | No | Optional. List of path-scoped patches routed through Reflector.TryModifyAt. |
| `jsonPatch` | `any` | No | Optional. JSON Merge Patch (RFC 7396, extended with [i]/[key] keys) routed through Reflector.TryPatch. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "objectRef": {
      "$ref": "#/$defs/AIGD.ObjectRef"
    },
    "objectDiff": {
      "$ref": "#/$defs/Feeder.ReflectorNet.Model.SerializedMember"
    },
    "pathPatches": {
      "$ref": "#/$defs/System.Collections.Generic.List(AIGD.PathPatch)"
    },
    "jsonPatch": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "object",
          "additionalProperties": true
        }
      ]
    }
  },
  "$defs": {
    "AIGD.ObjectRef": {
      "type": "object",
      "properties": {
        "instanceID": {
          "type": "integer",
          "description": "instanceID of the UnityEngine.Object. If this is '0', then it will be used as 'null'."
        }
      },
      "required": [
        "instanceID"
      ],
      "description": "Reference to UnityEngine.Object instance. It could be GameObject, Component, Asset, etc. Anything extended from UnityEngine.Object."
    },
    "Feeder.ReflectorNet.Model.SerializedMemberList": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/Feeder.ReflectorNet.Model.SerializedMember"
      }
    },
    "Feeder.ReflectorNet.Model.SerializedMember": {
      "type": "object",
      "properties": {
        "typeName": {
          "type": "string",
          "description": "Full type name. Eg: 'System.String', 'System.Int32', 'UnityEngine.Vector3', etc."
        },
        "name": {
          "type": "string",
          "description": "Object name."
        },
        "value": {
          "description": "Value of the object, serialized as a non stringified JSON element. Can be null if the value is not set. Can be default value if the value is an empty object or array json."
        },
        "fields": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/Feeder.ReflectorNet.Model.SerializedMember",
            "description": "Nested field value."
          },
          "description": "Fields of the object, serialized as a list of 'SerializedMember'."
        },
        "props": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/Feeder.ReflectorNet.Model.SerializedMember",
            "description": "Nested property value."
          },
          "description": "Properties of the object, serialized as a list of 'SerializedMember'."
        }
      },
      "required": [
        "typeName"
      ],
      "additionalProperties": false
    },
    "AIGD.PathPatch": {
      "type": "object",
      "properties": {
        "Path": {
          "type": "string",
          "description": "Slash-delimited path to the target field/element/entry. Plain segment navigates a field or property (e.g. 'admin' or 'admin/name'). Use '[i]' for array/list index (e.g. 'planets/[0]/orbitRadius'). Use '[key]' for dictionary entry (e.g. 'config/[timeout]'). A leading '#/' is stripped automatically. Required."
        },
        "Value": {
          "$ref": "#/$defs/Feeder.ReflectorNet.Model.SerializedMember",
          "description": "The new value to write at the path. Use the standard SerializedMember envelope: 'typeName' + 'value' for primitives, or nested 'fields'/'props' for complex types. Required — omitting it overwrites the target with a default empty SerializedMember."
        }
      }
    },
    "System.Collections.Generic.List(AIGD.PathPatch)": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/AIGD.PathPatch"
      }
    }
  },
  "required": [
    "objectRef"
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
      "$ref": "#/$defs/AIGD.ModifyObjectResponse"
    }
  },
  "$defs": {
    "AIGD.ObjectRef": {
      "type": "object",
      "properties": {
        "instanceID": {
          "type": "integer",
          "description": "instanceID of the UnityEngine.Object. If this is '0', then it will be used as 'null'."
        }
      },
      "required": [
        "instanceID"
      ],
      "description": "Reference to UnityEngine.Object instance. It could be GameObject, Component, Asset, etc. Anything extended from UnityEngine.Object."
    },
    "Feeder.ReflectorNet.Model.SerializedMember": {
      "type": "object",
      "properties": {
        "typeName": {
          "type": "string",
          "description": "Full type name. Eg: 'System.String', 'System.Int32', 'UnityEngine.Vector3', etc."
        },
        "name": {
          "type": "string",
          "description": "Object name."
        },
        "value": {
          "description": "Value of the object, serialized as a non stringified JSON element. Can be null if the value is not set. Can be default value if the value is an empty object or array json."
        },
        "fields": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/Feeder.ReflectorNet.Model.SerializedMember",
            "description": "Nested field value."
          },
          "description": "Fields of the object, serialized as a list of 'SerializedMember'."
        },
        "props": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/Feeder.ReflectorNet.Model.SerializedMember",
            "description": "Nested property value."
          },
          "description": "Properties of the object, serialized as a list of 'SerializedMember'."
        }
      },
      "required": [
        "typeName"
      ],
      "additionalProperties": false
    },
    "Feeder.ReflectorNet.Model.SerializedMemberList": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/Feeder.ReflectorNet.Model.SerializedMember"
      }
    },
    "System.String-1": {
      "type": "array",
      "items": {
        "type": "string"
      }
    },
    "AIGD.ModifyObjectResponse": {
      "type": "object",
      "properties": {
        "Success": {
          "type": "boolean",
          "description": "Whether the modification was successful."
        },
        "Reference": {
          "$ref": "#/$defs/AIGD.ObjectRef",
          "description": "Reference to the modified object."
        },
        "Data": {
          "$ref": "#/$defs/Feeder.ReflectorNet.Model.SerializedMember",
          "description": "Updated object data after modification."
        },
        "Logs": {
          "$ref": "#/$defs/System.String-1",
          "description": "Log of modifications made and any warnings/errors encountered."
        }
      },
      "required": [
        "Success"
      ]
    }
  },
  "required": [
    "result"
  ]
}
```

