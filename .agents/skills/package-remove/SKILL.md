---
name: package-remove
description: Uninstall a UPM package from the Unity project. Modifies `manifest.json` and may trigger a domain reload — the final result is delivered after the reload via the request's `requestId`. Built-in packages and packages that are dependencies of others cannot be removed. Use 'package-list' to list installed packages first.
---

# Package Manager / Remove

Remove (uninstall) a package from the Unity project. This removes the package from the project's manifest.json and triggers package resolution. Note: Built-in packages and packages that are dependencies of other installed packages cannot be removed. Note: Package removal may trigger a domain reload. The result will be sent after the reload completes. Use 'package-list' tool to list installed packages first.

## Inputs

- `packageId` — package name (e.g. `com.unity.textmeshpro`). A trailing `@<version>` is stripped automatically before being passed to `Client.Remove`, so accidental versioned IDs still work.

## Behavior

First verifies the package is installed via an offline `Client.List` — returns a clear `PackageNotFound` error if not. On removal failure, surfaces Unity's error message. On success, schedules a post-domain-reload notification so the response is delivered after Unity finishes the resolution.

## How to Call

Call this tool through the MCP client connected to the local Matrix AI Connector server.

Example input:
```json
{
  "packageId": "string_value"
}
```

> For complex input, keep the payload as valid JSON and send it through your connected MCP client.

Read the /feeder-mcp-initial-setup skill for local connection setup.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `packageId` | `string` | Yes | The ID of the package to remove. Example: 'com.unity.textmeshpro'. Do not include version number. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "packageId": {
      "type": "string"
    }
  },
  "required": [
    "packageId"
  ]
}
```

## Output

This tool does not return structured output.

