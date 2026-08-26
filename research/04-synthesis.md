# 04 — Tổng hợp nghiên cứu & thay đổi đã thực hiện (Synthesis)

> Tổng hợp từ: `00-dsh-ground-truth.md` (tự xác minh từ DSH source), `01-deepseek-web-report.md`, `02-package-source-report.md`, `03-skill-best-practices-report.md` + xác minh runtime trong Unity Editor.

## A. Kết luận về tùy chọn DeepSeek (Matrix AI Connector)

| # | Phát hiện | Mức | Trạng thái |
|---|---|---|---|
| 1 | **stdio `command` viết tên exe trần** (`ServerExecutableName`) thay vì đường dẫn đầy đủ → `.mcp.json` stdio không chạy được (exe không nằm trên PATH) | P0 | ✅ **ĐÃ SỬA**: dùng `settings.ExecutableFullPath` + JSON-escape đúng (`JsonSerializer.Serialize`) — **bắt được bug escape `\U` khi probe runtime** |
| 2 | **ConfigPath tương đối** `".mcp.json"` (viết vào CWD, chỉ chạy vì Unity CWD == project root) | P1 | ✅ **ĐÃ SỬA**: `Path.Combine(settings.ProjectRootPath, ".mcp.json")` — verify `ConfigPath = D:/Unity/TheMatrix/.mcp.json` |
| 3 | **DSH không đọc `.mcp.json`** — DSH kết nối MCP qua plugin `dsh-mcp-client` trong `cordis.patch.yml` (`transport: streamable-http`, `url`, `headers`, `serverName`) | — | ✅ **ĐÃ SỬA**: troubleshooting của DeepSeek configurator giờ hướng dẫn rõ ràng cấu hình DSH (transport/url/headers + "DSH does not read .mcp.json"); `.mcp.json` chỉ dành cho client MCP-native (Claude Code) |
| 4 | `SkillsPath = ".agents/skills"` đúng chuẩn DSH (project-agents root, rank 200, cần `.git` ancestor — tồn tại) | — | ✅ Xác nhận đúng, không đổi |
| 5 | Link items (DownloadUrl/TutorialUrl) không render (fork-wide, `AiAgentConfiguratorView.cs:273-276`) | P1 | ⏸ Không sửa (quyết định của fork, áp dụng cho mọi agent) |
| 6 | Tool `Enabled=false` (ping, unity-skill-create, unity-skill-generate…) vẫn sinh SKILL.md nhưng không đăng ký trong MCP server → skill "chết" | P1 | ⏸ Nằm trong DLL generator — không sửa được từ package |
| 7 | DeepSeek chưa có trong `skillAutoGenerate`/`agentAutoConfigure` (config chỉ có claude-code/codex); chưa có preset trong MatrixSpace AgentBackendCatalog | P2 | ⏸ Tùy chọn sản phẩm — đề xuất cho The Architect |
| 8 | `.mcp.json` hiện tại (`http://localhost:24946/mcp`) khớp đúng chuẩn streamable-http | — | ✅ Không cần đổi |

## B. Kết luận về skills

### Đã xác minh ĐÚNG (không cần sửa)
- `.agents/skills` và `.claude/skills` **byte-identical** (0 diff) — không drift.
- 83 thư mục skill: 78 tool registered + 3 skill của tool disabled + `matrix` + `feeder-mcp-initial-setup` (viết tay, chất lượng tốt).
- Format DSH: `<name>/SKILL.md`, frontmatter `name` (kebab-case regex `/^[a-z0-9]+(?:-[a-z0-9]+)*$/`) + `description` bắt buộc, `whenToUse`/`metadata`/invocation tùy chọn — **166/166 tên hợp lệ**.
- Description DSH cap 500 chars; body nhúng verbatim `<skill_content>`.
- Claim "76/83 file mojibake" của report 03 là **sai** (false positive do byte `C3 A2` = "â" tiếng Việt trong `matrix/SKILL.md`); kiểm tra raw bytes: chỉ file này có, và là ký tự hợp lệ.

### Giới hạn generator (DLL `Feeder.McpPlugin.dll` — không sửa được từ package)
- "How to Call" ví dụ JSON luôn dùng `"string_value"` placeholder cho mọi param.
- `$defs` phình to (ViewQuery/GameObjectRef/SerializedMember lặp lại), CLR types lộ dạng `$ref` (`System.String-1`...).
- Khắc phục từ phía package = bổ sung nội dung chuẩn trong `[AiSkillDescription]`/`[AiSkillBody]` (mô tả what+when+when-not, warning an toàn, cross-refs).

### Đã nâng cấp (Phase 3): 8 skill an toàn cao
| Skill | Thay đổi | File |
|---|---|---|
| `gameobject-destroy` | Description + Body: **IRREVERSIBLE** (DestroyImmediate, không undo, prefab stage ghi vào asset khi close) | `GameObject.cs` + `GameObject.pre-Unity.6.5.cs` (DestroySkill) |
| `assets-delete` | Description + Body: **IRREVERSIBLE** (xóa file khỏi đĩa, không trash/undo) | `Assets.Delete.cs` |
| `script-delete` | Description + Body: **IRREVERSIBLE** (.cs + .meta) | `Script.Delete.cs` |
| `scene-unload` | Description + Body: **cảnh báo mất thay đổi chưa lưu** → dùng `scene-save` trước | `Scene.Unload.cs` |
| `assets-modify` | Description + Body: **modify không undo, patch sai có thể corrupt asset/vỡ reference** | `Assets.cs` (ModifySkill) |
| `object-modify` | Description + Body: **ghi trực tiếp không undo, verify lại bằng object-get-data** | `Object.Modify.cs` |
| `package-remove` | Description + Body: **gỡ package đang được code dùng → vỡ compile** | `Package.Remove.cs` |
| `assets-move` | Description + Body: **move/rename có thể vỡ reference** | `Assets.Move.cs` |

Đã regenerate cả 2 folder → verify: 0 diff, warning hiện diện, description < 500 chars (max 438), tên hợp lệ, compile errors=False, tool registry 78 tool nguyên vẹn, session catalog (DSH) nhận mô tả mới.

## C. Chuẩn viết skill (từ report 03, đã đối chiếu DSH)
1. **Frontmatter description** = formula *what + when + when-not + caveat*, ≤2 câu, ≤500 ký tự; destructive → chữ IRREVERSIBLE/không undo ngay ở đầu.
2. **Body**: summary → when-to/when-not → inputs (kèm default) → behavior/side-effects → edge cases → **ví dụ JSON thật** (không placeholder) → cross-refs → warnings.
3. Tham chiếu tool khác bằng đúng tool id; nhất quán thuật ngữ giữa các skill cùng họ.
4. DeepSeek-fit: schema phải có root `"type":"object"` (đã có), tránh `any`, ưu tiên mô tả ngắn gọn, không phụ thuộc JSON-mode cho reasoner.

## D. Việc còn lại (các round sau)
- [x] Warning an toàn cho 8 skill — **xong round 4**
- [x] **Quyết định sản phẩm (The Architect đã duyệt round 4)**:
  - [x] Thêm `deepseek: true` vào mặc định `SkillAutoGenerate` + `AgentAutoConfigure` (`Runtime/UnityMcpPlugin.Config.cs` — 3 chỗ) + cập nhật in-memory/on-disk (`UserSettings/Feeder-MCP-Config.json`) — verify `gen=True cfg=True`
  - [x] Thêm preset **DeepSeek CLI** vào `MatrixSpace/AgentBackendCatalog.cs` (id `deepseek`, exe `dsh`, args `{prompt}` editable, Note giải thích dsh là harness launcher) — verify preset count 7, compile errors=False
- [~] Phase 5: báo cáo tổng kết cho The Architect (round 4)
- [~] Khi-nào-không-nên-dùng + ví dụ JSON thật cho họ data-getting: ưu tiên thấp — generator placeholder `string_value` không sửa được từ package; bodies hiện đã đầy đủ
