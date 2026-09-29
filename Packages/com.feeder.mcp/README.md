# Matrix Bridge

Matrix Bridge exposes Unity Editor tools to MCP-compatible AI
clients. The package ships with the local Windows x64 MCP server payload.

## Installation

In Unity 2022.3 or newer, open **Window > Package Manager**, choose
**Add package from git URL**, and use:

```text
https://github.com/BingoBoiz/TheMatrix.git?path=/Packages/com.feeder.mcp
```

The package includes its managed dependencies and compressed self-contained local server. On first
use, the editor extracts the server into the consuming project's `Library/mcp-server` cache. It does
not require Git LFS or a separate .NET runtime. Unity resolves the
official Unity Test Framework dependency from its standard registry.

For reproducible installs, use a released tag:

```text
https://github.com/BingoBoiz/TheMatrix.git?path=/Packages/com.feeder.mcp#v0.87.0
```

## Getting started

Installing the package changes nothing on a machine: no window opens, no server starts and no file
is written until the person using the project turns Matrix on. Teammates who do not use MCP are
never affected, even when the project is shared through version control.

To turn it on, run **Tools > Feeder > Matrix Setup** once (see [Matrix Setup](#matrix-setup)), or
open **Tools > Feeder > Matrix Bridge** and click the large state word. The choice is stored per
machine and per project folder, outside the project, so it is never committed. A saved window
layout that includes Matrix windows or the Scene view toolbar (for example one checked in with the
project) does not open them for someone who has not turned Matrix on.

Once Matrix is on, the package does this each time the editor loads:

1. Extracts and starts the bundled local MCP server.
2. Writes the project-root `.mcp.json` with the correct server URL (Claude Code picks it up on
   the next session in that folder).
3. Generates skill files into `.claude/skills`.

For other AI clients, open **Tools > Feeder > Matrix Bridge** and click the client name at the
bottom of the window to wire or unwire it. To turn Matrix off again, click the state word while it
reads ONLINE: the server stops, and nothing starts, opens or is written on the next load.

Note: the server port is derived from the project's directory path, so `.mcp.json` differs
between machines that clone the project to different paths. The file is rewritten with the local
port on editor load. Do not commit it: run **Tools > Feeder > Matrix Setup** once per machine to
gitignore it.

## Matrix Bridge window

Open **Tools > Feeder > Matrix Bridge**. Every connection is local: the bridge listens on loopback
only.

- Click the large state word to link or unlink (OFFLINE, LINKING, ONLINE).
- Click a client name to write or remove its MCP configuration. A dot under the name means it is
  wired; an amber dot means the configuration is outdated and a click rewrites it. The `+N` entry
  lists more clients.
- The `...` menu copies the endpoint, changes the port while offline, sets the logging level,
  turns the rain background on or off (the same setting as in Matrix Space, for machines where it
  lags), opens the logs and the config file, and reinstalls the server.

## Matrix Setup

Run **Tools > Feeder > Matrix Setup** once on each machine. It:

- turns Matrix on for the project on this machine: it starts the local server and links the editor;
- adds `.mcp.json` to the project's `.gitignore`; if the file is already committed, it prints
  `git rm --cached .mcp.json` for you to run;
- keeps only the core MCP tools enabled and disables the rest, to cut the tokens sent with every
  request; a client enables more on demand with `tool-set-enabled-state`;
- writes the Claude Code MCP config, then installs or verifies the Claude Code CLI (Node.js is
  installed with winget if missing).

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
