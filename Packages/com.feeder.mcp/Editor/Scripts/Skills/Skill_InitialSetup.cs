#nullable enable
using Feeder.McpPlugin;

namespace Feeder.MCP.Editor.API
{
    [AiSkillType]
    public static class Skill_InitialSetup
    {
        public const string SkillId = "feeder-mcp-initial-setup";

        [AiSkill(SkillId,
@"Provides the local setup checklist for the Matrix Bridge package.")]
        public static string Markdown => @"
# Matrix Bridge - Initial Setup

This project has the standalone `com.feeder.mcp` package installed. The package includes the local MCP server and does not require an additional package registry.

## Open The Bridge

1. Open `Tools/Feeder/Matrix Bridge`.
2. Click the large state word until it reads ONLINE. It cycles OFFLINE, LINKING, ONLINE; clicking it again unlinks.
3. The address under the state word is the local endpoint. Every connection stays on this machine (loopback only).

## Wire Your AI Client

1. Click the client name at the bottom of the window (claude, codex, cursor, deepseek, gemini). The `+N` entry lists more clients.
2. A dot under the name means the client configuration was written. An amber dot means it is outdated: click the name again to rewrite it.
3. Restart the AI client so it reads the new configuration.

## Generate Skills

Wiring a client that supports skills also generates its skill files, and they are refreshed on every editor load. Run `unity-skill-generate` to refresh them after changing which tools are enabled.

## Troubleshooting

- If the AI client cannot connect, confirm the window reads ONLINE and the port in the client configuration matches the address shown.
- To change the port: unlink, double-click the address, type the new port, press Enter, then wire the client again.
- The `...` menu at the top right opens the logs and the config file, sets the logging level, and reinstalls the server.
";
    }
}
