# 05 — Phân tích Feeder.McpPlugin.dll & các DLL ẩn (để thay thế bằng source)

> Mục tiêu: The Architect muốn xóa các DLL (Feeder.McpPlugin.dll…) để toàn bộ logic thành source có thể đọc/sửa.
> Nội dung: DLL chứa gì (dump reflection từ Unity Editor đang chạy) + độ khó viết lại từ đầu + chiến lược khả thi.

## 1. Vị trí & danh tính

| DLL | Path | Version | Kích thước | Vai trò |
|---|---|---|---|---|
| `Feeder.McpPlugin.dll` | `Packages/com.feeder.mcp/Runtime/Plugins/Feeder/` | 6.10.0.0 | 279 KB | Plugin lõi: attributes, tool/prompt/resource manager, runner, SignalR, skills generator, AgentConfig |
| `Feeder.McpPlugin.Common.dll` | `Packages/com.feeder.mcp/Runtime/Plugins/Feeder/` | 6.10.0.0 | 57 KB | Consts (MCP/Server args/env/headers…), ~40 DTO Request/Response, hub interfaces, Version |
| `Feeder.ReflectorNet.dll` | `Packages/com.feeder.mcp/Runtime/Plugins/Feeder/` | 5.3.1.0 | 232 KB | Reflection engine: Reflector, MethodWrapper, SerializedMember, ViewQuery, ~45 JSON converters, JsonSchema, MainThread |

- Không có .pdb / nguồn trong package; git history chỉ có bản DLL nhị phân.
- **Server** (`gamedev-mcp-server.exe`, v0.2.1) KHÔNG nằm trong 3 DLL này — nó có **đầy đủ source** tại `Packages/com.feeder.mcp/Matrix~/src/Feeder.Matrix` (net10.0, MCP endpoint /mcp, FMP hub /fmp, /healthz, tests xunit, ADRs).

## 2. Nguồn gốc — đã xác minh là OPEN SOURCE (không phải code độc quyền)

- Package license: **MIT** (`Packages/com.feeder.mcp/LICENSE.md`).
- `Matrix~/Docs/adr/ADR-0001` ghi rõ: "Feeder Local Matrix replaces the legacy upstream `gamedev-mcp-server.exe`… This phase swaps the server only; the Unity-side tool implementations stay as-is."
- Web evidence (repo public):
  - [IvanMurzak/Unity-MCP](https://github.com/IvanMurzak/Unity-MCP) — project plugin Unity, quản lý "McpPlugin and ReflectorNet package versions" (0.5.0 / 2.4.0 upstream).
  - [IvanMurzak/ReflectorNet](https://github.com/IvanMurzak/ReflectorNet) — "Advanced .NET reflection toolkit for AI-driven scenarios. Dynamically invoke methods with JSON, generate schemas, serialize complex objects" → chính là `Feeder.ReflectorNet.dll` (fork đổi tên).
  - [IvanMurzak/GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server) — server cũ mà Feeder Matrix đã thay thế.
- Kết luận: `Feeder.*` = fork/rebrand của upstream IvanMurzak (MIT). Source khả dụng công khai.

## 3. Bản đồ module (dump reflection — `research/dll-surface.txt`)

### Feeder.McpPlugin.dll — 227 types (124 public)
| Module | Types | Ghi chú |
|---|---|---|
| **Attributes** | `AiTool`, `AiToolType`, `AiSkill`, `AiSkillDescription`, `AiSkillBody`, `AiSkillType`, `AiPrompt(.*)`, `AiResource(.*)`, `RequestID`, `McpPlugin*(.*)` (legacy) | Lớp annotation mà toàn bộ code tool đang dùng |
| **AgentConfig** | `AiAgentConfigurator` (abstract), `AgentConfiguratorSettings`, `JsonAiAgentConfig`, `TomlAiAgentConfig`, `ConfigurationSection/Item/ItemKind`, `ValueComparisonMode`, `ConfiguratorStatus`, `AiAgentConfiguratorRegistry` + **19 configurator** (ClaudeCode, Codex, Cline, Cursor, Gemini, GitHubCopilotCli, KiloCode, OpenCode, Rider, Antigravity, ClaudeDesktop, Custom, UnityAi, VS/VS Code Copilot, ZooCode…) | Module DeepSeek configurator kế thừa; DLL-side detection/status logic |
| **Core engine** | `McpPlugin`, `McpPluginBuilder` (63), `McpManager`, `McpToolManager`, `McpPromptManager`, `McpResourceManager`, `McpSystemToolManager`, `RunTool` (33), `RunPrompt`, `RunResource`, `ProxyTool`, `ToolRunnerCollection`, `ToolMethodData`… | Đăng ký/điều phối tool, argument binding, domain-reload |
| **Skills** | `Skills.SkillFileGenerator` (22), `ISkillFileGenerator`, `SkillContent` | **Chính là nơi sinh SKILL.md** (gồm placeholder `"string_value"` & `$defs` — đã xác định từ round trước) |
| **SignalR** | `BaseHubConnector`, `ConnectionManager`, `McpManagerClientHub`, `HubConnectionProvider/Observable`, `IConnection*`, `IMcpManager`… | **Chính là client `/hub/mcp-server` đang 404** (bug đã chẩn đoán round trước) |

### Feeder.McpPlugin.Common.dll — 87 types (83 public)
`Consts` (Command/ContentType/Hub/Log/MCP.Plugin.Args·Env/MCP.Server.Args·Env·Headers/AuthOption/TransportMethod/MimeType), ~40 DTO (`RequestCallTool`, `ResponseCallTool`, `ResponseListTool`, `McpClientData`, `McpServerData`, `ContentBlock`, `RequestNotification`, `VersionHandshake`, `ResponseStatus`, `Role`…), hub interfaces (Client/Server), `Version`, `ThreadSafeBool`, `Utils.DataArguments`, `JsonOptions`.

### Feeder.ReflectorNet.dll — 187 types (107 public)
`Reflector` (39 — TryReadAt/TryModifyAt/View/Serialize/Registry), `MethodWrapper` (reflection-method-*), `SerializedMember(List)`, `ViewQuery`, ~25 reflection converters + ~45 JSON converters (mọi kiểu CLR), `JsonSchema` (37), `JsonSerializer` (32), `MainThread` (14), `TypeUtils` (40), `TypeMemberUtils`, `LruCache`, logging utils.

## 4. Code package (visible) phụ thuộc những gì từ DLL

- **Attributes**: `[AiTool]`, `[AiToolType]`, `[AiSkillDescription]`, `[AiSkillBody]`, `[RequestID]`, `[JsonStringOrObject]`, `[Description]` — 397 file .cs trong `Editor/Scripts/API/**`.
- **AgentConfig**: `AiAgentConfigurator`, `AgentConfiguratorSettings`, `JsonAiAgentConfig`, `ValueComparisonMode`, `ConfigurationSection/Item`, `TroubleshootingSection`, `DefaultConfigurationSections`, `AiAgentConfiguratorRegistry`.
- **Consts**: `Feeder.McpPlugin.Common.Consts.MCP.Server.TransportMethod`…
- **Engine**: `UnityMcpPluginEditor.Instance.McpPluginInstance` (`IMcpPlugin`), `GenerateSkillFiles`, `RunTool`, `Tools/Prompts/Resources` managers.
- **ReflectorNet**: `Reflector`, `MainThread.Instance.Run`, `SerializedMember(List)`, `ViewQuery`, `PathPatch`, `JsonSchema` (type-get-json-schema).

→ Phần code tool THỰC THI (Editor/Scripts/API/Tool/*.cs, 78 tool) **đã là source** — không cần viết lại.

## 5. Độ khó viết lại từ đầu (nếu bắt buộc) — theo module

| Module | Độ khó | Rủi ro | Lý do |
|---|---|---|---|
| Attributes + DTO + Consts (~130 types) | Dễ | Thấp | Cơ học, không logic phức tạp |
| AgentConfig + 19 configurator | Trung bình | Trung bình | Logic detect/status (IsConfigured/ReconfigureNeeded, ValueComparisonMode) nhiều nhánh ẩn |
| McpPlugin core (RunTool, managers, builder, ProxyTool) | Khó | Cao | Argument binding, JSON qua ReflectorNet, cancellation, domain-reload |
| Skills.SkillFileGenerator | Trung bình | Trung bình | Format SKILL.md nhiều section; **nếu viết lại được thì sửa luôn placeholder** |
| SignalR (hub client, reconnect, retry) | Trung bình | Trung bình | NuGet SignalR 8.0 có sẵn; contract phải khớp server (fmp/mcp — **cơ hội fix /hub/mcp-server**) |
| **ReflectorNet** (engine + 45 converters + JsonSchema) | **Rất khó** | **Rất cao** | Engine reflection/serialization bespoke; hành vi tinh vi (path-scoped, view-query, type fallback, LruCache); KHÔNG có test phía Unity |

**Ước lượng tổng**: ~25.000–50.000 LOC, **1–3 tháng** cho 1 kỹ sư, **rủi ro regression cao** (không có test suite cho Unity-side; chỉ có test cho server trong Matrix~/tests). Yêu cầu "chạy y hệt như cũ" là không thực tế nếu viết lại từ đầu theo kiểu re-derive logic.

## 6. Khuyến nghị chiến lược (xếp theo ưu tiên)

1. **A — Lấy source upstream (nhanh & an toàn nhất)**: clone [IvanMurzak/Unity-MCP](https://github.com/IvanMurzak/Unity-MCP) + [IvanMurzak/ReflectorNet](https://github.com/IvanMurzak/ReflectorNet) → đối chiếu version với DLL 6.10.0.0 / 5.3.1.0 → vendor source vào package (asmdef), build → swap DLL bằng source → xóa DLL. Rủi ro: version fork Feeder có thể sai lệch nhỏ so với upstream (cần diff).
2. **B — Decompile 3 DLL (ILSpy/dnSpy)**: ra source gần 1:1 với DLL hiện tại (không phụ thuộc upstream có khớp version hay không) → vendor → build → xóa DLL. Luôn khả thi, bảo toàn hành vi 100%.
3. **C — Rewrite từ đầu**: chỉ nên nếu muốn thiết kế lại hoàn toàn (không yêu cầu "y hệt cũ") — tốn 1-3 tháng.

**Sau khi có source (A hoặc B), các việc đang chờ sẽ thành công được ngay**:
- Fix `/hub/mcp-server` 404 (căn chỉnh hub contract với server).
- Fix skill generator placeholder `"string_value"` + `$defs` phình to (đang chặn Phase 3 hoàn chỉnh).
- Sửa deep internals khác mà hiện không đụng được.

## 7. Files tham khảo
- `research/dll-surface.txt` — dump đầy đủ 320 dòng (type + member count).
- `Packages/com.feeder.mcp/LICENSE.md` (MIT), `Matrix~/README.md`, `Matrix~/Docs/adr/ADR-0001-runtime-and-stack.md`.
