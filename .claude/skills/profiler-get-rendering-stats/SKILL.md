---
name: profiler-get-rendering-stats
description: Return current frame timing, FPS, vsync, target frame rate, threading mode, and graphics device type from Unity Time / QualitySettings / SystemInfo.
---

# Profiler / Get Rendering Stats

Snapshots rendering-related fields from `UnityEngine.Time`, `UnityEngine.QualitySettings`, `UnityEngine.Application` and `UnityEngine.SystemInfo`. All values are read from built-in Unity APIs so no external Unity package is required.

## Fields

- `FrameTimeMs` — `Time.deltaTime * 1000f`.
- `Fps` — `1 / Time.deltaTime` (0 when delta is zero).
- `VSyncCount` — `QualitySettings.vSyncCount`.
- `TargetFrameRate` — `Application.targetFrameRate`.
- `RenderingThreadingMode` — `SystemInfo.renderingThreadingMode.ToString()`.
- `GraphicsDeviceType` — `SystemInfo.graphicsDeviceType.ToString()`.

## How to Call

Call this tool through the MCP client connected to the local Matrix AI Connector server.

Example input:
```json
{
  "nothing": "string_value"
}
```

> For complex input, keep the payload as valid JSON and send it through your connected MCP client.

Read the /feeder-mcp-initial-setup skill for local connection setup.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `nothing` | `string` | No |  |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "nothing": {
      "type": "string"
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
      "$ref": "#/$defs/Feeder.MCP.Editor.API.Tool_Profiler-RenderingStatsData",
      "description": "Rendering statistics from Unity's Time / QualitySettings / SystemInfo. No external package required."
    }
  },
  "$defs": {
    "Feeder.MCP.Editor.API.Tool_Profiler-RenderingStatsData": {
      "type": "object",
      "properties": {
        "FrameTimeMs": {
          "type": "number",
          "description": "Last reported frame time (Time.deltaTime * 1000) in milliseconds."
        },
        "Fps": {
          "type": "number",
          "description": "Frames per second derived from Time.deltaTime."
        },
        "VSyncCount": {
          "type": "integer",
          "description": "QualitySettings.vSyncCount."
        },
        "TargetFrameRate": {
          "type": "integer",
          "description": "Application.targetFrameRate."
        },
        "RenderingThreadingMode": {
          "type": "string",
          "description": "SystemInfo.renderingThreadingMode."
        },
        "GraphicsDeviceType": {
          "type": "string",
          "description": "SystemInfo.graphicsDeviceType."
        }
      },
      "required": [
        "FrameTimeMs",
        "Fps",
        "VSyncCount",
        "TargetFrameRate"
      ],
      "description": "Rendering statistics from Unity's Time / QualitySettings / SystemInfo. No external package required."
    }
  },
  "required": [
    "result"
  ]
}
```

