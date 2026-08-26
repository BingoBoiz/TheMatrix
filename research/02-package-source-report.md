# Matrix AI Connector (com.feeder.mcp v0.85.0) — Package Source-Code Audit

Audit date: 2026-08-25 · Method: static read/grep on `D:\Unity\TheMatrix\Packages\com.feeder.mcp` + runtime reflection inside the connected Unity Editor (via `script-execute`/`unity-tool-list`/`console-get-logs`). No web research.

Scope: (A) the DeepSeek agent option, (B) the SKILL.md generation pipeline, (C) skill quality samples, (D) prioritized issues.

---

## A. DeepSeek agent option

### A.1 How DeepSeek is injected

- `Editor/Scripts/UI/AiAgentConfigurators/DeepSeekAiAgentConfigurator.cs` — package-side subclass of `AgentConfig.AiAgentConfigurator` (compiled in `Runtime/Plugins/Feeder/Feeder.McpPlugin.dll`). Id `deepseek` (line 27), `SkillsPath = ".agents/skills"` (line 32), `IconName = ""` (line 33), `DownloadUrl = https://www.deepseek.com/` (line 31), `TutorialUrl = https://api-docs.deepseek.com/` (line 35), `TutorialLinkLabel = "DeepSeek Docs"`, `DownloadLinkLabel = "DeepSeek"` (lines 36–37).
- `Editor/Scripts/UI/AiAgentConfigurators/AiAgentCatalog.cs` — injects DeepSeek into the dropdown immediately after Claude Code (lines 49–56). All UI/startup/auto-configure paths go through `AiAgentCatalog` (verified: `MainWindowEditor.AiAgents.cs`, `Startup.cs`, `Startup.AutoConfigure.cs`).
- Runtime reflection confirms the shared registry (`AgentConfig.AiAgentConfiguratorRegistry.All`) has **16 shared agents and does NOT contain deepseek** — it is injected only by this assembly, and there is indeed no runtime registration API on the base class. Shared agents (id → skills path): antigravity `.agent/skills`, claude-code `.claude/skills`, claude-desktop `""` (no skills), cline `.cline/skills`, **codex `.agents/skills`** (same folder as DeepSeek!), cursor `.cursor/skills`, gemini `.gemini/skills`, github-copilot-cli `.claude/skills`, kilo-code `.kilocode/skills`, open-code `.opencode/skills`, rider-junie `.junie/skills`, unity-ai `""`, vs-copilot `.github/skills`, vscode-copilot `.claude/skills`, zoo-code `.roo/skills`, other-custom `.claude/skills`.
- Base-class surface (reflection dump): props `AgentId, AgentName, DownloadLinkLabel, DownloadUrl, IconName, SkillsPath, SupportsSkills, TutorialLinkLabel, TutorialUrl`; methods `BuildLinks, BuildSections, BuildTroubleshootingSections, CreateStdioConfig, CreateHttpConfig, DefaultConfigurationSections, Describe, GetStdioConfig, GetHttpConfig, GetStatus, IsConfigured, IsDetected, ApplyStdioAuthorization, ApplyHttpAuthorization, TroubleshootingSection, ResolveAbsoluteSkillsPath`.

### A.2 Runtime comparison: DeepSeek vs Claude Code (same settings)

Settings built via `AgentConfiguratorSettings.CreateForHost` with `ProjectRootPath=D:\Unity\TheMatrix`, port 24946, timeout 10000, host `http://localhost:24946`, token `test-token-123`, `AuthOption none|required`.

| Surface | Claude Code (shared) | DeepSeek (injected) | Verdict |
|---|---|---|---|
| stdio `args` (auth none) | `port=24946`, `plugin-timeout=10000`, `client-transport=stdio`, `authorization=none` | identical | ✅ |
| stdio `args` (auth required) | + `token=test-token-123` | identical | ✅ |
| stdio `command` | `"D:/Unity/TheMatrix/Packages/com.feeder.mcp/Server/feeder-mcp-server.exe"` (**full path**, i.e. `ExecutableFullPath`) | `"feeder-mcp-server"` (**bare name**, i.e. `ServerExecutableName`) | ❌ **BUG** — see D1 |
| http (auth none) | `{type:"http", url:"http://localhost:24946"}` | identical | ✅ |
| http (auth required) | + `headers.Authorization: "Bearer test-token-123"` | identical | ✅ |
| ConfigPath | `D:\Unity\TheMatrix\.mcp.json` (absolute) | `.mcp.json` (relative) | ❌ **BUG** — see D2 |
| Name | `Claude Code` | `Feeder-MCP` (DefaultMcpServerName) | cosmetic |
| IsDetected / GetStatus vs real `.mcp.json` (http entry) | True / stdio=ReconfigureNeeded, http=Configured | True / identical | ✅ |
| Describe(stdio) sections | custom: Start, Manual Configuration Steps (`claude mcp add …`), Troubleshooting | `DefaultConfigurationSections`: "Configuration" (JSON preview) + Troubleshooting (4 custom bullets, lines 76–88) | OK |
| BuildLinks() | Link('Download'), Link('YouTube Tutorial') | Link('DeepSeek'), Link('DeepSeek Docs') | ⚠️ links never rendered — see D4 |

**Empirical Configure() test** (temp project-root override): DeepSeek's `Configure()` wrote `.mcp.json` into the **process CWD** (D:\Unity\TheMatrix — because Unity's CWD is the project root) instead of the settings `ProjectRootPath`. Claude's absolute ConfigPath wrote to the correct temp folder. So DeepSeek currently works only because Unity's CWD happens to be the project root; in batch/CI or editor launched from another directory the file lands in the wrong place. (The audit temporarily polluted the real `.mcp.json` with a stdio entry; it was restored to the original http entry.)

### A.3 UI integration paths (all verified in source)

- `Editor/Scripts/UI/Window/MainWindowEditor.AiAgents.cs` — dropdown = `AiAgentCatalog.GetAgentNames()` (line 38); default selection Claude Code (lines 52–55); on agent switch with auto-generate on, sets `SkillsPath = agent.SkillsPath` and regenerates (lines 141–146).
- `Editor/Scripts/Startup.cs` — startup skill generation for the *selected* agent, fallback `claude-code` (lines 33–41). DeepSeek only generates once selected **and** its auto-generate flag is on.
- `Editor/Scripts/Startup.AutoConfigure.cs` — writes `.mcp.json` per agent in `AgentAutoConfigure` (lines 41–52). Config currently has `agentAutoConfigure: {claude-code: true, codex: true}` → DeepSeek never auto-configured (expected: opt-in, but note the zero-setup path skips it).
- `Editor/Scripts/UI/AiAgentConfigurators/AiAgentConfiguratorView.cs` — `IconName == ""` → icon slot hidden (lines 173–178), so DeepSeek's empty icon is handled cleanly; Configure/Remove status row works for DeepSeek (`HasDetectableConfig`, line 220 — DeepSeek is not the `CustomConfigurator`); skills section uses `SkillsPath` (`.agents/skills`) and `IsAutoGenerateSkills("deepseek")` (lines 511–554).
- `Editor/Scripts/UI/AiAgentConfigurators/AgentConfiguratorSettingsFactory.cs` — maps Unity state; in `Custom` connection mode `host` becomes `McpServerManager.GetMcpEndpointUrl(Host)` (→ `http://host:port/mcp`), so DeepSeek's http `url` gets `/mcp` (matches current `.mcp.json`). ✅

### A.4 Config / repo state

- `UserSettings/Feeder-MCP-Config.json`: `skillsPath = ".claude/skills"`, `skillAutoGenerate = {claude-code: true}` only, `agentAutoConfigure = {claude-code, codex}`, `transportMethod = streamableHttp`, `authOption = none`, `host = http://localhost:24946`, token present (redacted). **No deepseek keys anywhere** → DeepSeek never enabled in this project.
- `.mcp.json` (project root): `{mcpServers.Feeder-MCP: {type: http, url: http://localhost:24946/mcp}}` — no tokens. Written by Claude/http auto-configure.
- `git status`: `DeepSeekAiAgentConfigurator.cs` + `AiAgentCatalog.cs` **untracked** (fork additions, not committed); `Startup.cs`, `Startup.AutoConfigure.cs`, `MainWindowEditor.AiAgents.cs` modified; `Matrix~/Docs/design/parallel-lanes.md`, `PLAN-DeepSeek-Matrix-Review.md` untracked. `git log`: last commit `a86f621 "Add Codex backend, session persistence and reload coordination"` — DeepSeek is a fresh layer on top of Codex support.
- `Editor/Scripts/MatrixSpace/AgentBackendCatalog.cs` — separate Matrix chat-pane backend list (claude, codex, gemini, kimi, cursor, custom). **No DeepSeek preset** → DeepSeek is only a config-file option; the Matrix pane cannot chat with DeepSeek.

---

## B. Skills pipeline

### B.1 Generator surface (package source)

- `Editor/Scripts/API/SystemTool/Skills.cs` — `[AiToolType] partial class Tool_Skills` (empty shell).
- `Skills.Generate.cs` — `unity-skill-generate`, **`Enabled = false`** (line 19). `GenerateAll(string? path)` validates relative/no-`..` path, temporarily swaps `SkillsPath` and calls `McpPluginInstance.GenerateSkillFiles(...)` (lines 43–86). Actual generator lives in `Feeder.McpPlugin.dll` (not in package source).
- `Skills.Create.cs` — `unity-skill-create`, **`Enabled = false`** (line 21). Writes a `.cs` under `Assets/`, validates syntax, `Processing` + post-compilation notification (lines 213–256).
- `Skills.Create.SkillBody.cs` — the shared `SkillsCreateSkillBody` const (full sample + 5 suggestion sections).

### B.2 Inventory (runtime + disk)

- **Registered tools in the running Unity Editor** (`unity-tool-list`): **78 tools**. Every one of the 78 has a `SKILL.md` in both `.agents/skills` and `.claude/skills` ✅.
- **On disk**: 83 skill folders per location (166 SKILL.md total). The extra 5 folders with **no matching registered tool**:
  - `ping`, `unity-skill-create`, `unity-skill-generate` — tools declared `Enabled = false`; **they still get generated SKILL.md** (their files are in the standard generated format with "How to Call" + schema sections), but they are **not registered with the MCP server** (absent from `unity-tool-list`). → An agent that loads these skills will call a non-existent tool. (In this very session the harness listed `unity-skill-create`/`unity-skill-generate` as available skills while the server doesn't expose them.)
  - `matrix` (hand-written persona skill), `feeder-mcp-initial-setup` (hand-written checklist) — non-tool skills, not generated.
- **All 78 registered tools have `[AiSkillDescription]`**; 86 grep hits across `Editor/Scripts/API` = ~81 unique tool files + 5 `pre-Unity.6.5.cs` duplicates, all guarded by `#if UNITY_6000_5_OR_NEWER` / `#if !UNITY_6000_5_OR_NEWER` (checked `Assets.Modify.cs`, `Editor.Selection*.cs`, `GameObject*.cs`) — no duplicate tool registration.
- **`.agents/skills` vs `.claude/skills`: byte-identical — 0 missing files, 0 content differences** (verified by file-hash comparison; parent independently confirmed).

### B.3 Disabled-tool handling

`Enabled = false` on `[AiTool]` (unity-skill-generate, unity-skill-create, ping) does **not** stop SKILL.md generation. There is no package-side filter (generator is in the DLL), so disabled tools produce "dead" skills. Suggested fix: skip `Enabled=false` tools in the generator, or keep them registered so the skill is callable (inconsistency: skill present ↔ tool absent).

### B.4 Special non-tool skills

- `.agents/skills/matrix/SKILL.md` (84 lines) — high quality: identity, Matrix canon grounding, Unity→Matrix terminology table, voice rules, hard rules protecting technical accuracy. Note line 65 embeds a Vietnamese phrase ("Vâng, thưa The Architect") — persona assumes a Vietnamese-speaking user.
- `.agents/skills/feeder-mcp-initial-setup/SKILL.md` (32 lines) — decent generic checklist (open connector, configure client, generate skills, troubleshooting). Slightly stale wording ("AI agent dropdown", "Copy or apply the generated MCP configuration") but accurate.

### B.5 Git/regeneration state

- `.agents/skills/matrix/SKILL.md` and `.agents/skills/unity-skill-generate/SKILL.md` are **modified**; `ui-inspect-element/issues/pick/tree/windows` folders are **untracked** (newly generated). So the on-disk skills were regenerated recently (someone ran the generator with an `.agents/skills` override), yet the persisted config still points at `.claude/skills` with only `claude-code` auto-generate — the two folders are currently in sync only because both were generated, not because the config drives both.

---

## C. Skill quality samples

All generated SKILL.md files share a template: YAML front matter (`name`, `description` from `[AiSkillDescription]`) → `# <AiTool Title>` → `[AiSkillBody]` text → auto-generated `## How to Call` (example JSON) → `## Input` table → `### Input JSON Schema` → `## Output` (+ schema). Sampled: gameobject-find, assets-modify, tests-run, script-execute, scene-save, ui-inspect-tree, screenshot-isolated, reflection-method-find, tool-set-enabled-state, ping, unity-skill-generate.

**Body quality (C# `[AiSkillBody]` ↔ SKILL.md): consistent** — verified `assets-modify` (Assets.cs `ModifySkill.Description/Body`, lines 48–65) and `unity-skill-generate` bodies match their SKILL.md verbatim. Best bodies: tests-run (dirty-scene precondition, domain-reload handling), script-execute (mode + parameter table), screenshot-isolated (side-effect caveat for OnEnable), gameobject-find (path-scoped reads).

**Generator flaws (all in the compiled generator, not package source):**

1. **"How to Call" example JSON uses `"string_value"` for every parameter regardless of type** — e.g. `screenshot-isolated`: `"isolated": "string_value"`, `"fieldOfView": "string_value"`, `"resolution": "string_value"` (bools/numbers as strings); `tool-set-enabled-state`: `"includeLogs": "string_value"`; `gameobject-find`: `"paths": "string_value"`, `"viewQuery": "string_value"` (objects/arrays). Misleading for complex ref types (`assetRef`/`gameObjectRef` should show `{"instanceID": N}`).
2. **Example values don't match documented defaults** — `ui-inspect-tree` example `"maxDepth": 0` (default 12); `tests-run` example `"includeMessages": false` (default true); `reflection-method-find` example `typeNameMatchLevel/methodNameMatchLevel: 0` (body says default 1).
3. **Schema `$defs` bloat + CLR generic-name leaks** — `System.String-1` (assets-modify output = `List<string>`), `AIGD.ComponentDataShallow-1`, `AIGD.ToolToggleInput-1`, `System.Boolean` wrapped as a `$ref` (tool-set-enabled-state input), full `SerializedMember`/`SerializedMemberList` trees duplicated into every schema, input and output schema blocks repeat the same `$defs`. `gameobject-find` output schema is ~400 lines mostly `$defs` duplication.
4. **Input tables type everything non-primitive as `any`** (gameObjectRef, content, jsonPatch, filter, parameters…), forcing the reader to the schema anyway.
5. Minor: `unity-skill-generate` body examples reference `.claude/skills` even though the DeepSeek/Codex target is `.agents/skills` (parameter is generic — fine, but the example biases the wrong folder).

---

## D. Prioritized issues

### P0 — critical

- **D1. DeepSeek stdio config writes a bare executable name → stdio entry cannot launch.**
  `DeepSeekAiAgentConfigurator.cs:45` uses `settings.ServerExecutableName` (`"feeder-mcp-server"`); Claude Code writes `settings.ExecutableFullPath` (the package-local `.exe` is not on PATH). The code comment (lines 90–94) claims the entry is "byte-identical" to Claude Code — false (verified via reflection: only `command` differs). **Fix:** use `settings.ExecutableFullPath` in `CreateStdioConfig`.

### P1 — should fix

- **D2. DeepSeek config uses relative `ConfigPath = ".mcp.json"` → writes land in CWD, not the project root.**
  `DeepSeekAiAgentConfigurator.cs:43` and `:60`. Empirically verified: `Configure()` wrote to `D:\Unity\TheMatrix\.mcp.json` (process CWD) while settings said a temp `ProjectRootPath`. Works only because Unity's CWD == project root. **Fix:** `Path.Combine(settings.ProjectRootPath, ".mcp.json")` (Claude's config already yields an absolute path).
- **D3. Disabled tools still get generated SKILL.md (ping, unity-skill-create, unity-skill-generate) but aren't registered in the MCP server** → dead skills agents may call. `Skills.Generate.cs:19`, `Skills.Create.cs:21` (`Enabled = false`) vs their on-disk SKILL.md. **Fix:** skip `Enabled=false` tools in the generator (or keep them registered).
- **D4. Download/Tutorial links never render in this fork.** `AiAgentConfiguratorView.cs:273–276` returns `null` for `ConfigurationItemKind.Link` ("intentionally not rendered"). DeepSeek's `DownloadUrl`/`TutorialUrl` (and every agent's) are invisible; also the view never calls `BuildLinks()`. **Fix:** implement Link rendering (open-URL button).
- **D5. Double "Reconfiguration Required" alert risk.** The DTO `Describe()` already emits a `Reconfiguration Required` Alert section (seen in the runtime dump), and `AiAgentConfiguratorView.cs:426–431` renders its own `_reconfigureAlertPanel` on the same `GetStatus == ReconfigureNeeded` condition (fork-wide, not DeepSeek-specific). **Fix:** dedupe (verify visually; render one of the two).

### P2 — nice to have

- **D6. DeepSeek not reachable from the Matrix chat pane** — `AgentBackendCatalog.cs:60–153` has no deepseek preset (only claude/codex/gemini/kimi/cursor/custom); DeepSeek exists only in the agent-config dropdown. **Fix:** add a `deepseek` preset (generic backend, `SupportsExecResume=false`) if in-scope.
- **D7. Zero-setup path never enables DeepSeek** — `Startup.cs:33–41` and `Startup.AutoConfigure.cs` only act on saved-selection/`SkillAutoGenerate`/`AgentAutoConfigure`, and the config has only `claude-code`/`codex`. Expected opt-in, but the DeepSeek first-run experience requires opening the window and enabling skills manually. **Fix:** optionally pre-seed `skillAutoGenerate.deepseek` when a deepseek selection is made.
- **D8. "How to Call" placeholder JSON is type-agnostic (`"string_value"` everywhere) and example values mismatch defaults** — generated output of the DLL generator. **Fix:** type-aware placeholders (true/false/0/`{}`/`[]`/`{"instanceID": 0}`) using the schema + defaults. Upstream, not package-source.
- **D9. Schema `$defs` bloat + CLR generic names (`System.String-1`, `…-1` arity suffixes, `System.Boolean` as `$ref`)** — generator quality. **Fix:** friendly `$defs` naming + dedupe across input/output. Upstream.
- **D10. DeepSeek brand polish** — `DownloadUrl` points to the generic homepage (DeepSeek ships no CLI; if the intended client is DeepSeek Harness, label/URL should say so); no icon asset (`IconName=""` is handled by the view but the slot is empty next to other agents' 64px icons). **Fix:** ship `Editor/Gizmos/ai-agents/deepseek-64.png` and point URLs at the actual client.
- **D11. `.agents/skills` is shared with Codex** (registry: `codex → .agents/skills`). Not a bug today (folders byte-identical), but `SkillsPath` is a single global config value that flips whenever an auto-generate agent is switched (`MainWindowEditor.AiAgents.cs:143`), so the persisted `skillsPath` alternates between `.claude/skills` and `.agents/skills` depending on the last-switched agent. **Fix:** keep per-agent skills path mapping instead of one global `SkillsPath` (or document the behavior).

---

## Appendix — verification notes

- Runtime tool list and reflection were captured from the live Unity Editor via the MCP bridge (`script-execute` → console logs); DeepSeek type loaded as `Feeder.MCP.Editor.UI.DeepSeekAiAgentConfigurator` ✅.
- `AgentConfiguratorSettings` shape: `CreateForHost(projectRootPath, executableFullPath, port, timeoutMs, host, token, connectionMode, authOption, serverExecutableName, serverVersion, dockerImage)`; `AuthOption` enum = `Feeder.McpPlugin.Common.Consts.MCP.Server.AuthOption {unknown, none, required}`.
- Secrets: token values redacted; not reproduced in this report.
- Uncertainties: D5 (double alert) is inferred from code + DTO dump, not visually confirmed; D8/D9 fixes are upstream (DLL) — only mitigable from the package side.
