---
name: matrix-hyper-testing
description: EXPERIMENTAL Hyper Testing Mode - play test one Unity project in parallel by sending several hyper-playtester-N subagents, each driving its own clone Unity Editor. Use only when the user asks for parallel / hyper play testing AND the hyper-testing tool reports the mode ON. Covers the clone pool (up, refresh, status, down), splitting test cases across clones, RAM watching, collecting results, and writing the experience journal that improves Matrix later.
---

# Hyper Testing Mode (Experimental)

Several clone Unity Editors run next to the original project. Each clone links `Assets/` and the embedded
packages back to the original through directory junctions, so a code change in the original is on disk
in every clone at once. Each clone has its own `Library/`, its own Matrix server port, and a
`hyper-playtester-N` subagent bound to that port. You are the orchestrator: you write and split the test
cases, keep the pool healthy, launch the playtesters in parallel, and turn their results into fixes and
journal entries.

## Rule 0 - The mode is the user's switch, never yours

- If `hyper-testing` (action `status`) says the mode is OFF, stop and tell the user: "Hyper Testing Mode
  is off; enable it in Tools > Feeder > Hyper Testing (Experimental) if you want parallel play testing."
- Never try to enable it: no `script-execute`, no reflection, no EditorPrefs or registry writes, no
  editing of files under `Library/MatrixHyperTesting/`, no hand-made clone markers. The tools refuse
  such attempts. Treat a refusal as final.
- It is experimental. When something behaves unexpectedly, stop that step, write it in the journal
  (Rule 7) and report. Do not improvise workarounds that write into the original project.

## Rule 1 - Read the journal first

Before every session read, in `HyperTesting/Journal/`:

1. `playbook.md` - the flows that worked best so far. Follow them.
2. The last entries of `clone-issues.md`, `playtest-issues.md`, `ram-observations.md` - known traps.
3. `HyperTesting/config.json` - clone cap, RAM reserve, ports.

## Rule 2 - Pool lifecycle

The tool is `hyper-testing` on the original's Matrix server (enable it with `tool-set-enabled-state` if it
is missing from your tool list). Long actions start a background job and return at once; poll
`status` every 30-60 s until the job line says it finished.

| Action | When |
|---|---|
| `status` | Any time. Shows free RAM, every clone's RAM / peak / play / compile state, the job state. |
| `up` + `count` | Start N clones, one after another. A new clone copies `Library` (minutes). A free-RAM gate stops early instead of crashing the machine - respect it, never retry around it. |
| `refresh` | After every code or asset change in the original, once the ORIGINAL compiles clean. Clones refresh and compile one at a time (config `maxParallelCompiles`). |
| `down` | Stop the clone editors, keep their folders warm for next time. |
| `purge` | Stop and delete clone folders. Only when the user asks or a clone is corrupted. |
| `ram` | One RAM sample into `Journal/ram-samples.csv` (a sampler also runs every 30 s while clones run). |

After the first `up` of a Claude Code session, the `hyper-playtester-N` agent files appear in
`.claude/agents/`. If `.claude/agents/` did not exist when this Claude Code session started, the user
must restart Claude Code once before the agents can be spawned - say so instead of guessing.

## Rule 3 - Before launching playtesters

1. The original project compiles clean (read its console). Never test a broken tree.
2. `refresh` and wait: every clone must report `compiled clean`. A clone with compile errors or
   `refresh NOT confirmed` gets no test cases this round - note it in `clone-issues.md`.
3. `status`: every clone `runner active`, not playing, `BLOCKED WRITES` absent. Free RAM above the reserve.
4. `git status --porcelain` of the original - keep the output; you compare it after the run.

## Rule 4 - Test cases and assignment

- One case file per scenario in `TestCases/<yyyyMMdd-HHmm>-<slug>.md`, using the template from the
  `matrix-shared-editor` skill (steps a zero-context agent can run through MCP, exact expected evidence).
- Run id: `<yyyyMMdd-HHmm>-<slug>`. Create `HyperTesting/Runs/<run-id>/`.
- Split cases over the ready clones: longest first, round robin. Cases that wipe the same save data or
  depend on each other go to the same clone, in order.
- Each playtester gets one prompt with: run id, its case paths (absolute), the output folder, the time
  budget (default 20 min), and anything special (scene to open, debug buttons to use).

## Rule 5 - Launch in parallel

Spawn every `hyper-playtester-N` in ONE message, in the background. Prompt skeleton:

```
Run id: <run-id>
Cases (in order): <abs path 1>, <abs path 2>
Write results to: <origin>/HyperTesting/Runs/<run-id>/
Time budget: 20 min. Stop early and report BLOCKED if the clone misbehaves.
Notes: <scene, preconditions, debug buttons>
```

While they run, call `status` every few minutes. If free RAM drops below the reserve or a clone's
working set keeps climbing, note it in `ram-observations.md` with the numbers; do not start more work.

## Rule 6 - Collect and act

1. Each playtester ends with one JSON line: `{"run":..., "clone":N, "case":..., "status":"pass|fail|blocked", "failed":[...], "result":"<path>"}`.
2. Write `HyperTesting/Runs/<run-id>/SUMMARY.md`: table case -> clone -> status -> evidence path, plus
   wall time, RAM peak per clone, problems.
3. `git status --porcelain` of the original again. Any change you did not make = a clone wrote
   through a junction: report it at once and record it in `clone-issues.md` as CRITICAL.
4. Fix failures in the ORIGINAL only (normal coding workflow), compile, `refresh`, re-run only the
   failed cases. Update each case's `Status:` yourself; playtesters never edit case files.

## Rule 7 - The journal (this mode exists to learn)

Append, never rewrite other entries. Entry format:

```
### <yyyy-MM-dd HH:mm> - <who> - <clone or "orchestrator"> - <run id>
- What happened:
- Evidence: (tool output, log line, numbers)
- RAM at the time: free <MB>, clone <MB> / peak <MB>
- Workaround used:
- Suggested Matrix fix:
```

- `clone-issues.md` - creating, launching, refreshing, compiling, stopping clones.
- `playtest-issues.md` - tool failures, focus/throttle stalls, flaky steps, timeouts, wrong evidence.
- `ram-observations.md` - peaks, low-RAM moments, which step caused growth, safe clone count found.
- `playbook.md` - at the end of every session, update "Best known flow" with timings and what to do
  differently next time; move proven tips from "Candidates" into it.
- `matrix-improvements.md` - concrete changes Matrix MCP should make, each linked to journal entries.

## Rule 8 - Finish

- `down` unless the user wants the pool warm.
- Report: cases run per clone, pass/fail/blocked, wall time vs. running them one by one, RAM peak, every
  journal entry you added, and anything not verified.
