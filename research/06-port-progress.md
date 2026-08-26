# 06 — Port plugin DLLs → source (đang triển khai)

> Tiếp nối `05-dll-analysis.md`. Chiến lược The Architect đã duyệt: A+B kết hợp, phạm vi 3 DLL plugin.

## Tiến độ

| Bước | Trạng thái |
|---|---|
| Decompile 3 DLL bằng ilspycmd 11.0 | ✅ `research/decompiled/` — 293 file, ~22.7k LOC |
| Clone upstream (Unity-MCP, ReflectorNet, MCP-Plugin-dotnet) | ✅ `research/upstream-src/` (dùng làm reference) |
| Port vào package + convert file-scoped ns → block (C# 9) | ✅ `Runtime/Plugins/Feeder.Source/` — 293 file, 290 converted |
| Viết 3 asmdef (overrideReferences=false) | ✅ (mỗi asmdef trong folder code của nó — Unity yêu cầu 1 asmdef/folder) |
| Verify compile bằng dotnet (netstandard2.1, LangVersion 9) | ✅ **BUILD SUCCEEDED** — sau khi sửa: 1 primary ctor (LruCache), 8× `base._002Ector` (decompiler artifact), 2× enum `(int)` cast, + refs Http.Connections.Common/Bcl.AsyncInterfaces |
| Cập nhật Feeder.MCP.Runtime/Editor.asmdef (bỏ DLL refs, thêm asmdef refs) | ✅ |
| **Xóa 3 DLL + .meta** | ✅ (git đã track → restore được: `git checkout`) |
| Unity compile (assets-refresh, domain reload) | ✅ **errors=False** (23:58:12) — `Library/ScriptAssemblies/` có `Feeder.ReflectorNet.dll` (270KB), `Feeder.McpPlugin.dll` (319KB), `Feeder.McpPlugin.Common.dll` (73KB) biên dịch từ source |
| Test toàn bộ tool flow + skills regenerate | ✅ **78 tools nguyên vẹn**, Scene đọc OK, script-execute OK, **skills regenerate OK từ source SkillFileGenerator**, .agents/.claude vẫn 0 diff, DeepSeek configurator compile OK |
| Bonus: fix `/hub/mcp-server` 404 + skill generator placeholder | ✅ **CẢ 2 ĐÃ XONG** (round 1): |
| 1. `/hub/mcp-server` | `McpManagerClientHub.GetMcpServerData()` → try hub, catch → **fallback GET `/healthz`** (map version/apiVersion/transport/IsAiAgentConnected từ unityLinks). Đã xác minh method `FetchServerDataFromHealthzAsync` tồn tại trong assembly compile từ source |
| 2. SkillFileGenerator | `CreateExampleValue` → xử lý `const`/`enum`/`$ref`/`format`/`Sample:` trong description; `$ref` ref-type → `{"instanceID": 0}`, List → `[]`; fallback `"value"` thay `"string_value"`. Verify: **0 file còn `string_value`**, ví dụ thật (`gameObjectRef: {instanceID:0}`, `descriptionMode: "Include"`, `paths: []`), .agents/.claude 0 diff, tên hợp lệ |

## Fixes decompiler artifacts (đã áp dụng, ghi để tái lập)
- `LruCache.cs` — primary constructor `CacheItem(TKey,TValue)` (C#12) → struct + ctor thường.
- 8× `base._002Ector(...)` — ilspy đặt lời gọi base ctor sai chỗ; xóa (base() ngầm) hoặc chuyển `: base(hubConnection)` (HubConnectionLogger).
- `ConnectionManager.cs` ×2 — `state - 1 <= 1` (enum − int) → `(int)state - 1 <= 1`.
- Thêm reference: `Microsoft.AspNetCore.Http.Connections.Common.dll`, `Microsoft.Bcl.AsyncInterfaces.dll` (+ transitive SignalR set) — với asmdef overrideReferences=false thì Unity tự resolve.

## Cấu trúc cuối
```
Runtime/Plugins/Feeder.Source/
  Feeder.ReflectorNet/Feeder.ReflectorNet.asmdef + 100 .cs
  Feeder.McpPlugin.Common/Feeder.McpPlugin.Common.asmdef + 65 .cs
  Feeder.McpPlugin/Feeder.McpPlugin.asmdef + 128 .cs
```
- Feeder.MCP.Runtime.asmdef / Feeder.MCP.Editor.asmdef: `references` thêm 3 asmdef, bỏ 3 DLL khỏi precompiledReferences.
- NuGet DLLs giữ nguyên tại `Runtime/Plugins/NuGet/`.
- Rollback nếu cần: `git checkout -- Packages/com.feeder.mcp/Runtime/Plugins/Feeder/*.dll*` + xóa Feeder.Source.
