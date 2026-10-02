# HyperTesting (Matrix Bridge, experimental)

This folder belongs to **Hyper Testing Mode (Experimental)**: several clone Unity Editors play test this
project in parallel, each driven by its own `hyper-playtester-N` subagent.

Turn it on or off only from **Tools > Feeder > Hyper Testing (Experimental)**. Agents cannot enable it.
The switch is per machine and per project, and every Matrix Bridge package update turns it off again.

| Path | What it holds |
|---|---|
| `config.json` | Clone cap, RAM reserve and per-clone estimate, ports, clone folder, light-editor options |
| `Journal/playbook.md` | The best known play-test flow. Read before every session, update after |
| `Journal/clone-issues.md` | Problems creating, launching, refreshing or stopping clones (`[auto]` lines come from the pool) |
| `Journal/playtest-issues.md` | Problems while play testing: tools, focus, timeouts, flaky steps |
| `Journal/ram-observations.md` | RAM peaks and low-RAM alerts (`[auto]` lines come from the sampler) |
| `Journal/ram-samples.csv` | RAM sampled every 30 s while clones run (ignored by git) |
| `Journal/matrix-improvements.md` | Concrete changes Matrix MCP should make, distilled from the journal |
| `Runs/<run id>/` | Per-run results, screenshots and `SUMMARY.md` (ignored by git) |

Clones live next to this project in `<project>.HyperClones/hc<N>` unless `config.json` says otherwise.
Delete them with the window's **Delete clones** button, never with a recursive delete: `Assets` inside a
clone is a junction to this project's `Assets`.

## config.json

| Key | Default | Meaning |
|---|---|---|
| `maxClones` | 3 | Upper bound for `up` |
| `clonesRoot` | empty | Folder for clones; empty = `<project>.HyperClones` next to the project |
| `basePort` | 27100 | Clone N gets `basePort + N` (or the next free port, step 10) |
| `ramReserveMb` | 4096 | Free RAM that must stay free; `up` stops and the sampler alerts below it |
| `ramPerCloneEstimateMb` | 4096 | RAM one more clone is assumed to need (the measured peak x1.2 wins when larger) |
| `maxParallelCompiles` | 1 | Clones that may refresh/compile at the same time |
| `compileTimeoutSec` | 300 | How long `refresh` waits for one clone |
| `jobWorkerCount` | 2 | `-job-worker-count` for clone editors (0 = Unity default) |
| `belowNormalPriority` | true | Clone editors run at BelowNormal CPU priority |
| `playTargetFrameRate` | 30 | Frame cap in clone play mode (0 = no cap) |
| `playLowestQuality` | true | Quality level 0 in clone play mode, restored on exit |
| `isolatePlayerPrefs` | true | Clone product name gets `_hcN` so PlayerPrefs / save paths do not collide |
| `closeSceneViews` | true | Close Scene views in clones (each one renders a second camera) |
| `ramSampleIntervalSec` | 30 | RAM sampler period |

Changes apply to clones on their next `up` (the marker file is rewritten then).
