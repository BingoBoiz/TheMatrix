# Feeder Bridge Protocol v1 (FBP/1)

Status: Draft accepted for implementation — 2026-07-10

FBP is the private protocol between the **Feeder Unity Adapter** (inside the editor) and the **Feeder Local Bridge**. It is *not* MCP: MCP terminates at the bridge; FBP is our own contract, versioned independently of the legacy upstream SignalR hub contract (which we deliberately do not clone).

- Transport: SignalR over WebSocket only, `http://127.0.0.1:<port>/fbp`.
- Wire format: MessagePack (SignalR MessagePack hub protocol).
- All messages share one envelope; binary payloads (screenshots, files) travel as raw `byte[]` fields — never base64 inside FBP.

## 1. Envelope

Every message carries:

| Field | Type | Notes |
|---|---|---|
| `protocolVersion` | string | `"1"`. Mismatch → `bridge.error` `FBP_VERSION_UNSUPPORTED` and disconnect. |
| `projectId` | string | Stable SHA-256/16 hex of normalized project path. |
| `unityInstanceId` | string | GUID per editor process (survives domain reload, not editor restart). |
| `sessionId` | string | Bridge-assigned link session (changes on reconnect). |
| `requestId` | string | Unique per request; responses echo it. |
| `traceId` | string | W3C trace-id for cross-process correlation. |
| `messageType` | string | See §2. |
| `deadlineUnixMs` | long? | Absolute deadline; 0/null = bridge default. |
| `payload` | msgpack map / bin | Message-specific body. |

## 2. Message types

| Type | Direction | Purpose |
|---|---|---|
| `unity.register` | U→B | Handshake (§3). Response: `sessionId`, accepted protocol version, resume info. |
| `unity.unregister` | U→B | Graceful shutdown (editor closing, not domain reload). |
| `registry.snapshot` | U→B | Full tool/prompt/resource registry + `registryHash`. Sent when hash differs from bridge's cached hash. |
| `registry.changed` | U→B | Delta (added/removed/updated entries) + new hash. |
| `tool.invoke` | B→U | Invoke tool: name, args (UTF-8 JSON bytes), `operationId`, deadline. |
| `tool.result` | U→B | Terminal result for `operationId`: success payload or error. May arrive on a *different* connection than the invoke (domain reload). |
| `tool.progress` | U→B | Progress notification for a running `operationId`. |
| `tool.cancel` | B→U | Cancel a queued/running `operationId`. |
| `resource.read` | B→U | Read resource by URI. Response is a `tool.result`-shaped completion. |
| `prompt.get` | B→U | Resolve prompt by name/args. |
| `heartbeat` | both | Liveness + editor state (`ready` / `compiling` / `reloading` / `playing` / `paused`). Interval 5 s, miss ×3 → link considered dead. |
| `bridge.error` | B→U | Protocol-level error (bad envelope, auth failure, backpressure). |

## 3. `unity.register` handshake payload

Unity MUST send:

- `projectPathHash` (= `projectId` derivation input check)
- `unityVersion` (e.g. `6000.0.32f1`)
- `pluginVersion`
- `supportedProtocolVersions` (array — bridge picks highest common)
- `toolRegistryHash` (bridge replies `registryUpToDate: true|false`; `false` triggers `registry.snapshot`)
- `authToken` (per-project 256-bit token, see ADR-0005)
- `editorState` (`ready` / `compiling` / `playing` / `reloading`)
- `pendingOperations` (operationIds it can still complete after a reload)

Bridge replies with `sessionId`, chosen protocol version, list of `operationId`s it still awaits (so Unity can resurrect or fail them), and scheduler config (queue capacity, heartbeat interval).

## 4. Error model

Payload of `bridge.error` and the error branch of `tool.result`:

| Field | Notes |
|---|---|
| `code` | Stable string: `FBP_VERSION_UNSUPPORTED`, `FBP_AUTH_FAILED`, `FBP_QUEUE_FULL`, `FBP_DEADLINE_EXCEEDED`, `FBP_CANCELLED`, `FBP_TOOL_NOT_FOUND`, `FBP_TOOL_FAILED`, `FBP_UNITY_UNAVAILABLE`, `FBP_INTERNAL` |
| `message` | Human-readable, no secrets |
| `retriable` | bool |
| `detailsJson` | Optional UTF-8 JSON bytes (tool stack traces etc.; stripped in release for MCP clients, kept in local logs) |

MCP mapping: `FBP_QUEUE_FULL` → JSON-RPC `-32000` server busy; `FBP_DEADLINE_EXCEEDED`/`FBP_CANCELLED` → request cancellation semantics; `FBP_TOOL_*` → `tools/call` result with `isError: true` (parity with current server); envelope-level failures → JSON-RPC error objects.

## 5. Link state machine (bridge view of a Unity instance)

```
        register ok                    heartbeat miss ×3 / ws close
Idle ──────────────► Ready ──────────────────────────────► Degraded
                      │ ▲                                      │
     editorState:     │ │ editorState: ready                   │ re-register within TTL (120 s)
     compiling/reload ▼ │                                      ▼
                     Busy ◄──────────────────────────── Resumed (replay pending op completions)
                                                               │ TTL expired
                                                               ▼
                                                          Evicted (pending ops → FBP_UNITY_UNAVAILABLE)
```

- `Busy` (compiling/reloading): bridge keeps accepting MCP requests, queues them (bounded), and answers `tools/list` from cache.
- `Degraded`: requests queue until TTL; new mutating requests fail fast with `FBP_UNITY_UNAVAILABLE` after 10 s.
- `Resumed`: same `unityInstanceId` re-registered; bridge re-delivers `tool.cancel`s and receives late `tool.result`s.

## 6. Versioning rules

- Additive fields: allowed within v1 (MessagePack maps, unknown keys ignored).
- Semantic/breaking changes: bump `protocolVersion`; bridge supports current and previous major for one release cycle.
