---
name: unity-skill-generate
description: "Regenerate every `SKILL.md` from the project's currently-registered MCP tools into the configured skills folder (or a project-relative override path). Writes the YAML `description:` from `[AiSkillDescription]` and the body from `[AiSkillBody]`."
---

# Skill (Tool) / Generate All

Generate all skills from the existed Tools in the Unity Project.

## Inputs

- `path` (optional) — project-relative skills folder (e.g. `.claude/skills`). Absolute paths and `..` traversal segments are rejected. When null/empty, the editor's configured `SkillsRootFolderAbsolutePath` is used.

## Behavior

Creates the destination folder if missing, then invokes `McpPluginInstance.GenerateSkillFiles(...)` to emit a `SKILL.md` per registered MCP tool. The plugin's `SkillsPath` is temporarily swapped to the target folder and restored in `finally` so the on-disk configuration is unchanged after the call returns.

## How to Call

Call this tool through the MCP client connected to the local Matrix AI Connector server.

Example input:
```json
{
  "path": "value"
}
```

> For complex input, keep the payload as valid JSON and send it through your connected MCP client.

Read the /feeder-mcp-initial-setup skill for local connection setup.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `path` | `string` | No | Path to the skills folder. If null or empty, the default path will be used. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "path": {
      "type": "string"
    }
  }
}
```

## Output

This tool does not return structured output.

