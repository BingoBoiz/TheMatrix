---
name: editor-application-set-state
description: Start / stop / pause the Unity Editor 'playmode'. Use 'editor-application-get-state' to inspect the current state first. Throws if the project currently has compilation errors.
---

# Editor / Application / Set State

Control the Unity Editor application state. You can start, stop, or pause the 'playmode'. Use 'editor-application-get-state' tool to get the current state first.

## Inputs

- `isPlaying` (default `false`) — sets `EditorApplication.isPlaying`.
- `isPaused` (default `false`) — sets `EditorApplication.isPaused`.

## Behavior

Refuses any state change while `EditorUtility.scriptCompilationFailed` is true — instead throws with the compilation error details so the caller can fix them first. On success returns the post-change `EditorStatsData` snapshot.

## How to Call

Call this tool through the MCP client connected to the local Matrix AI Connector server.

Example input:
```json
{
  "isPlaying": false,
  "isPaused": false
}
```

> For complex input, keep the payload as valid JSON and send it through your connected MCP client.

Read the /feeder-mcp-initial-setup skill for local connection setup.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `isPlaying` | `boolean` | No | If true, the 'playmode' will be started. If false, the 'playmode' will be stopped. |
| `isPaused` | `boolean` | No | If true, the 'playmode' will be paused. If false, the 'playmode' will be resumed. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "isPlaying": {
      "type": "boolean"
    },
    "isPaused": {
      "type": "boolean"
    }
  }
}
```

## Output

### Output JSON Schema

```json
{
  "type": "object",
  "properties": {
    "result": {
      "$ref": "#/$defs/AIGD.EditorStatsData",
      "description": "Available information about 'UnityEditor.EditorApplication'."
    }
  },
  "$defs": {
    "AIGD.EditorStatsData": {
      "type": "object",
      "properties": {
        "IsPlaying": {
          "type": "boolean",
          "description": "Whether the Editor is in Play mode."
        },
        "IsPaused": {
          "type": "boolean",
          "description": "Whether the Editor is paused."
        },
        "IsCompiling": {
          "type": "boolean",
          "description": "Is editor currently compiling scripts? (Read Only)"
        },
        "IsPlayingOrWillChangePlaymode": {
          "type": "boolean",
          "description": "Editor application state which is true only when the Editor is currently in or about to enter Play mode. (Read Only)"
        },
        "IsUpdating": {
          "type": "boolean",
          "description": "True if the Editor is currently refreshing the AssetDatabase. (Read Only)"
        },
        "ApplicationContentsPath": {
          "type": "string",
          "description": "Path to the Unity editor contents folder. (Read Only)"
        },
        "ApplicationPath": {
          "type": "string",
          "description": "Gets the path to the Unity Editor application. (Read Only)"
        },
        "TimeSinceStartup": {
          "type": "number",
          "description": "The time since the editor was started. (Read Only)"
        }
      },
      "required": [
        "IsPlaying",
        "IsPaused",
        "IsCompiling",
        "IsPlayingOrWillChangePlaymode",
        "IsUpdating",
        "TimeSinceStartup"
      ],
      "description": "Available information about 'UnityEditor.EditorApplication'."
    }
  },
  "required": [
    "result"
  ]
}
```

