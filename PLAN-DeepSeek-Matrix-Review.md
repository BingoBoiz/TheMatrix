# PLAN — Review tùy chọn DeepSeek trong Matrix AI Connector & Tối ưu Skills cho DeepSeek + Unity

- **Project**: `D:\Unity\TheMatrix`
- **Package**: `Packages/com.feeder.mcp` — Feeder MCP (Matrix AI Connector), v0.85.0, embedded package
- **Trạng thái**: Phase 0 ✅ · Phase 1 ✅ · Phase 2 ✅ (đã verify runtime) · Phase 3 ✅ (8 skill an toàn + 4 skill data-getting when-not) · Phase 4 ✅ (đã regenerate & verify) · Phase 5 ✅ (báo cáo + 2 quyết định sản phẩm đã duyệt & triển khai) · **Đã thực thi step-by-step hoàn tất**
- **Người duyệt**: The Architect (user)

---

## 1. Bối cảnh (Context)

Matrix AI Connector (`com.feeder.mcp`) là package Unity tích hợp sẵn một MCP server cục bộ (Feeder-MCP) để các AI agent điều khiển Unity Editor qua MCP. Package có màn hình cấu hình agent (dropdown) với nhiều agent khác nhau; **tùy chọn "DeepSeek"** là một agent được inject riêng bởi package:

- `Editor/Scripts/UI/AiAgentConfigurators/DeepSeekAiAgentConfigurator.cs` — định nghĩa agent DeepSeek (Id `deepseek`, skills path `.agents/skills`, viết `.mcp.json` dạng stdio/http).
- `Editor/Scripts/UI/AiAgentConfigurators/AiAgentCatalog.cs` — chèn DeepSeek ngay sau Claude Code trong dropdown.

**Skills** (SKILL.md) là thứ mà agent đọc để biết cách dùng từng MCP tool. Chúng được **sinh tự động** bởi tool `unity-skill-generate` từ các attribute `[AiSkillDescription]` / `[AiSkillBody]` gắn trên mỗi `[AiTool]` method trong source C# của package. Hiện có 2 bản sao trên đĩa:

- `.agents/skills/**` — DeepSeek Harness (DSH) đọc từ đây (chính là bộ skill trong session catalog hiện tại).
- `.claude/skills/**` — Claude Code đọc từ đây.

Tổng cộng ~166 file SKILL.md (2 folder), ngoài ra còn skill đặc biệt không sinh từ tool: `matrix` (persona) và `feeder-mcp-initial-setup` (checklist setup).

**Yêu cầu**: Review tùy chọn DeepSeek để đảm bảo nó cấu hình đúng/đầy đủ, và đảm bảo bộ skills được viết tốt nhất — đầy đủ nhất, chính xác nhất, tiết kiệm token nhất, phù hợp nhất với **model DeepSeek** (cách model đọc tool schema, function calling) và với **Unity** (API đúng, an toàn, đúng quy trình Editor).

---

## 2. Mục tiêu (Objectives)

1. **Review tùy chọn DeepSeek**: kiểm tra `DeepSeekAiAgentConfigurator` + `AiAgentCatalog` so với các agent dùng chung (Claude Code trong DLL `Feeder.McpPlugin.dll`) — đúng chuẩn `.mcp.json` (http/stdio), auth, URLs (DownloadUrl/TutorialUrl), skills path (`.agents/skills`), troubleshooting, icon, thứ tự dropdown.
2. **Review & nâng cấp skills**: bộ SKILL.md phải đạt chuẩn "best practice" cho LLM tool-calling (đặc biệt DeepSeek) và đúng/safe với Unity Editor.
3. **Nguyên tắc chỉnh sửa**: mọi nội dung skill phải sửa ở **source C#** (attribute `[AiSkillDescription]`/`[AiSkillBody]`/`[Description]`), sau đó regenerate bằng `unity-skill-generate` — **không sửa tay file SKILL.md** để tránh bị ghi đè & lệch 2 folder.

---

## 3. Hiện trạng đã xác minh (Grounding)

| Hạng mục | Giá trị |
|---|---|
| Package | `Packages/com.feeder.mcp`, version `0.85.0`, displayName "Feeder MCP (Matrix AI Connector)" |
| DeepSeek configurator | `Editor/Scripts/UI/AiAgentConfigurators/DeepSeekAiAgentConfigurator.cs` — `Id="deepseek"`, `AgentName="DeepSeek"`, `SkillsPath=".agents/skills"`, `IconName=""` (không icon), `DownloadUrl=https://www.deepseek.com/`, `TutorialUrl=https://api-docs.deepseek.com/` |
| Stdio args | `port`, `plugin-timeout`, `client-transport=stdio`, `authorization` — cố tình giống y hệt Claude Code |
| HTTP config | `type=http`, `url=settings.Host`, auth nếu `IsHttpAuthRequired` |
| Injection | `AiAgentCatalog.cs` — chèn DeepSeek ngay sau `claude-code` |
| Skills pipeline | `unity-skill-generate` (`API/SystemTool/Skills.Generate.cs`) → `McpPluginInstance.GenerateSkillFiles(...)`; path cấu hình qua `UnityMcpPluginEditor.SkillsPath` (persist trong `UserSettings/Feeder-MCP-Config.json`); skill output = 1 SKILL.md/tool (frontmatter `name`+`description` + body: Inputs, How to Call, Input/Output JSON Schema...) |
| 2 bản sao skills | `.agents/skills/**` (DSH) và `.claude/skills/**` (Claude Code) — ~166 SKILL.md tổng |
| Skill không sinh từ tool | `.agents/skills/matrix/SKILL.md` (persona), `.agents/skills/feeder-mcp-initial-setup/SKILL.md` (setup) |
| Config file | `UserSettings/Feeder-MCP-Config.json` (SkillsPath, token, tool states...) |

---

## 4. Phase 0 — Nghiên cứu (3 subagent song song, background)

> 3 subagent chạy độc lập, mỗi agent ghi report vào `research/` và tóm tắt ở message cuối.

### 4.1 Subagent A — Web: DeepSeek models/API + MCP + DeepSeek Harness
- **Nghiên cứu web**: model hiện tại của DeepSeek (`deepseek-chat`, `deepseek-reasoner`; V3.x/R1…), context window, max output tokens, **function/tool calling** (định dạng, giới hạn, độ chính xác), JSON output mode, params (temperature/reasoning), base URL, auth, pricing, rate limit.
- **DeepSeek Harness (DSH)**: là gì, docs ở đâu, cách nó đọc `.mcp.json` (mcpServers) và `.agents/skills` (định dạng SKILL.md frontmatter), model mặc định, cấu hình CLI/agent.
- **MCP spec**: hình dạng chuẩn của `.mcp.json` cho stdio và http (streamable HTTP), fields `env`/`headers`/auth, best practices.
- **Quirk của DeepSeek khi tool-calling**: ảnh hưởng tới cách viết mô tả tool/schema (xử lý `$ref`/`$defs`, `any`, `oneOf`, độ dài description, parallel tool calls, reasoning model vs tool).
- **Output**: `research/01-deepseek-web-report.md` + summary kèm source URLs.

### 4.2 Subagent B — Source: Audit package `com.feeder.mcp`
- Đọc toàn bộ luồng liên quan: `DeepSeekAiAgentConfigurator.cs`, `AiAgentCatalog.cs`, `AiAgentConfiguratorView.cs`, `AgentConfiguratorSettingsFactory.cs`, `UnityMcpPluginEditor.Config.cs`, `MainWindowEditor*.cs`, `Skills*.cs`, `Startup.Editor.cs`.
- Base class `AgentConfig.AiAgentConfigurator` nằm trong DLL `Feeder.McpPlugin.dll` — nếu có thể, dùng reflection trong Unity Editor (qua các tool `mcp__matrix__*`: `reflection-method-find`, `script-execute`, `unity-tool-list`) để soi surface của `AiAgentConfigurator`, `AiAgentConfiguratorRegistry`, `AgentConfiguratorSettings`, `DefaultConfigurationSections`, `TroubleshootingSection`… Nếu không truy cập được runtime, tìm DLL/source tham chiếu trong `Library/`, `Packages/`, `Assets/`.
- So sánh `DeepSeekAiAgentConfigurator` với agent Claude Code (trong DLL) — thiếu/sai gì (sections, troubleshooting, auth, args, urls, icon, link label…).
- Skills pipeline: liệt kê **tất cả** `[AiTool]` có `[AiSkillDescription]`/`[AiSkillBody]`; so khớp danh sách tool với file SKILL.md có trên đĩa; **diff `.agents/skills` vs `.claude/skills`** (dùng pwsh `Compare-Object` / `git diff --no-index`); kiểm tra skill "mồ côi" (SKILL.md không có tool tương ứng) và tool thiếu SKILL.md.
- Kiểm tra `UserSettings/Feeder-MCP-Config.json` (SkillsPath hiện tại, agent đang chọn), `.mcp.json` root nếu có, `git status`/`git log` gần đây.
- **Output**: `research/02-package-source-report.md` + summary — danh sách issue ưu tiên kèm file:line.

### 4.3 Subagent C — Best practices viết tool/skill docs + audit mẫu skills
- **Web research**: hướng dẫn chính thức về viết tool descriptions/schemas cho LLM tool-calling (Anthropic tool use best practices, OpenAI function calling, MCP spec tool annotation, DeepSeek function calling docs), tiết kiệm token, ví dụ minh họa, mô tả tham số (default, enum, unit), tránh ambiguity.
- **Audit cục bộ**: đọc ≥12 SKILL.md mẫu trải đều các nhóm (gameobject-*, assets-*, script-*, scene-*, profiler-*, ui-inspect-*, screenshot-*, reflection-*, package-*, unity-skill-create/generate, feeder-mcp-initial-setup, matrix) + source attribute tương ứng, đánh giá theo checklist: đầy đủ? đúng? tiết kiệm token? phù hợp DeepSeek (param `any` + `$ref`/`$defs` — model có resolve được không)? phù hợp Unity (main-thread, prefab edit mode, scene dirty trước tests-run, AssetDatabase refresh, an toàn undo)?
- **Output**: `research/03-skill-best-practices-report.md` + summary — **chuẩn viết skill (authoring standard)** + danh sách issue top.

---

## 5. Phase 1 — Tổng hợp & chốt quyết định (Gate)

- Gom 3 report, đối chiếu chéo (A × B × C), xác định:
  1. DeepSeek configurator cần sửa gì (nếu có) — đúng chuẩn `.mcp.json` cho DSH, auth, urls, sections, icon, order.
  2. Danh sách skill cần nâng cấp (theo mức ưu tiên: sai/nguy hiểm > thiếu > dài dòng > cosmetic).
  3. Chuẩn authoring thống nhất.
- Chốt danh sách thay đổi + trình The Architect duyệt trước khi triển khai (nếu thay đổi lớn).

## 6. Phase 2 — Sửa tùy chọn DeepSeek (nếu findings yêu cầu)

- Sửa `DeepSeekAiAgentConfigurator.cs` / `AiAgentCatalog.cs` / view (label, icon, urls, sections, troubleshooting, args, auth).
- Verify: mở Unity Editor, kiểm tra dropdown Matrix AI Connector hiển thị DeepSeek, bấm Configure viết `.mcp.json` đúng, Auto-generate skills ghi vào `.agents/skills`.
- Compile sạch (kiểm tra console log / `assets-refresh`).

## 7. Phase 3 — Nâng cấp nội dung skills ở source C#

- Sửa/viết lại `[AiSkillDescription]` và `[AiSkillBody]` (+ `[Description]` trên tham số nếu cần) theo chuẩn authoring từ Phase 1, theo thứ tự ưu tiên.
- Ưu tiên cao: các skill ảnh hưởng an toàn (scene-save, tests-run, script-update-or-create, assets-delete, gameobject-destroy, assets-modify, prefab open/close, screenshot-isolated side-effects…).
- Đảm bảo mỗi skill: nêu rõ "khi nào dùng / khi nào không", ví dụ JSON ngắn, cảnh báo tác dụng phụ, quy tắc main-thread/prefab/scene dirty.

## 8. Phase 4 — Regenerate & đồng bộ skills

- Chạy `unity-skill-generate` (qua MCP) cho `.agents/skills` (mặc định) và `.claude/skills` (path override).
- Diff lại 2 folder — phải khớp 1:1 với source attributes.
- Kiểm tra skill đặc biệt (`matrix`, `feeder-mcp-initial-setup`) không bị ghi đè/mất.

## 9. Phase 5 — Kiểm thử & xác nhận

- `unity-tool-list` đối chiếu đủ tool; mở 1-2 SKILL.md mới để review chất lượng.
- Smoke test thực tế 1-2 luồng (vd: `gameobject-find` → `gameobject-component-get` → `script-update-or-create`).
- Chạy `tests-run` (EditMode) nếu có test liên quan; kiểm tra console không có error/warning mới.
- Báo cáo tổng kết cho The Architect.

---

## 10. Files sẽ chạm tới (dự kiến)

- `PLAN-DeepSeek-Matrix-Review.md` (file này)
- `research/01-deepseek-web-report.md`, `research/02-package-source-report.md`, `research/03-skill-best-practices-report.md`
- `Packages/com.feeder.mcp/Editor/Scripts/UI/AiAgentConfigurators/DeepSeekAiAgentConfigurator.cs` (+ có thể `AiAgentCatalog.cs`, `AiAgentConfiguratorView.cs`) — nếu cần sửa
- Các file C# chứa `[AiSkillDescription]`/`[AiSkillBody]` trong `Packages/com.feeder.mcp/Editor/Scripts/API/**`
- `.agents/skills/**` và `.claude/skills/**` — chỉ qua regenerate (không sửa tay)

## 11. Tiêu chí hoàn thành (Definition of Done)

1. Tùy chọn DeepSeek cấu hình đúng chuẩn MCP cho DeepSeek Harness (`.mcp.json` stdio/http + auth + skills path `.agents/skills`) và UI hiển thị đầy đủ (tên, link, troubleshooting).
2. Mọi skill đạt chuẩn authoring: đầy đủ input/output, mô tả rõ ràng, ví dụ, cảnh báo an toàn, tiết kiệm token, đúng Unity API.
3. `.agents/skills` và `.claude/skills` khớp nhau và khớp source attributes (regenerate, không sửa tay).
4. Unity compile sạch, không error/warning mới; smoke test 2 luồng chính chạy ngon.
5. Báo cáo tổng kết + list thay đổi đã duyệt bởi The Architect.

## 12. Rủi ro / lưu ý

- Base class nằm trong DLL (`Feeder.McpPlugin.dll`) — không sửa được, chỉ adapt theo surface có sẵn.
- `unity-skill-generate` ghi đè toàn bộ folder skills — mọi thay đổi nội dung phải ở source C#.
- Skill `matrix` và `feeder-mcp-initial-setup` không sinh từ tool — sửa trực tiếp file (ngoại lệ duy nhất của nguyên tắc "không sửa tay").
- 2 folder skills lệch nhau nếu chỉ regenerate 1 nơi — luôn regenerate cả 2.
- Thay đổi trong package embedded có thể bị mất nếu package được cập nhật từ registry/git — lưu ý khi commit.
