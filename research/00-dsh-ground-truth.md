# 00 — DSH Ground Truth (from local DSH source, verified directly)

> Nguồn: `C:\Users\Admin\AppData\Local\npm-cache\_npx\1e7f6d9597241db0\node_modules\@deepseek-ai\...` (DeepSeek Harness implementation checkout).
> Đây là bằng chứng trực tiếp từ source DSH — dùng để đối chiếu với report web (01) và source package (02).

## 1. Cách DSH nạp skills — `dsh-skill-filesystem/lib/index.js`

- Roots được quét (theo `cwd`, project root = thư mục chứa `.git` — đã xác minh `D:\Unity\TheMatrix\.git` tồn tại):
  - `projectRoot/.dsh/skills` (rank 100, `project-dsh`)
  - `projectRoot/.agents/skills` (rank 200, `project-agents`) ← **xác nhận `SkillsPath = ".agents/skills"` của DeepSeek configurator là đúng**
  - `customSkillDirs` (rank 300)
  - `dshHome/skills` (rank 400, user-dsh; `DSH_HOME`/`resolveDshHome`)
  - `agentsHome/skills` (rank 500, user-agents; `DSH_AGENTS_HOME` hoặc `~/.agents`)
  - bundled (rank 600)
- Format phát hiện: **directory bundle** `<root>/<skill-name>/SKILL.md` hoặc **flat** `<root>/<name>.md`.
- **Frontmatter YAML BẮT BUỘC** (`parseSkillFile`): `name` (string) + `description` (string). Thiếu một trong hai → skill bị ignore kèm warning. `name` phải khớp regex `/^[a-z0-9]+(?:-[a-z0-9]+)*$/` (kebab-case) (`dsh-skill/lib/index.js`).
- Frontmatter tùy chọn: `whenToUse` (string), `metadata` (object), invocation policy: `disable-model-invocation` (bool), `user-invocable` (bool). Key legacy `disableModelInvocation`/`modelInvocable`/`userInvocable` bị **reject** (skill bị ignore).
- Body được nhúng verbatim vào model dưới dạng `<skill_content name="...">` → chất lượng body ảnh hưởng trực tiếp.
- Đã validate: **166/166 skill dirs có tên kebab-case hợp lệ** (cả `.agents/skills` + `.claude/skills`).

## 2. Cách DSH kết nối MCP server — `dsh-mcp-client/lib/index.js`

- DSH kết nối MCP qua **plugin config riêng của DSH** (được load từ `cordis.yml` — xem lỗi "pick a unique serverName in cordis.yml"), **KHÔNG đọc project `.mcp.json`** (đã grep toàn bộ `@deepseek-ai/*` không thấy `.mcp.json`/`mcpServers`/`Feeder`).
- Config schema (`Config` union):
  - **stdio**: `{ transport: "stdio", serverName, command, args: string[], env: {k:v}, cwd, toolCallTimeoutMs, failOnStartupError, reconnect }`
  - **streamable-http**: `{ transport: "streamable-http", serverName, url, headers: {k:v}, toolCallTimeoutMs, failOnStartupError, reconnect }`
- `serverName` phải khớp pattern kebab (SERVER_NAME_PATTERN), duy nhất trong app.
- Tools MCP được publish với tên `serverName.rawName` (namespace hóa).

## 3. Hệ quả cho review tùy chọn DeepSeek (Matrix AI Connector)

1. **Đúng**: `.agents/skills` — DSH đọc root này; "Auto-generate Skills" ghi vào đây là chuẩn.
2. **Cần làm rõ**: `.mcp.json` mà DeepSeek configurator viết **KHÔNG được DSH tiêu thụ** — chỉ hữu dụng cho client đọc `.mcp.json` (Claude Code…). Với DSH, bước quan trọng là **cấu hình plugin mcp-client**: `transport: streamable-http`, `url: http://localhost:<port>/mcp`, `headers: { "Authorization": "Bearer <token>" }` (hoặc stdio).
3. Endpoint HTTP của Unity server: chuẩn hóa thành `<base>/mcp` (`McpServerManager.cs` ~L1223-1225); default `http://localhost:<port>/mcp`.
4. **Đề xuất sửa**: bổ sung vào troubleshooting/help của DeepSeek configurator bước "cấu hình DSH (mcp-client plugin)" với đúng key `transport`/`url`/`headers` — vì text hiện tại mới nói chung chung "Point your DeepSeek client at the MCP URL".

## 4. Tài liệu tham khảo (paths tuyệt đối)

- `...\@deepseek-ai\dsh-skill-filesystem\lib\index.js` — roots, format, frontmatter, watcher
- `...\@deepseek-ai\dsh-skill\lib\index.js` — `SKILL_NAME` regex, `renderSkillContent`
- `...\@deepseek-ai\dsh-mcp-client\lib\index.js` — `Config` schema, `createTransport` (stdio / StreamableHTTPClientTransport)
