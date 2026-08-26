# DeepSeek × DeepSeek Harness × MCP — Web Research Report

**Purpose:** Verify that the "DeepSeek" option in the Unity package **Matrix AI Connector** (`com.feeder.mcp`), which writes a project-root `.mcp.json` (`mcpServers.Feeder-MCP`) and generates `SKILL.md` skills into `.agents/skills`, is optimal for DeepSeek models connected through the **DeepSeek Harness (DSH)**.

**Method:** Web research (official docs, GitHub issues, community guides) + inspection of the locally installed DSH runtime (`@deepseek-ai/*` packages, v0.1.1-rc.2, and the live `~/.dsh` config) for authoritative, version-pinned facts. DSH-internal facts are marked **[DSH runtime, verified locally]**. Research date: ~2026-08.

---

## 1. DeepSeek models & API (current)

### Model names
| Model id (API) | Role | Notes |
|---|---|---|
| `deepseek-chat` | Default general chat model | Lineage: V3.x → **V3.1 (hybrid thinking)** → **V3.2-Exp (DSA long-context)**. Supports optional thinking mode. |
| `deepseek-reasoner` | Reasoning model | Historically R1-0528; since V3.x/V3.2 it is the thinking variant (emits `reasoning_content`). |
| `deepseek-v4-flash` / `deepseek-v4-pro` / `deepseek-v4-flash-vision-exp` | V4 preview family (open-sourced 2026-04-24) | **Default in DSH.** Vision variant is image-capable. 1,000,000-token context advertised. |

- **Sources:** [DeepSeek API — Your First API Call](https://api-docs.deepseek.com/), [Models & Pricing](https://api-docs.deepseek.com/quick_start/pricing/), [DeepSeek V4 Preview Release](https://api-docs.deepseek.com/news/news260424/), [DeepSeek-V4 预览版：迈入百万上下文普惠时代](https://api-docs.deepseek.com/zh-cn/news/news260424/), [DeepSeek-V4正式发布 (stdaily)](https://www.stdaily.com/web/gdxw/2026-04/25/content_507933.html), [DeepSeek V3.2 API Guide (codersera)](https://codersera.com/blog/deepseek-v32-api-guide-deepseek-chat-deepseek-reasoner-openai-sdk/), [DeepSeek V4 Flash model card (EmpirioLabs)](https://empiriolabs.ai/models/deepseek-v4-flash), [How to Use the DeepSeek V4 API (apidog)](https://apidog.com/blog/how-to-use-deepseek-v4-api/).
- **[DSH runtime, verified locally]** `@deepseek-ai/dsh-llm-deepseek` default catalog: `deepseek-v4-flash`, `deepseek-v4-pro`, `deepseek-v4-flash-vision-exp`, each with a **1,000,000-token context window** and `maxTokens` output cap default **256,000**. The live `~/.dsh/settings.yaml` confirms the deployment default: `provider: deepseek-official`, `model: deepseek-v4-flash`, `reasoningEffort: high`.

### Endpoint, auth, transport
- **Base URL:** `https://api.deepseek.com` (OpenAI-compatible; `/chat/completions`; `/v1` alias works).
- **Auth:** API key — HTTP header `Authorization: Bearer <key>`. No OAuth.
- **[DSH runtime]** Adapter uses `DEEPSEEK_API_KEY` env (per-request via credentials seam) and optional `$DEEPSEEK_BASE_URL`; **streaming only** (`stream_options.include_usage` always on); error taxonomy `AUTH / QUOTA / RATE_LIMIT / CONTEXT_WINDOW_EXCEEDED / INVALID_REQUEST / SERVER`; default retry policy: 5 retries, backoff 500 ms → 10 s.
- **Sources:** [DeepSeek API — Chat Completions](https://api-docs.deepseek.com/api/create-chat-completion/), [DEEPSEEK_SETUP.md](https://github.com/gunksd/Perps-news/blob/main/docs/DEEPSEEK_SETUP.md), [DeepSeek API Guide (deepseek-usa.ai)](https://deepseek-usa.ai/docs/api/), [DeepSeek API Error taxonomy (dsh-llm-deepseek README — local checkout)].

### Context / output limits
- V3.x line (`deepseek-chat`, `deepseek-reasoner`): **128K context** on the official API (pricing page lists context windows).
- **V4 preview: 1,000,000-token context** ("百万上下文普惠时代" = million-context popularization era). Community model card pages list V4-flash specs.
- Max output tokens: official docs cap `max_tokens` (default 4096, max 8192 for V3.x — treat exact current ceiling as **uncertain**, check [Models & Pricing](https://api-docs.deepseek.com/quick_start/pricing/)); **[DSH runtime]** the harness configures a per-request output cap defaulting to 256,000.

### Pricing & rate limits (volatile — verify before quoting)
- V3.2-Exp cut prices roughly in half: **< $0.03 / 1M input tokens** (cache miss) at launch, with cache-hit pricing far lower (VentureBeat headline); official page is authoritative.
- Later (2026) **price increases up to ~1100% on some tiers** were reported (Chinese press, 163.com / zhidx.com) — DeepSeek pricing changed multiple times; **do not hard-code numbers in product copy**.
- Rate limits are not cleanly published; community trackers (e.g. tickerr.ai) approximate them. 429 → `RATE_LIMIT` error class in DSH.
- **Sources:** [VentureBeat — V3.2-Exp halves API pricing](https://venturebeat.com/technology/deepseeks-new-v3-2-exp-model-cuts-api-pricing-in-half-to-less-than-3-cents), [DeepSeek 新定价今日生效 (163)](https://www.163.com/dy/article/L4HEA4JS051180F7.html), [DeepSeek Free Tier (pricepertoken)](https://pricepertoken.com/endpoints/deepseek/free), [DeepSeek limits (tickerr)](https://tickerr.ai/limits/deepseek), [Models & Pricing](https://api-docs.deepseek.com/quick_start/pricing/).

### Default chat vs reasoning
- Official quickstart uses `deepseek-chat` for chat and `deepseek-reasoner` for reasoning. **In DSH, the default is `deepseek-v4-flash` (V4 flash) with `reasoningEffort: high`** — i.e., thinking enabled by default; `off` maps to `thinking.type: disabled` on the wire. **[DSH runtime]**

---

## 2. DeepSeek function/tool calling

### Support & format
- **Yes — OpenAI-compatible function calling**: `tools` array (`{type:"function", function:{name, description, parameters (JSON Schema)}}`), `tool_choice: "auto" | "none" | "required" | "<specific function name>"` (forced calling), multiple tool calls per turn, streaming `delta.tool_calls`.
- **Sources:** [Tool Calls | DeepSeek API Docs](https://api-docs.deepseek.com/guides/tool_calls/), [DeepSeek Tool Calls guide (deepseek-usa.ai)](https://deepseek-usa.ai/docs/deepseek-tool-calls/), [DeepSeek V3.2 Developer Guide (sitepoint)](https://www.sitepoint.com/deepseek-v32-the-complete-developer-guide-2026/).

### Known limitations & quirks (important)
1. **Reasoner/thinking + tools is a fragile area:**
   - [vercel/ai #10778](https://github.com/vercel/ai/issues/10778) — tool calling inside DeepSeek "thinking mode" (V3.2) needed SDK support; treat thinking-mode tool calls as less mature.
   - [cline/cline #8365](https://github.com/cline/cline/issues/8365) — **V3.2 sometimes emits tool calls inside `reasoning_content`**; clients must parse `reasoning_content` for tool calls.
   - [deepseek-ai/DeepSeek-V3 #1448](https://github.com/deepseek-ai/DeepSeek-V3/issues/1448) — official reasoning-backend **JSON-constraint sampling for tool calls has defects** (community bug report).
   - [deepseek-ai/DeepSeek-V3 #1541](https://github.com/deepseek-ai/DeepSeek-V3/issues/1541) — **v4-flash emits unquoted enum literals in forced tool_call arguments (invalid JSON), ~40–60% of long-prompt calls**. Mitigate: keep prompts/schemas lean, prefer `auto` over forced calls, validate+repair arguments.
2. **JSON mode (`response_format: {"type":"json_object"}`)** exists ([JSON Output guide](https://api-docs.deepseek.com/guides/json_mode/)) but:
   - Community mirrors document the constraint that the prompt must contain the word "json" ([json_mode.md mirror](https://github.com/thevibeworks/deepseek-docs/blob/main/content/en/guides/json_mode.md)).
   - Interop errors are common ([dify #12713](https://github.com/langgenius/dify/issues/12713)) — deepseek-reasoner historically rejected `response_format`; treat as best-effort, prefer tool calls or explicit "output JSON only" instructions.
3. **Schema strictness — the `type: object` trap:** OpenAI-compatible providers (incl. DeepSeek) fail when a tool's JSON Schema lacks an explicit top-level `"type": "object"` ([vercel/ai #7924](https://github.com/vercel/ai/issues/7924), fixed in [vercel/ai commit 14e94d9](https://github.com/vercel/ai/commit/14e94d9994ced43f462c9e6699308cc2700e3bc4)). → **Always emit `{"type":"object","properties":{...}}`; never a bare `$ref` at the root.**
4. **`$ref` / `$defs`, `anyOf` / `oneOf`, `any` types:** DeepSeek parses standard JSON Schema via the OpenAI-compatible layer, but community experience is that **nested `$ref`/`$defs` and `anyOf`/`oneOf`/`any` are resolved inconsistently** and inflate token cost (every schema token is sent on each request). Best practice: **flatten/inline schemas, avoid `any`, avoid deep `$defs` indirection in tool schemas**, keep `description` strings short (they are sent verbatim in every request and count toward the prompt prefix / KV cache).
5. **Reasoning passback:** in thinking mode, multi-turn tool-call conversations must **replay `reasoning_content`** from earlier assistant turns (DeepSeek thinking-mode docs; [thinking-mode tool-call sample](https://api-docs.deepseek.com/api_samples/thinking_mode_api_example_tool_call_output/)). **[DSH runtime]** the adapter already implements this ("reasoning passback rule") — so this is handled by DSH, not by Matrix.

### What this means for tool *descriptions* (do / don't) — see §6.

---

## 3. DeepSeek Harness (DSH)

### What it is
- Official product/repo: **[deepseek-ai/deepseek-harness](https://github.com/deepseek-ai/DeepSeek-Harness)** — a Node.js agent harness (Cordis plugin architecture), CLI `@deepseek-ai/dsh`. Entry modes: `dsh --profile web` (Web GUI), `dsh --profile headless "<job>"`, `dsh plugin ...`. Profiles auto-init from shipped templates; user patches go in `<profile>/cordis.patch.yml` + `$DSH_HOME/cordis.patch.yml`.
- **Sources:** [deepseek-harness README](https://github.com/deepseek-ai/deepseek-harness/blob/master/README.md), [CLI README (apps/cli)](https://github.com/deepseek-ai/deepseek-harness/blob/master/apps/cli/README.md), [DeepSeek Harness (Aliyun Model Studio)](https://help.aliyun.com/en/model-studio/deepseek-harness), [DeepSeek Harness 配置参考 (settings.yaml)](https://www.ai-indeed.com/encyclopedia/29682.html), [Tencent Cloud guide](https://cloud.tencent.cn/developer/article/2728154).

### Config
- **Home:** `$DSH_HOME` else `~/.dsh` (all user data under one root). **[DSH runtime]**
- **Settings:** `settings.yaml` (or `.json`) at harness home; namespaces per plugin. **[DSH runtime]** Verified live file `~/.dsh/settings.yaml`:
  ```yaml
  agent-default-model:
    provider: deepseek-official
    model: deepseek-v4-flash
    reasoningEffort: high
  ```
- **Credentials:** `~/.dsh/.credentials.yaml` (present in the live install). API key itself never appears in config; resolved per request via credentials seam then env (`DEEPSEEK_API_KEY`). **[DSH runtime]**

### How DSH consumes MCP — **critical finding**
- **DSH does NOT read a project-root `.mcp.json` at all.** A full-tree search of the installed DSH runtime found **no `mcpServers` / `.mcp.json` handling** (v0.1.1-rc.2). MCP servers are configured as **Cordis plugin instances** in `cordis.yml`/`cordis.patch.yml` using `@deepseek-ai/dsh-mcp-client`:
  ```yaml
  - insert:
      - id: mcp-matrix
        name: '@deepseek-ai/dsh-mcp-client'
        config:
          serverName: matrix            # [A-Za-z0-9_-]{1,32}, unique
          transport: streamable-http    # or "stdio"
          url: http://localhost:24946/mcp
          headers:                      # optional, e.g. Authorization: Bearer <token>
          failOnStartupError: false     # default false
  ```
  - **Stdio** fields: `command`, `args`, `env` (**ambient env is scrubbed — secrets must be listed explicitly in `env`**), `cwd`. **HTTP** fields: `url`, `headers`. Transport values are exactly `"stdio" | "streamable-http"` (no bare `"http"`). Optional: `toolCallTimeoutMs` (default 60000), `reconnect.*` (default enabled, backoff 500→30000 ms, max 10 attempts). **[DSH runtime — dsh-mcp-client README + types]**
  - Tool naming: `mcp__<serverName>__<rawName>` (Claude-Code/Codex-style), normalized to 64 chars `[A-Za-z0-9_-]` with deterministic hash suffix on collision. Resources/Prompts are NOT bridged — **tools only**. **[DSH runtime]**
  - **Verified live config** (`~/.dsh/profiles/web/cordis.patch.yml`) connecting to the Matrix AI Connector:
    ```yaml
    - insert:
        - id: mcp-matrix
          name: '@deepseek-ai/dsh-mcp-client'
          config:
            serverName: matrix
            transport: streamable-http
            url: http://localhost:24946/mcp
            failOnStartupError: false
    ```
- **Bridge alternative:** the community `dsh-bridges` project runs DSH *under* Claude Code / Codex (which DO read `.mcp.json`), so a `.mcp.json` is still useful in that topology. [dsh-bridges claude-code guide](https://raw.githubusercontent.com/yhlooo/dsh-bridges/main/docs/guides/claude-code.md), [dsh-bridges codex guide](https://github.com/yhlooo/dsh-bridges/blob/main/docs/guides/codex.md).

### How DSH loads skills — **matches `.agents/skills` convention**
- Provider `@deepseek-ai/dsh-skill-filesystem` scans roots in rank order (project root = nearest `.git` ancestor, else cwd):
  | Rank | Source | Path |
  |---|---|---|
  | 100 | project-dsh | `<projectRoot>/.dsh/skills` |
  | 200 | **project-agents** | **`<projectRoot>/.agents/skills`** |
  | 300 | custom | `Config.customSkillDirs` |
  | 400 | user-dsh | `~/.dsh/skills` |
  | 500 | user-agents | `~/.agents/skills` |
- **Format:** `<name>/SKILL.md` directory bundles or flat `<name>.md`; **one level deep only** (no nested `**/SKILL.md`). Frontmatter is YAML: **required `name` (kebab-case) and `description`**; optional `whenToUse`, `metadata`, `disable-model-invocation`, `user-invocable`. `disable-model-invocation: true` hides from the model; `user-invocable: false` hides from human commands. **[DSH runtime — dsh-skill-filesystem README]**
- **What the model sees:** a durable `<system-reminder>` catalog `- <name>: <description>` per skill, refreshed on change; **descriptions are normalized/truncated to `catalogDescriptionMaxLength` = 500 chars default** (XML-escaped). Bodies are loaded on demand via the `skill` tool, rendered as `<skill_content name=...>` + `<skill_instructions>`. **[DSH runtime — dsh-tool-skill README]**
- **Verified:** the Matrix-generated `.agents/skills/*/SKILL.md` files (e.g. `assets-find`) use exactly this format — kebab-case `name`, quoted `description`, and are live-loaded by this DSH session (the available-skills catalog in this session mirrors the folder).

---

## 4. MCP spec & `.mcp.json` conventions

### Canonical shapes
- **Spec (2025-06-18):** transports are **stdio** and **Streamable HTTP**; the older HTTP+SSE transport is deprecated. [MCP Transports spec](https://modelcontextprotocol.io/specification/2025-06-18/basic/transports)
- **`.mcp.json` (Claude Code / Codex convention)** — a project-root JSON with a single `mcpServers` map:
  ```json
  {
    "mcpServers": {
      "Feeder-MCP": {
        "type": "http",
        "url": "http://localhost:24946/mcp",
        "headers": { "Authorization": "Bearer <token>" }
      },
      "some-stdio": {
        "command": "npx",
        "args": ["-y", "@modelcontextprotocol/server-x"],
        "env": { "TOKEN": "..." }
      }
    }
  }
  ```
  - **stdio server:** `command` (required), `args`, `env`, `cwd`.
  - **http (streamable-http) server:** `"type": "http"` + `url` (+ optional `headers` incl. bearer auth); older `"type": "sse"` for legacy.
- **Sources:** [Claude Code MCP docs](https://code.claude.com/docs/en/mcp.md), [FastMCP MCP JSON configuration](https://gofastmcp.com/v3/integrations/mcp-json-configuration), [FastMCP mcp_config reference](https://gofastmcp.com/python-sdk/fastmcp-mcp_config), [Claude Code × Dagu docs](https://docs.dagu.sh/mcp/clients/claude-code), [Claude Code #48514 — streamable-http headers](https://github.com/anthropics/claude-code/issues/48514), [mcp-use client configuration](https://mintlify.wiki/mcp-use/mcp-use/python/client/configuration).
- **Current workspace `.mcp.json` (verified)** is already the canonical http shape:
  ```json
  { "mcpServers": { "Feeder-MCP": { "type": "http", "url": "http://localhost:24946/mcp" } } }
  ```
- **Gap:** DSH speaks `transport: streamable-http` in cordis config, **not** `.mcp.json`. So `.mcp.json` alone does not connect DSH; the DSH bridge entry (§3) must be provided or documented.

---

## 5. Skill authoring for LLM agents (SKILL.md)

- **Anthropic's Agent Skills** (the de-facto standard, mirrored by DSH): a skill is a folder with `SKILL.md` (YAML frontmatter `name` + `description`, body = instructions), plus optional `references/`, `scripts/`, `assets/`. Official guidance: **description is a *trigger/selector* — state what the skill is for and when to use it, keep it concise and action-oriented** (long descriptions dilute the catalog; many agents truncate). [Anthropic engineering — Equipping agents for the real world with Agent Skills](https://www.anthropic.com/engineering/equipping-agents-for-the-real-world-with-agent-skills), [O'Reilly — Agent Skills](https://www.oreilly.com/radar/agent-skills/), [Agent Skills Specification (community)](https://github.com/enuno/claude-command-and-control/blob/main/docs/references/agent-skills-specification.md), [Claude Code SKILL.md validator issue #25380](https://github.com/anthropics/claude-code/issues/25380).
- **Rules that matter for DSH specifically (authoritative):**
  1. Skill root **`.agents/skills`** works out of the box (rank 200); requires a `.git` ancestor to define the project root.
  2. `name` must be **kebab-case** and match the folder name; `description` required; **keep description ≤ ~500 chars** (DSH truncates at 500).
  3. **One directory level deep** — `<root>/<name>/SKILL.md` or `<root>/<name>.md` only; a `MatrixSpace/`-style nested layout will not be discovered.
  4. Optional frontmatter: `whenToUse` (hint), `metadata`, `disable-model-invocation`, `user-invocable`. Invalid `name` spelling or non-boolean invocation values **drop the skill** from discovery (fails closed).
  5. Body should be self-contained (loaded on demand), and can reference sibling `references/scripts/assets` via relative paths resolved against the skill directory.
  6. Keep catalog cost in mind: every skill's description is sent in every session's `<available_skills>` block; 60+ verbose descriptions waste tokens and can crowd the model's attention.

---

## 6. DeepSeek tool-calling — do / don't for tool & skill docs

**DO**
- DO keep **tool schemas flat and explicit**: always a root `{"type":"object","properties":{...}}`; avoid root-level `$ref` (DeepSeek/OpenAI-compat layer rejects or mis-resolves it — [vercel/ai #7924](https://github.com/vercel/ai/issues/7924)).
- DO write **short, action-first descriptions** (`"Find assets by filter string…"`) — they are sent verbatim in every request prefix; shorter descriptions = cheaper requests, better KV-cache reuse, fewer argument mistakes.
- DO put **enum values and constraints inline** in the schema (`enum`, `minimum`, etc.) rather than prose, but keep `enum` lists small — v4-flash has been observed emitting unquoted enum literals on long prompts ([DeepSeek-V3 #1541](https://github.com/deepseek-ai/DeepSeek-V3/issues/1541)); small, simple enums reduce that risk.
- DO prefer **`tool_choice: "auto"`** over forced/required calls in thinking mode (reasoning+forced calls is the most fragile combination — [vercel/ai #10778](https://github.com/vercel/ai/issues/10778), [cline/cline #8365](https://github.com/cline/cline/issues/8365), [DeepSeek-V3 #1448](https://github.com/deepseek-ai/DeepSeek-V3/issues/1448)).
- DO document in the SKILL body that tool calls go **through the connected MCP client** and show a tiny JSON example (as Matrix skills already do).
- DO mention `reasoningEffort`/thinking in docs: with thinking on, the model reasons before each call — instruct it to call tools directly rather than narrating.

**DON'T**
- DON'T rely on `any` types (`"type":"any"` or untyped properties) — unconstrained schemas hurt argument quality.
- DON'T build deep `$defs`/`$ref` graphs in MCP tool schemas; if the schema is generated from C# types (as Matrix does), **flatten or cap the depth** and prefer concrete types over generic containers.
- DON'T put **required credentials/tokens in schema descriptions or skill bodies** (they leak into prompt prefixes and skill catalogs).
- DON'T use **JSON mode** (`response_format: json_object`) as the primary contract — support is uneven across reasoner/thinking models and SDKs ([dify #12713](https://github.com/langgenius/dify/issues/12713)); tool calls or "output valid JSON" instructions are more reliable.
- DON'T set `temperature`/`top_p`/penalties for `deepseek-reasoner`-style thinking models — unsupported/ignored (community confirmed, e.g. [DeepSeek-R1 #436](https://github.com/deepseek-ai/DeepSeek-R1/issues/436)); DSH handles effort via `reasoningEffort` instead. **[DSH runtime]**
- DON'T assume a `.mcp.json` alone connects DSH — it must be mirrored into the DSH profile's `cordis.patch.yml` (or used via a Claude-Code/Codex bridge). **[DSH runtime — verified]**

---

## 7. Checklist — the "DeepSeek" option in Matrix AI Connector

**`.mcp.json` (keep — it's already the canonical Claude-Code/Codex shape):**
- [x] `mcpServers.Feeder-MCP` with `"type": "http"` + `url` (verified in workspace: `http://localhost:24946/mcp`).
- [ ] If auth is enabled on the MCP endpoint, add `"headers": {"Authorization": "Bearer <token>"}` (and warn the user that the token is stored in plain text in `.mcp.json`).

**DSH-specific MCP wiring (the missing piece):**
- [ ] Emit (or document as a one-time step) a **DSH cordis patch** entry for `@deepseek-ai/dsh-mcp-client`:
  ```yaml
  - insert:
      - id: mcp-matrix
        name: '@deepseek-ai/dsh-mcp-client'
        config:
          serverName: matrix
          transport: streamable-http
          url: http://localhost:24946/mcp
          failOnStartupError: false
  ```
  Target file: `~/.dsh/profiles/<profile>/cordis.patch.yml` (web profile in the verified install).
- [ ] If a stdio mode is offered: `transport: stdio`, `command` + `args`, and **explicit `env`** (DSH scrubs ambient env — secrets like `DEEPSEEK_API_KEY` must be listed).
- [ ] Note DSH tool-name convention `mcp__matrix__<tool>` (64-char cap) in the DeepSeek onboarding doc so users recognize tools.
- [ ] Mention `toolCallTimeoutMs` (default 60 s) — long Unity calls (e.g. tests) may need a higher value.

**Skills (already correct — keep and tighten):**
- [x] Output folder `.agents/skills/<kebab-name>/SKILL.md` (DSH rank-200 root; requires `.git` ancestor — TheMatrix has one).
- [x] Frontmatter `name` (kebab-case) + `description` — matches DSH format (verified on `assets-find` skill).
- [ ] **Keep `description` ≤ ~500 chars** (DSH truncates at 500; catalog is re-sent every session — be economical).
- [ ] Consider adding `whenToUse:` hints (optional) for trigger-quality descriptions.
- [ ] Keep skills one directory deep; do not nest `SKILL.md` deeper.
- [ ] Body: short, self-contained, with a tiny JSON input example and a pointer to the MCP connection (already the Matrix pattern).

**DeepSeek model expectations (document in the option's help text):**
- [ ] Default model: `deepseek-v4-flash` (thinking on, `reasoningEffort: high`); alternatives `deepseek-v4-pro`, `deepseek-v4-flash-vision-exp` (1M context). **[DSH runtime]**
- [ ] Env to set: `DEEPSEEK_API_KEY` (required), optional `DEEPSEEK_BASE_URL`.
- [ ] Warn: tool-call JSON can be malformed on very long prompts with forced calls (community reports) — keep tool schemas small/flat, prefer `auto` tool choice.
- [ ] DeepSeek API pricing/limits change frequently — point to [Models & Pricing](https://api-docs.deepseek.com/quick_start/pricing/) rather than hard-coding numbers.

---

## Top sources
- [DeepSeek API Docs — Tool Calls](https://api-docs.deepseek.com/guides/tool_calls/) · [JSON Output](https://api-docs.deepseek.com/guides/json_mode/) · [Models & Pricing](https://api-docs.deepseek.com/quick_start/pricing/) · [Chat Completions API](https://api-docs.deepseek.com/api/create-chat-completion/) · [V4 Preview Release](https://api-docs.deepseek.com/news/news260424/)
- [deepseek-ai/deepseek-harness (official DSH repo)](https://github.com/deepseek-ai/DeepSeek-Harness) · [CLI README](https://github.com/deepseek-ai/deepseek-harness/blob/master/apps/cli/README.md) · [Aliyun DSH docs](https://help.aliyun.com/en/model-studio/deepseek-harness)
- [MCP Transports spec 2025-06-18](https://modelcontextprotocol.io/specification/2025-06-18/basic/transports) · [Claude Code MCP docs](https://code.claude.com/docs/en/mcp.md) · [FastMCP MCP JSON configuration](https://gofastmcp.com/v3/integrations/mcp-json-configuration)
- [Anthropic — Agent Skills](https://www.anthropic.com/engineering/equipping-agents-for-the-real-world-with-agent-skills)
- Quirks: [vercel/ai #7924](https://github.com/vercel/ai/issues/7924) · [vercel/ai #10778](https://github.com/vercel/ai/issues/10778) · [cline/cline #8365](https://github.com/cline/cline/issues/8365) · [DeepSeek-V3 #1448](https://github.com/deepseek-ai/DeepSeek-V3/issues/1448) · [DeepSeek-V3 #1541](https://github.com/deepseek-ai/DeepSeek-V3/issues/1541)
- DSH runtime facts verified locally: `@deepseek-ai/dsh-mcp-client`, `dsh-skill-filesystem`, `dsh-tool-skill`, `dsh-llm-deepseek`, `dsh-agent-default-model` READMEs (v0.1.1-rc.2) + live `~/.dsh/settings.yaml` and `~/.dsh/profiles/web/cordis.patch.yml`.
