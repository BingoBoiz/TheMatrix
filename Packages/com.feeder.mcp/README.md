# Feeder MCP (Matrix AI Connector)

Matrix AI Connector exposes Unity Editor tools, prompts, and resources to MCP-compatible AI
clients. The package ships with the local Windows x64 MCP server payload.

## Installation

In Unity 2022.3 or newer, open **Window > Package Manager**, choose
**Add package from git URL**, and use:

```text
https://github.com/BingoBoiz/TheMatrix.git?path=/Packages/com.feeder.mcp
```

The package includes its managed dependencies and compressed self-contained local server. On first
use, the editor extracts the server into the consuming project's `Library/mcp-server` cache. It does
not require Git LFS, a separate .NET runtime, or an OpenUPM scoped registry. Unity resolves the
official Unity Test Framework dependency from its standard registry.

For reproducible installs, use a released tag:

```text
https://github.com/BingoBoiz/TheMatrix.git?path=/Packages/com.feeder.mcp#v0.85.0
```

## Getting started

Install the package and open the project once — that's it for Claude Code. On editor load the
package automatically:

1. Extracts and starts the bundled local MCP server.
2. Writes the project-root `.mcp.json` with the correct server URL (Claude Code picks it up on
   the next session in that folder).
3. Generates skill files into `.claude/skills`.

For other AI clients, or to opt out, open **Tools > Feeder > Matrix AI Connector**, select the
client, and use the **Configure** button or the **Auto-configure on Unity load** toggle.

Note: the server port is derived from the project's directory path, so `.mcp.json` differs
between machines that clone the project to different paths. This is expected — the file is
rewritten with the correct port on editor load, so it is safe to commit or gitignore it.

## Platform support

The package currently includes the local server binary for Windows x64. The Unity editor code
can be imported on other platforms, but starting the bundled server requires a matching payload
under `Server~/<platform>-<architecture>`.

The Windows payload is the Feeder MCP Server. Its on-disk filename remains
`gamedev-mcp-server.exe` solely so existing AI-client configurations continue to work without
being regenerated; the executable metadata, implementation, dependencies, and MCP server identity
are Feeder-owned. The Git package stores this payload as `server-payload.zip` to remain below
GitHub's per-file limit; installation and updates are handled automatically by the editor package.

## License

MIT. See `LICENSE.md`.
