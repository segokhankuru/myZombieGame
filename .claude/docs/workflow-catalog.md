# Workflow Catalogue

46 skills across 6 phases. Each row: command, owning role, input, output.

---

## Phase 0 - Bootstrap

| Command | Owner | Input | Output |
|---|---|---|---|
| `/start` | - | - | State detection + the single next step |
| `/help [command]` | - | - | Command list, filtered by current phase |
| `/status [--deep]` | `producer` | Project | Dashboard, blockers, open gates, token note |
| `/kickoff "<idea>"` | `studio-head` | Game idea | `design/00-brief.md`, roster, mode, scaffolding |
| `/onboard` | `unity-architect` | An existing Unity project | `CONTEXT.md`, draft `ARCHITECTURE.md`, debt list |

## Phase 1 - Concept and Design

| Command | Owner | Input | Output |
|---|---|---|---|
| `/concept` | `creative-director` ‖ `studio-head` | Brief | `PILLARS.md`, fantasy, references, the differentiator |
| `/gdd` | `game-designer` | Pillars | `design/GDD.md` |
| `/core-loop` | `game-designer` | GDD | The loop spec + the 30-second, 10-minute and 10-hour experience |
| `/systems-design <name>` | `game-designer` | Loop | `design/systems/SYS-NN-*.md` |
| `/economy` | `systems-designer` | Systems | `config/schema/`, `config/balance/`, `design/economy/curves.md` |
| `/level-design <name>` | `level-designer` | Loop + systems | `design/levels/LVL-NN-*.md`, beat chart, blockout spec |
| `/narrative` | `narrative-designer` | Pillars | Story bible, characters, dialogue with localization keys |
| `/game-ux` | `game-ux-designer` | GDD | HUD spec, menu flows, onboarding, input map, accessibility |
| `/scope-check` | `studio-head` | Backlog | Scope report + the cut list |

## Phase 2 - Technical and Art Direction

| Command | Owner | Input | Output |
|---|---|---|---|
| `/architecture` | `unity-architect` + `technical-director` | GDD + systems | `ARCHITECTURE.md`, assemblies, `PERF-BUDGET.md`, first ADRs |
| `/adr "<question>"` | `unity-architect` | A technical question | `ADR-NNNN-*.md` |
| `/data-schema <domain>` | `systems-designer` + `tools-programmer` | Systems | Config schema, importer, generated ScriptableObject |
| `/netcode-design` | `netcode-programmer` | GDD + architecture | Authority model, replication plan, failure handling |
| `/art-direction` | `art-director` | Pillars | `ART-BIBLE.md` + the locked `comfy/styles/project.json` |
| `/asset-pipeline` | `technical-artist` | Art bible | `ASSET-BUDGET.md`, naming, import presets, postprocessor |
| `/audio-direction` | `audio-director` | Pillars + loop | `AUDIO-BIBLE.md`, bus map, SFX families |

## Phase 3 - Production

| Command | Owner | Input | Output |
|---|---|---|---|
| `/epics` | `game-designer` + `unity-architect` | GDD + milestone | `design/backlog/epics/EP-NN-*/EPIC.md` |
| `/stories <epic>` | `game-designer` + `qa-lead` | Epic | Story files in task-packet form |
| `/milestone-plan` | `producer` | Ready stories | `M-NN.md`: assignment, dependencies, scene ownership |
| `/dev-task [story]` | the relevant programmer | Story file | Code + tests + updated story |
| `/vertical-slice <epic>` | `producer` | Epic | One feature end to end, multi-agent, art and audio included |
| `/gen-asset "<subject>"` | `asset-generator` | Asset request | Generated batch, contact sheet, promotion |
| `/tune <domain>` | `systems-designer` | Config domain | Changed values + the felt-effect statement |
| `/handoff` | any | Work in progress | Handoff packet, at most 200 words |

## Phase 4 - Quality

| Command | Owner | Input | Output |
|---|---|---|---|
| `/code-review [scope]` | `code-reviewer` | Diff | Findings + `CR-CODE` verdict |
| `/qa-run [scope]` | `test-engineer` | Test plan | EditMode + PlayMode run, results, bug records |
| `/playtest [focus]` | `playtest-analyst` | A build | Playtest protocol, observations, `PT-FUN` verdict |
| `/feel-check <feature>` | `creative-director` | A feature | Feel diagnosis against the pillars, fix list |
| `/perf-check [scene]` | `performance-engineer` | Build + budget | Frame budget comparison, bottlenecks |
| `/balance-check <domain>` | `systems-designer` | Config | Curve simulation over a session, break points |
| `/bug "<description>"` | `test-engineer` | Observation | `BUG-NNN.md` + triage |
| `/dod-check [story]` | `qa-lead` | Story + evidence | `QA-DONE` verdict |

## Phase 5 - Release

| Command | Owner | Input | Output |
|---|---|---|---|
| `/build [target]` | `build-engineer` | Project | A build, size and warning report, `OPS-READY` |
| `/steam-prep` | `publishing-manager` + `build-engineer` | Build + brief | Store page copy, capsule brief, tags, achievements, depot plan |
| `/release <version>` | `build-engineer` + `studio-head` | Release scope | Release plan, rollback, `SH-SHIP` verdict |
| `/patch-notes` | `tech-writer` | Story history | Player-facing notes + `CHANGELOG.md` |
| `/hotfix "<issue>"` | `producer` | A live incident | Fast path: reproduce, fix, verify, ship |
| `/retro` | `producer` | Milestone | Retro note + owned actions |

## Utility

| Command | Owner | Purpose |
|---|---|---|
| `/roundtable "<topic>"` | caller | Multi-lens discussion on a hard cross-discipline decision |
| `/context-compact` | `producer` | Compact bloated docs, refresh indexes, cut token cost |
| `/comfy [caps\|status\|jobs]` | `asset-generator` | ComfyUI health, model discovery, job history |

---

## Phase transitions

A phase does not advance until its exit gates return `APPROVED`. `/status` names the
open gate.

```
Phase 0 -> 1 : SH-GREENLIGHT
Phase 1 -> 2 : CD-PILLARS + GD-LOOP + PO-SCOPE
Phase 2 -> 3 : TD-STACK + ARCH-DESIGN + AD-STYLE
Phase 3 -> 4 : PM-PLAN (per milestone)
Phase 4 -> 5 : QA-DONE + PT-FUN + PERF-BUDGET
Phase 5      : OPS-READY + SH-SHIP
```

Can phases be skipped? At `scale=prototype`, `/kickoff` proposes this shortcut:

```
/kickoff -> /core-loop -> /architecture -> /epics -> /stories
        -> /dev-task -> /playtest -> /build
```

`creative-director`, `narrative-designer`, `publishing-manager` and the `full`-mode
gates are disabled. This cuts roughly 60% of the token cost. The one thing it does not
skip is `/playtest` - a prototype whose whole purpose is to answer "is this fun" cannot
skip the step that answers it.

---

## The game-specific workflows

These have no equivalent in an application pipeline and are where this studio earns
its name:

| Command | Why it exists |
|---|---|
| `/core-loop` | A game is its loop. Everything else is decoration on a loop that either works or does not. |
| `/feel-check` | Correct and satisfying are unrelated properties. This is the only step that judges the second one. |
| `/playtest` | The only evidence type that a test suite cannot produce. |
| `/tune` + `/balance-check` | Games are shipped by tuning, not by features. Making tuning cheap is the highest-leverage thing this repo does. |
| `/gen-asset` | An indie team's real constraint is art throughput, not code. |
| `/vertical-slice` | A game is proven by one complete feature, not by many partial ones. |
