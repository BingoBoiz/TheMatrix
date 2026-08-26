---
name: console-clear-logs
description: Clear the MCP log cache (used by 'console-get-logs') and the Unity Editor Console window. Useful for isolating logs to a specific action by clearing the slate first.
---

# Console / Clear Logs

Clears the MCP log cache (used by console-get-logs) and the Unity Editor Console window. Useful for isolating errors related to a specific action by clearing logs before performing the action.

## Behavior

Calls `Debug.ClearDeveloperConsole()` to wipe the Editor Console, then clears the MCP-side `LogCollector` cache so subsequent 'console-get-logs' calls only see new entries.

## How to Call

Call this tool through the MCP client connected to the local Matrix AI Connector server.

Example input:
```json
{
  "nothing": "value"
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

This tool does not return structured output.

