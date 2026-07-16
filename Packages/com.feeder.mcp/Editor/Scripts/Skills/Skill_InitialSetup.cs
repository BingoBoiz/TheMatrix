#nullable enable
using Feeder.McpPlugin;

namespace Feeder.MCP.Editor.API
{
    [AiSkillType]
    public static class Skill_InitialSetup
    {
        public const string SkillId = "feeder-mcp-initial-setup";

        [AiSkill(SkillId,
@"Provides the local setup checklist for the Matrix AI Connector package.")]
        public static string Markdown => @"
# Matrix AI Connector - Initial Setup

This project has the standalone `com.feeder.mcp` package installed. The package includes the local MCP server and does not require an additional package registry.

## Open The Connector

1. Open `Tools/Feeder/Matrix AI Connector`.
2. Use `Custom` connection mode for local work.
3. Keep HTTP transport selected unless your AI client only supports stdio.
4. Start the local MCP server from the connector if it is not already running.

## Configure Your AI Client

1. Select your AI client in the `AI agent` dropdown.
2. Copy or apply the generated MCP configuration shown by the connector.
3. Match the timeout value in the client configuration to the connector timeout.
4. If authorization is enabled, copy the generated token into the client configuration.

## Generate Skills

Enable skill generation in the connector when you want project-specific skill files. Refresh the generated skills after enabling or disabling tools, prompts, or resources.

## Troubleshooting

- If the AI client cannot connect, confirm the local MCP server is running and the port matches the generated config.
- If a tool times out, increase the connector timeout and update the AI client config to the same value.
- If authorization fails, generate a new token and update the AI client config.
";
    }
}
