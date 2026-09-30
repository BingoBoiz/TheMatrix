# Changelog

All notable changes to this package are documented here.

## [0.87.1] - 2026-09-30

### Fixed

- Matrix Bridge and Matrix Space keep their font and rain after you press Play or open another scene.
- Matrix Bridge no longer floods the console with `MissingReferenceException`, and on Unity 6 its dock
  area switches tabs instead of throwing `InvalidOperationException`.
- An editor that already hit these errors recovers on the next script reload, with no restart.
- The Matrix rain no longer leaks a font and a material on every script reload.

## [0.87.0] - 2026-09-29

### Added

- Matrix Bridge window: one screen with a Matrix rain background that shows the bridge state, one
  click to link or unlink, and one click on a client name to wire or unwire its MCP configuration.
- **Rain** item in the Matrix Bridge `...` menu turns the rain background off for machines where it
  lags. It is the same setting as the Matrix Space rain toggle, so both windows follow it.

### Changed

- Matrix is now opt-in for each machine and project. Installing or updating the package no longer
  starts the server, links the editor, writes `.mcp.json` or skill files, or shows the Scene view
  toolbar, and Matrix windows or the toolbar restored from a saved layout close themselves. Turn it
  on with **Tools > Feeder > Matrix Setup**, or by clicking the state word in Matrix Bridge; click
  the state word again to turn it off. Existing installs turn it on once.
- The main window is now Matrix Bridge (**Tools > Feeder > Matrix Bridge**).
- Every connection is local: the bridge listens on loopback only, and a host outside loopback in the
  config is reset to the local default.
- Transport, authorization and timeout are set in `UserSettings/Feeder-MCP-Config.json` instead of
  the window.
- Individual tools are enabled or disabled in the same file or with the `tool-set-enabled-state`
  tool; the window no longer lists them.

### Removed

- Cloud connection mode and its authorization flow.
- The Tools, Prompts and Resources windows, and the built-in prompts and resource.

## [0.86.0] - 2026-09-29

- Added **Tools > Feeder > Matrix Setup**, a per-machine setup: it adds `.mcp.json` to the project's
  `.gitignore`, keeps only the core MCP tools enabled to cut the tokens sent with each request, and
  writes the Claude Code MCP config and installs or verifies the Claude Code CLI.
- Documented that `.mcp.json` holds a machine-specific port and should be gitignored, not committed.

## [0.85.0] - 2026-07-17

- Added zero-setup auto-configure: on editor load the package writes the MCP config file for
  enabled agents (default: Claude Code's project-root `.mcp.json`) and generates skill files,
  so a freshly installed package works without opening a window.
- Auto-configure self-heals stale ports when the project is cloned or moved to a new path
  (the server port derives from the project directory).
- Added a per-agent "Auto-configure on Unity load" toggle to the MCP status row;
  skill auto-generation now defaults to enabled for Claude Code.
- Auto-configure is skipped in batch mode, CI, and Cloud connection mode, and never blocks
  editor load on failure.
- Removed the external download/tutorial links from the agent configurator UI.

## [0.84.0] - 2026-07-16

- Replaced the bundled MCP server with the Feeder MCP Server 0.2.0.
- Added the FMP/1 Unity adapter while keeping the existing Unity tool registry and implementations.
- Preserved Streamable HTTP and stdio client compatibility in one self-contained executable.
- Preserved deferred tool completion across Unity compilation and domain reloads.
- Added loopback-only binding and optional bearer-token authentication for MCP and Unity links.
- Retained the historical executable filename only as a compatibility path for existing client configs.
- Compressed the self-contained server payload for Git distribution and added automatic, safe
  extraction into each consuming project's `Library/mcp-server` cache without requiring Git LFS.

## [0.83.0] - 2026-07-16

- Moved the package distribution to the standalone TheMatrix repository.
- Added Git URL installation documentation and release metadata.
- Replaced the external preferences package dependency with package-local typed preference wrappers
  that preserve existing preference keys.
- Moved the Feeder and NuGet managed DLLs used by the package out of FeederBase's `Assets`
  folder and into the distributable package.
- Added the Matrix persona as a built-in skill that can be generated for supported AI clients.
- Kept the Windows x64 local MCP server payload bundled with the package.

## [0.82.4]

- Initial standalone embedded package imported from FeederBase.
