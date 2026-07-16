# ADR-0001: Runtime and technology stack

Status: Accepted — 2026-07-10

## Context

Feeder Local Bridge replaces the legacy upstream `gamedev-mcp-server.exe` (101 MB, binds `0.0.0.0`) with a bridge we own, running fully local with lower latency. This phase swaps the server only; the Unity-side tool implementations stay as-is.

## Decision

| Component | Choice | Notes |
|---|---|---|
| Bridge runtime | .NET 10 LTS (`net10.0`) | LTS until Nov 2028. SDK 10.0.301 pinned via `global.json`. |
| MCP implementation | Official MCP C# SDK (`ModelContextProtocol.*`) | Version pinned in `Directory.Packages.props`; NuGet lock files committed. |
| MCP HTTP transport | Streamable HTTP on `127.0.0.1` only | Never `0.0.0.0` (fixes upstream exposure). |
| MCP stdio transport | `Feeder.StdioShim` — tiny forwarder process | Newline-delimited JSON-RPC on stdio, forwards to the persistent bridge; zero business logic. |
| Unity ↔ Bridge link | SignalR, WebSocket-only (no negotiate fallback, no long polling) | Keeps reconnect/RPC abstraction the Unity plugin already uses. |
| Internal serialization | MessagePack on the Unity link | Smaller frames, no base64 for binary payloads. |
| MCP edge serialization | System.Text.Json, UTF-8, source-generated contexts on hot paths | |
| Logging | stderr + rolling file under `Library/FeederBridge/logs` | **No byte ever written to MCP stdout other than JSON-RPC.** |
| Distribution (win-x64) | Self-contained; Native AOT evaluated after Phase 9 benchmarks | AOT adopted only if MCP SDK + SignalR test suites pass, startup/memory improve measurably, and no reflection-based feature is lost. |
| macOS / Linux | Deferred | Layout keeps RID-specific publish dirs. |

## Why an out-of-process bridge

Unity domain reload kills any in-editor endpoint mid-request. A persistent external process keeps MCP sessions (and AI clients) alive across compile/reload/play-mode transitions; Unity reconnects to the bridge, not the other way around.

## Consequences

- Two processes to ship (bridge + shim) plus the Unity adapter.
- Protocol between Unity and bridge is ours to version (see [feeder-bridge-protocol-v1.md](../protocol/feeder-bridge-protocol-v1.md)).
- Replacing the `Feeder.McpPlugin*` DLLs for full upstream independence is a later phase; the bridge must interoperate with the current plugin's tool registry semantics in the meantime.
