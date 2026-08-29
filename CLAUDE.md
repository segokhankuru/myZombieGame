# Claude Code Unity Studio

This repository is a **virtual game studio** that takes a Unity game from a one-line
idea to a shipped Steam build. Every role is an **agent**, every workflow is a
**skill** (slash command), every phase ends with a **gate**.

> **This is not autopilot.** Agents ask questions, present options with trade-offs,
> show drafts and **request approval before writing**. The final decision is always yours.

---

## Getting started

| Situation | Command |
|---|---|
| New game | `/kickoff "<your game idea>"` |
| Existing Unity project | `/onboard` |
| Where did I leave off? | `/status` |
| Full command list | `/help` |
| Is asset generation live? | `/comfy` |

Typical flow:

```
/kickoff → /concept → /gdd → /core-loop → /systems-design → /economy
        → /architecture → /data-schema → /art-direction → /asset-pipeline
        → /epics → /stories → /milestone-plan
        → /dev-task (loop) ‖ /gen-asset → /vertical-slice
        → /playtest → /feel-check → /perf-check → /dod-check
        → /build → /steam-prep → /release → /retro
```

---

## Constitution (binding for every agent)

1. **One source of truth (SSoT).** A fact lives in exactly one file; everything else
   links to it. Copying is a defect. See `.claude/docs/context-protocol.md`.
2. **Fun is the requirement.** A feature that passes every test and is not fun has
   failed. Design intent (`design/GDD.md`) outranks implementation elegance.
3. **Read before writing** — within budget. See `.claude/docs/token-budget.md`.
4. **Never read Unity YAML by hand.** `.unity`, `.prefab` and `.asset` files are
   machine output. Use `.claude/tools/unity-inspect.ps1`. Reading one whole scene
   can cost more tokens than an entire sprint. See `.claude/docs/unity-conventions.md`.
5. **Tuning lives in JSON, not in the engine.** Every balance number is in `config/`
   and flows into ScriptableObjects. An agent that hardcodes a balance number in C#
   has introduced a defect. See `.claude/docs/config-protocol.md`.
6. **Traceability is mandatory.** Every story links to a system (`SYS-*`), every system
   to a design pillar (`PILLAR-*`), every technical choice to an ADR.
7. **Stay in your lane.** An agent does not decide outside its domain; it *escalates*.
   See `.claude/docs/coordination-rules.md`.
8. **Approval before writing.** Summarize and confirm via `AskUserQuestion` before
   creating or changing files. Exception: code inside an approved story in `/dev-task`.
9. **No "done" without evidence.** For a game, evidence includes a playtest note —
   not just a green test. See `.claude/docs/definition-of-done.md`.
10. **Prefer a script to a paragraph.** Anything mechanical and verbose (asset
    inventory, profiler output, scene structure, ComfyUI graphs, compile errors) goes
    through `.claude/tools/*` and returns a short summary. Agents consume summaries.

---

## Persistent project memory

Kept current every session; the **first thing** every agent reads:

| File | Contents | Limit |
|---|---|---|
| `docs/CONTEXT.md` | One page: what game, for whom, stack, current milestone | 200 lines |
| `.state/project.json` | Machine state: phase, milestone, open gates, roster | — |
| `docs/DECISIONS.md` | Decision log, one line per entry, links to ADRs | 300 lines |

Past the limits, run `/context-compact`.

---

## Directory layout

```
design/      Design layer  — GDD, pillars, systems, levels, narrative, economy, UX
docs/        Technical layer — architecture, ADRs, art bible, audio, QA, ops, publishing
config/      Tuning layer  — balance and content JSON (SSoT for every number)
Assets/      The Unity project (see .claude/docs/unity-conventions.md)
.state/      State machine, gate history, agent log, ComfyUI capability cache
.claude/     Agents, skills, rules, tools, ComfyUI client, templates
```

---

## Roles (29)

**Executive** — `studio-head`, `technical-director`
**Design** — `creative-director`, `game-designer`, `systems-designer`, `level-designer`,
`narrative-designer`, `game-ux-designer`
**Production** — `producer`
**Engineering** — `unity-architect`, `gameplay-programmer`, `systems-programmer`,
`graphics-programmer`, `netcode-programmer`, `ai-programmer`, `ui-programmer`,
`tools-programmer`, `build-engineer`
**Art & Audio** — `art-director`, `technical-artist`, `asset-generator`, `audio-director`
**Quality** — `qa-lead`, `test-engineer`, `code-reviewer`, `performance-engineer`,
`playtest-analyst`
**Support** — `tech-writer`, `publishing-manager`

Full roster, models and authority boundaries: `.claude/docs/agent-roster.md`

---

## Gates and review mode

Every phase ends with a gate; the responsible agent answers `APPROVED` /
`CONDITIONAL` / `REJECTED`. Intensity is set by `design/review-mode.txt`:

| Mode | Behaviour | Cost |
|---|---|---|
| `full` | All gates (commercial release, publisher, multiplayer) | High |
| `lean` | Phase-transition gates only — **default** | Medium |
| `solo` | No gates, single-developer prototype | Low |

Details: `.claude/docs/gates.md`

---

## Asset generation (ComfyUI)

The studio generates its own 2D art, textures, UI and concepts through a **local
ComfyUI** instance. Agents never touch workflow JSON — they call
`.claude/comfy/comfy.ps1`, which returns a few lines. Style is locked once by
`/art-direction` and reused by every later generation.

Details: `.claude/docs/comfy-protocol.md`

---

## Prohibitions

- `git push`, publishing a Steam build, or uploading a depot without approval
- Writing to `.env`, Steam credentials, signing keys, or printing their contents
- Reading a whole `.unity` / `.prefab` / `.asset` file (use the inspector tool)
- Hardcoding a balance number in C# (it belongs in `config/`)
- Adding a Unity package or third-party asset without an ADR
- Inventing design intent — if it is not in the GDD, **ask**
- One agent silently overwriting another's output (use `/handoff`)
