# Changelog

All notable changes to this package are documented here.

## [0.84.0] - 2026-07-16

- Replaced the bundled third-party GameDev MCP server with the Feeder MCP Server 0.2.0.
- Added the FBP/1 Unity adapter while keeping the existing Unity tool registry and implementations.
- Preserved Streamable HTTP and stdio client compatibility in one self-contained executable.
- Preserved deferred tool completion across Unity compilation and domain reloads.
- Added loopback-only binding and optional bearer-token authentication for MCP and Unity links.
- Retained the historical executable filename only as a compatibility path for existing client configs.
- Compressed the self-contained server payload for Git distribution and added automatic, safe
  extraction into each consuming project's `Library/mcp-server` cache without requiring Git LFS.

## [0.83.0] - 2026-07-16

- Moved Matrix AI Connector distribution to the standalone TheMatrix repository.
- Added Git URL installation documentation and release metadata.
- Removed the OpenUPM PlayerPrefsEx dependency and replaced it with package-local typed
  preference wrappers that preserve existing preference keys.
- Moved the Feeder and NuGet managed DLLs used by the package out of FeederBase's `Assets`
  folder and into the distributable package.
- Added the Matrix persona as a built-in skill that can be generated for supported AI clients.
- Kept the Windows x64 local MCP server payload bundled with the package.

## [0.82.4]

- Initial standalone embedded package imported from FeederBase.
