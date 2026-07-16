---
name: package-list
description: List all UPM packages installed in the Unity project — name, version, source, description. Optionally filter by source (registry, embedded, local, git, built-in, local tarball), by name/display/description substring, and by direct-dependency-only.
---

# Package Manager / List Installed

List all packages installed in the Unity project (UPM packages). Returns information about each installed package including name, version, source, and description. Use this to check which packages are currently installed before adding or removing packages.

## Inputs

- `sourceFilter` (default `All`) — restrict by Unity `PackageSource`: `All`, `Registry`, `Embedded`, `Local`, `Git`, `BuiltIn`, `LocalTarball`.
- `nameFilter` (optional) — case-insensitive substring filter over name / displayName / description. Results are prioritized: exact name → exact displayName → name substring → displayName substring → description substring.
- `directDependenciesOnly` (default `false`) — when true, return only packages listed in `manifest.json` (no transitive dependencies).

## How to Call

Call this tool through the MCP client connected to the local Matrix AI Connector server.

Example input:
```json
{
  "sourceFilter": "string_value",
  "nameFilter": "string_value",
  "directDependenciesOnly": false
}
```

> For complex input, keep the payload as valid JSON and send it through your connected MCP client.

Read the /feeder-mcp-initial-setup skill for local connection setup.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `sourceFilter` | `string` | No | Filter packages by source. |
| `nameFilter` | `string` | No | Filter packages by name, display name, or description (case-insensitive). Results are prioritized: exact name match, exact display name match, name substring, display name substring, description substring. |
| `directDependenciesOnly` | `boolean` | No | Include only direct dependencies (packages in manifest.json). If false, includes all resolved packages. Default: false |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "sourceFilter": {
      "type": "string",
      "enum": [
        "All",
        "Registry",
        "Embedded",
        "Local",
        "Git",
        "BuiltIn",
        "LocalTarball"
      ]
    },
    "nameFilter": {
      "type": "string"
    },
    "directDependenciesOnly": {
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
      "$ref": "#/$defs/System.Collections.Generic.List(AIGD.PackageData)"
    }
  },
  "$defs": {
    "AIGD.PackageData": {
      "type": "object",
      "properties": {
        "Name": {
          "type": "string",
          "description": "The official Unity name of the package used as the package ID."
        },
        "DisplayName": {
          "type": "string",
          "description": "The display name of the package."
        },
        "Version": {
          "type": "string",
          "description": "The version of the package."
        },
        "Description": {
          "type": "string",
          "description": "A brief description of the package."
        },
        "Source": {
          "type": "string",
          "description": "The source of the package (Registry, Embedded, Local, Git, etc.)."
        },
        "Category": {
          "type": "string",
          "description": "The category of the package."
        }
      },
      "description": "Package information returned from package list operation."
    },
    "System.Collections.Generic.List(AIGD.PackageData)": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/AIGD.PackageData",
        "description": "Package information returned from package list operation."
      }
    }
  },
  "required": [
    "result"
  ]
}
```

