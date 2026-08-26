# Review + Kế hoạch sửa: tùy chọn DeepSeek trong Matrix AI Connector

Status: Draft — 2026-08-26
Scope: mọi thay đổi trong working tree phục vụ việc thêm agent **DeepSeek** (configurator, catalog,
backend preset, defaults, và phần port DLL → source đi kèm).
Related: ADR-0002 (MCP versioning & transports), ADR-0005 (security), `research/00-dsh-ground-truth.md`

---

## 0. Tóm tắt

Thay đổi hiện tại **hoạt động được** (project compile từ source, 78 tool nguyên vẹn,
`.agents/skills` đúng root mà DeepSeek Harness đọc, skills sửa qua attribute C# rồi regenerate,
`.agents` và `.claude` byte-identical 83/83). Nhưng có **3 lỗi thật**, **9 điểm lệch quy tắc dự án**,
và **4 nhóm polish** cần xử lý trước khi commit.

Ưu tiên: P0 (lỗi) → P1 (quy tắc/hygiene) → P2 (polish).

---

## 1. Những gì đã ĐÚNG (giữ nguyên, không sửa)

| Hạng mục | Bằng chứng |
|---|---|
| `SkillsPath = ".agents/skills"` | `dsh-skill-filesystem/lib/index.js` quét `projectRoot/.agents/skills` (rank 200) — verified |
| Sửa nội dung skill ở source C# (`[AiSkillDescription]`/`[AiSkillBody]`) rồi regenerate | 13 file `API/Tool/*.cs` thay đổi; không có SKILL.md nào bị sửa tay |
| 2 bản skills đồng bộ | `diff -r .agents/skills .claude/skills` → 0 khác biệt, 83 thư mục mỗi bên |
| `IconName = ""` không làm vỡ UI | `AiAgentConfiguratorView.SetAgentIcon()` ẩn slot khi rỗng |
| Stdio args khớp semantics của `AgentConfigBuilders.StdioArgs` | `AuthOption` enum vốn đã lowercase → `ToLowerInvariant()` là no-op vô hại |
| Troubleshooting nói đúng sự thật "DSH does not read .mcp.json" | `dsh-mcp-client` chỉ đọc config plugin từ `cordis.yml` — verified |
| Port DLL → source compile sạch | `Library/ScriptAssemblies/Feeder.McpPlugin.dll` (325 KB) build từ source |

---

## 2. P0 — Lỗi thật, phải sửa

### P0-1. `AgentSetupBase` vẫn dùng registry cũ → DeepSeek "vô hình" với luồng Setup

`Editor/Scripts/MatrixSpace/Setup/AgentSetupBase.cs:41,44` gọi
`AiAgentConfiguratorRegistry.GetByAgentId/All`, không phải `AiAgentCatalog`. Đây là **file duy nhất
còn sót** sau khi `MainWindowEditor.AiAgents.cs` và `Startup` đã chuyển sang catalog.

Hệ quả: bất kỳ `IAgentSetup` nào có `PresetId = "deepseek"` sẽ log
`No MCP configurator 'deepseek' available — skipped` và **im lặng bỏ qua bước ghi MCP config**.

**Fix**: đổi alias `using` sang `AiAgentCatalog` (cùng namespace `Feeder.MCP.Editor.UI`, đã được
import sẵn ở dòng 6), thay 2 call site. Cân nhắc bổ sung `GetByAgentName` vào `AiAgentCatalog`
để xóa hẳn vòng lặp fallback thủ công ở `:44-52`.

### P0-2. Preset `deepseek` trong `AgentBackendCatalog` không chạy được

`Editor/Scripts/MatrixSpace/AgentBackendCatalog.cs:145-154` — `DefaultArguments = "{prompt}"`.
`dsh` là launcher; `lib/bin.js` **bắt buộc** `--profile`, nếu thiếu thì thoát ngay với
`error: --profile <name> is required`. Help của chính nó ghi rõ dạng non-interactive:

```
dsh --profile headless "run the tests"   answer one task, print the result, and exit
```

Nên preset hiện tại **chắc chắn fail ngay turn đầu tiên**.

**Fix**:

```csharp
DefaultArguments = "--profile headless {prompt}",
```

và viết lại `Note` cho khớp (bỏ câu "set the executable, arguments … to match your installation",
thay bằng: profile `headless` in kết quả rồi thoát; profile được khởi tạo lần chạy đầu dưới
`~/.dsh/profiles/<name>/`).

### P0-3. Config DeepSeek không dọn key của transport kia

`DeepSeekAiAgentConfigurator.CreateStdioConfig` thiếu `SetPropertyToRemove("type")` +
`SetPropertyToRemove("url")`; `CreateHttpConfig` thiếu `SetPropertyToRemove("command")` +
`SetPropertyToRemove("args")`. So sánh với `AgentConfigBuilders.JsonStdio/JsonHttp` và
`ClaudeCodeConfigurator` — cả hai đều dọn.

Hệ quả: khi user đổi transport, entry `Feeder-MCP` trong `.mcp.json` còn sót key của transport cũ
→ entry lai (vừa `command`/`args` vừa `type`/`url`), client MCP có thể từ chối.

**Fix**: thêm 4 lời gọi `SetPropertyToRemove` tương ứng. (`AgentConfigBuilders` là `internal`
trong `Feeder.McpPlugin` nên không tái sử dụng trực tiếp được từ assembly Editor.)

---

## 3. P1 — Lệch quy tắc / thiết kế dự án

### P1-1. Hai agent cùng sở hữu một file `.mcp.json` — và DSH không đọc file đó

`Runtime/UnityMcpPlugin.Config.cs:121,145` đặt `AgentAutoConfigure["deepseek"] = true`. Nhưng:

- DeepSeek ghi **đúng file, đúng entry** mà Claude Code đã ghi (`ExpectedFileContent` hard-code
  server key `"Feeder-MCP"`), nên đây là ghi trùng: mỗi session Editor ghi 2 lần, thứ tự phụ thuộc
  thứ tự duyệt Dictionary → last-writer-wins trên một file **đang nằm trong version control**.
- Theo ground truth, **DSH không hề đọc `.mcp.json`** → giá trị thực tế của flag này bằng 0.

**Fix (khuyến nghị)**: bỏ `["deepseek"] = true` khỏi `AgentAutoConfigure` (cả default field lẫn
`Reset()`), giữ nguyên hành vi cũ cho user hiện hữu. Ai cần vẫn bật được bằng toggle trong UI.

### P1-2. `SkillAutoGenerate["deepseek"] = true` là default không có tác dụng

`Startup.cs:32-41` chỉ generate skills cho **agent đang được chọn**:

```csharp
var agent = AiAgentCatalog.GetByAgentId(savedAgentId);   // savedAgentId, không phải toàn bộ flag
if (agent?.SupportsSkills == true && UnityMcpPluginEditor.IsAutoGenerateSkills(agent.AgentId))
```

Nên flag `deepseek=true` không bao giờ kích hoạt trừ khi DeepSeek là agent được chọn — và khi
kích hoạt thì nó **ghi đè `UnityMcpPluginEditor.SkillsPath` toàn cục** sang `.agents/skills`
mà không khôi phục.

**Chọn 1 trong 2** (cần The Architect quyết):

- **(a) Đơn giản**: bỏ `["deepseek"]=true`, để user tự bật → khớp hành vi thật của code.
- **(b) Đúng ý đồ**: đổi `Startup` sang lặp qua *mọi* agent có `IsAutoGenerateSkills(id)==true`,
  generate lần lượt (giống `AutoConfigureAgents`), và **khôi phục `SkillsPath` gốc sau cùng**
  (mẫu có sẵn ở `API/SystemTool/Skills.Generate.cs:76-84`). Đây là cách duy nhất để `.agents` và
  `.claude` tự đồng bộ — vốn là điều bản review này đang phải verify bằng tay.

### P1-3. Lý do tồn tại của `AiAgentCatalog` đã hết hiệu lực

Doc comment của `AiAgentCatalog` / `DeepSeekAiAgentConfigurator` viết: *"the shared registry compiled
into Feeder.McpPlugin.dll has no runtime registration API"*. Nhưng cùng đợt thay đổi này đã **thay
3 DLL bằng source** (`Runtime/Plugins/Feeder.Source/`), nên registry giờ là file source sửa được:
`.../Feeder.McpPlugin.AgentConfig/AiAgentConfiguratorRegistry.cs`.

**Chọn 1 trong 2**:

- **(a) Đăng ký thẳng** `new DeepSeekAiAgentConfigurator()` vào mảng registry — nhưng phải chuyển
  configurator xuống assembly `Feeder.McpPlugin`, mất tính "package-side extension" và làm
  divergence với upstream lớn hơn khi re-port.
- **(b) Giữ `AiAgentCatalog`** (khuyến nghị — cô lập divergence khỏi phần port) nhưng **sửa lại
  doc comment** cho đúng sự thật: giữ ngoài registry *có chủ đích* để phần port ở lại 1:1 với DLL
  gốc, dễ re-port khi upstream đổi.

### P1-4. Thứ tự dropdown phá quy ước alphabet

Registry sort theo `AgentName` rồi append `Custom` cuối cùng. `AiAgentCatalog.Build()` chèn DeepSeek
ngay sau `claude-code` → nằm **giữa "Claude Code" và "Claude Desktop"**.

**Fix**: chèn theo đúng thứ tự alphabet (giữ `Custom` cuối) — tìm vị trí đầu tiên có
`AgentName` sắp xếp sau "DeepSeek" và không phải `Custom`.

### P1-5. Thiếu icon

15/15 agent chia sẻ đều có `Editor/Gizmos/ai-agents/<name>-64.png`. DeepSeek để rỗng → dòng agent
trong UI khuyết so với mọi entry khác.

**Fix**: thêm `deepseek-64.png` (64×64, import settings copy từ `claude-64.png.meta`), đặt
`IconName => "deepseek-64.png"`.

### P1-6. Thiếu CHANGELOG + version bump

`Packages/com.feeder.mcp/CHANGELOG.md` ghi từng version; `package.json` vẫn `0.85.0` trong khi
working tree chứa: agent mới, port 3 DLL → source, đổi asmdef references, đổi default config.

**Fix**: bump `0.86.0` + entry CHANGELOG gồm 3 mục: (1) DeepSeek agent + `.agents/skills`,
(2) 3 plugin DLL thay bằng source biên dịch trong package, (3) thay đổi default
`AgentAutoConfigure` / `SkillAutoGenerate` (nếu giữ).

### P1-7. Hygiene: 344 MB rác chưa bị ignore

`research/` (decompiled + 3 upstream clone + `port-build`) = **344 MB**, và
`PLAN-DeepSeek-Matrix-Review.md` ở root — cả hai **không** nằm trong `.gitignore` → `git add -A`
sẽ nuốt hết vào repo.

**Fix**:

- Thêm `research/` vào `.gitignore` (hoặc move ra ngoài repo).
- Xóa/di chuyển `PLAN-DeepSeek-Matrix-Review.md` — tài liệu dự án nằm ở `Matrix~/Docs/`
  (chính là chỗ file này đang nằm).
- Commit 5 skill mới `.agents/skills/ui-inspect-*` cùng đợt.

### P1-8. Licensing của source đã port

`Feeder.Source/` (293 file, ~22.7k LOC) là **decompile từ DLL đã ship**; `research/upstream-src`
cho thấy nguồn gốc upstream (Unity-MCP / ReflectorNet / MCP-Plugin-dotnet). `LICENSE.md` của package
là MIT "Copyright (c) 2026 Feeder", **không có NOTICE / attribution cho bên thứ ba**.

**Fix (bắt buộc trước khi publish)**: xác nhận license upstream, thêm `THIRD-PARTY-NOTICES.md`
(hoặc header per-folder) ghi rõ nguồn + license gốc cho 3 assembly đã port. Đây là quyết định
pháp lý — cần The Architect chốt, không tự làm.

### P1-9. `.mcp.json` bị thêm BOM

Diff duy nhất của `.mcp.json` là thêm `\ufeff` ở đầu file. Package ghi file bằng `File.WriteAllText`
(UTF-8 **không** BOM), nên BOM này đến từ một lần sửa tay bằng PowerShell.

**Fix**: `git checkout -- .mcp.json` rồi để `Configure()` ghi lại.

---

## 4. P2 — Polish

| # | Việc | Chi tiết |
|---|---|---|
| P2-1 | `ModelsDevProviderId = "deepseek"` | `Library/com.feeder.mcp/models-dev.json` có provider `deepseek` (`deepseek-v4-pro`, `deepseek-v4-flash`, …). Chỉ hữu ích nếu đồng thời quyết được cách truyền model cho `dsh` (profile-based, không có flag `-m`) — nếu không thì **cố ý bỏ trống** và ghi chú lý do trong `Note`. |
| P2-2 | `LoginHint` / `LoginArgs` | Mọi preset khác đều có hint. DeepSeek đăng nhập qua `~/.dsh/settings.yaml` + `llm-deepseek` → viết hint đúng thay vì để trống. |
| P2-3 | `DeepSeekSetup : AgentSetupBase` | Mọi preset trừ `custom` đều có installer trong `AgentSetupCatalog`. `dsh` phát hành qua npm (`@deepseek-ai/dsh`, bin `dsh`) → step `npm i -g @deepseek-ai/dsh` khả thi, tái dùng pipeline của `ClaudeSetup`. Không làm thì nút SETUP bị ẩn — chấp nhận được nhưng lệch với các preset anh em. |
| P2-4 | Dọn code thừa + doc sai trong configurator | (a) `CreateStdioConfig`/`CreateHttpConfig` tự gọi `ApplyStdio/HttpAuthorization` trong khi base `GetStdioConfig/GetHttpConfig` đã gọi → thừa (idempotent nên không sai). (b) Comment "identical shape Claude Code writes" **không đúng**: Claude ghi `command` dạng forward-slash (`.Replace('\\','/')`), DeepSeek ghi backslash escaped → sửa comment hoặc normalize giống Claude. (c) `InstallUrl` nên là `https://github.com/deepseek-ai/deepseek-harness` (tên repo thật). (d) Troubleshooting nên nêu đúng đường dẫn `~/.dsh/profiles/<profile>/cordis.yml` và ràng buộc `serverName` kebab-case, duy nhất trong app. |

---

## 5. Thứ tự thi công đề xuất

1. **Quyết định cần chốt trước** (chặn P1-2, P1-3, P1-8): (a) skills auto-generate cho *mọi* agent
   bật flag hay chỉ agent đang chọn; (b) giữ `AiAgentCatalog` hay đăng ký thẳng vào registry;
   (c) hướng xử lý license cho `Feeder.Source`.
2. **Lô 1 — P0** (3 sửa, không cần quyết định gì): `AgentSetupBase` → catalog; `--profile headless`;
   4 lời gọi `SetPropertyToRemove`.
3. **Lô 2 — P1 code**: defaults trong `UnityMcpPlugin.Config.cs`, thứ tự dropdown, doc comment, icon.
4. **Lô 3 — P1 hygiene**: `.gitignore` cho `research/`, xóa PLAN ở root, bỏ BOM `.mcp.json`,
   CHANGELOG + bump `0.86.0`, THIRD-PARTY-NOTICES.
5. **Lô 4 — P2**: preset polish + `DeepSeekSetup`.

## 6. Kiểm chứng sau mỗi lô

- `assets-refresh` → `console-get-logs` (filter Error) — bắt buộc sau mọi thay đổi C#.
- Mở **Tools > Feeder > Matrix AI Connector**: DeepSeek xuất hiện đúng vị trí alphabet, có icon,
  Configure/Remove chạy; đổi transport stdio↔http rồi **đọc lại `.mcp.json`** xác nhận không còn key thừa.
- Matrix Space: tạo pane backend "DeepSeek CLI", gửi 1 prompt, xác nhận `dsh --profile headless`
  trả kết quả (không còn lỗi `--profile <name> is required`).
- `unity-skill-generate` cho cả 2 path → `diff -r .agents/skills .claude/skills` phải rỗng.
- `git status` sạch rác: không có `research/`, không có file plan ở root.

## 7. Rollback

- Code DeepSeek: xóa 2 file trong `Editor/Scripts/UI/AiAgentConfigurators/`, revert
  `MainWindowEditor.AiAgents.cs`, `Startup*.cs`, `UnityMcpPlugin.Config.cs`, `AgentBackendCatalog.cs`.
- Source port: `git checkout -- Packages/com.feeder.mcp/Runtime/Plugins/Feeder/` (khôi phục 3 DLL),
  xóa `Runtime/Plugins/Feeder.Source/`, revert 2 asmdef.
