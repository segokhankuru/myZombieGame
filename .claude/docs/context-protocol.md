# Context Protocol - Single Source of Truth

Every fact lives in **exactly one file**. If an agent duplicates a fact elsewhere, that
is a defect. This table decides where a fact lives and who may write it.

---

## SSoT table

| Fact | File | Owner | Readers |
|---|---|---|---|
| Commercial goal, target player, success metrics | `design/00-brief.md` | `studio-head` | everyone |
| Design pillars, tone, fantasy | `design/PILLARS.md` | `creative-director` | everyone |
| Core loop, mechanics, feature list | `design/GDD.md` | `game-designer` | everyone |
| A single system's behaviour (`SYS-*`) | `design/systems/SYS-*.md` | `game-designer` | engineering, QA |
| Every tunable number and its range | `config/balance/*.json` + `config/schema/*.json` | `systems-designer` | engineering, QA |
| Content instances (items, enemies, levels) | `config/content/*.json` | `game-designer`, `level-designer` | engineering |
| Level layout, pacing, teaching order | `design/levels/LVL-*.md` | `level-designer` | engineering, QA |
| Story, characters, dialogue | `design/narrative/` | `narrative-designer` | UI, audio |
| HUD, menus, onboarding, input map | `design/ux/` | `game-ux-designer` | UI programmer, QA |
| Milestone plan and scope | `design/milestones/M-NN.md` | `producer` | everyone |
| Risk register | `design/risks.md` | `producer` | executive |
| System architecture, assemblies | `docs/architecture/ARCHITECTURE.md` | `unity-architect` | engineering, QA |
| Architecture decisions (`ADR-*`) | `docs/architecture/adr/` | `unity-architect` (approval `technical-director`) | engineering |
| Frame and memory budgets | `docs/architecture/PERF-BUDGET.md` | `performance-engineer` | engineering, art |
| Art bible, palette, style rules | `docs/art/ART-BIBLE.md` | `art-director` | art, UI |
| Locked generation style | `.claude/comfy/styles/project.json` | `art-director` | `asset-generator` |
| Asset budget | `docs/art/ASSET-BUDGET.md` | `technical-artist` | art, engineering |
| Asset provenance | `docs/art/ASSET-LOG.md` | append: `asset-generator` | everyone |
| Audio direction and bus map | `docs/audio/AUDIO-BIBLE.md` | `audio-director` | engineering |
| Test strategy | `docs/qa/strategy.md` | `qa-lead` | QA, engineering |
| Playtest findings | `docs/qa/playtests/PT-*.md` | `playtest-analyst` | design, everyone |
| Bugs | `docs/qa/bugs/BUG-*.md` | `test-engineer` | engineering, design |
| Build and release ops | `docs/ops/` | `build-engineer` | everyone |
| Steam page, marketing beats | `docs/publishing/` | `publishing-manager` | `studio-head` |
| Decision log | `docs/DECISIONS.md` | append: everyone | everyone |
| Project summary (the brain) | `docs/CONTEXT.md` | `producer` | **everyone, read first** |
| Machine state | `.state/project.json` | `producer` | skills |

---

## Read map - which role opens what

An agent opens **only** the files on its row. More than that is a budget violation.

```
studio-head          -> CONTEXT.md, 00-brief.md, milestones/, risks.md
technical-director   -> CONTEXT.md, ARCHITECTURE.md, adr/index.md, PERF-BUDGET.md
creative-director    -> CONTEXT.md, PILLARS.md, GDD.md (loop only), playtests/
game-designer        -> CONTEXT.md, PILLARS.md, GDD.md, systems/, config/content/
systems-designer     -> CONTEXT.md, GDD.md (relevant system), config/**, playtests/
level-designer       -> CONTEXT.md, GDD.md (loop), levels/, config/content/levels.json
narrative-designer   -> CONTEXT.md, PILLARS.md, narrative/
game-ux-designer     -> CONTEXT.md, GDD.md (relevant), ux/, ART-BIBLE.md
producer             -> CONTEXT.md, milestones/, risks.md, project.json, story headers
unity-architect      -> CONTEXT.md, GDD.md, ARCHITECTURE.md, adr/, PERF-BUDGET.md
gameplay-programmer  -> story file, config keys in the packet, ARCHITECTURE.md section
systems-programmer   -> story file, config-protocol.md, ARCHITECTURE.md section
graphics-programmer  -> story file, ART-BIBLE.md, PERF-BUDGET.md
netcode-programmer   -> story file, netcode ADR, ARCHITECTURE.md section
ai-programmer        -> story file, relevant SYS-* doc
ui-programmer        -> story file, design/ux/hud.md + the relevant flow, ART-BIBLE.md
tools-programmer     -> story file, config-protocol.md, unity-conventions.md
build-engineer       -> story file, docs/ops/, PERF-BUDGET.md
art-director         -> CONTEXT.md, PILLARS.md, ART-BIBLE.md, comfy-caps
technical-artist     -> ASSET-BUDGET.md, ART-BIBLE.md, asset-index output
asset-generator      -> the asset request, styles/project.json, ASSET-LOG.md
audio-director       -> CONTEXT.md, AUDIO-BIBLE.md, relevant SYS-* doc
qa-lead              -> CONTEXT.md, GDD.md, strategy.md, story headers
test-engineer        -> story file, relevant SYS-* doc, existing tests
code-reviewer        -> the diff, the story, the relevant rules file
performance-engineer -> PERF-BUDGET.md, profiler summary, relevant source
playtest-analyst     -> PILLARS.md, GDD.md (loop), previous playtests, telemetry summary
tech-writer          -> CONTEXT.md, GDD.md, CHANGELOG.md
publishing-manager   -> CONTEXT.md, 00-brief.md, PILLARS.md, docs/publishing/
```

---

## `docs/CONTEXT.md` template

The first file every agent reads. Hard limit 200 lines.

```markdown
# Project Context

**Game:** <name> - <one sentence, the pitch>
**Genre / reference:** <closest three games, and how this differs>
**Target player:** <who, and what they want out of an evening>
**Platform:** <PC / Steam / target spec>
**Stage:** <concept | preproduction | vertical slice | production | polish | ship>
**Milestone:** <M-NN - goal - date>
**Review mode:** <full | lean | solo>

## Pillars
<3-4 one-line pillars from PILLARS.md - copied, this is the exception to no-copying>

## Core loop
<the loop in 5 lines or fewer>

## Deliberately not in this game
<3-5 bullets - the scope wall>

## Technology
| Area | Choice | ADR |
|---|---|---|

## Performance budget
<frame budget, target hardware, worst-case scene - at most 4 lines>

## Active roles
<roster>

## Current work
**Milestone:** <NN> - <goal>
**In progress:** <story list with owners>
**Blocked:** <if any>

## Known debt and risks
<at most 5 lines>
```

---

## Handoff packet

`/handoff` output. Hard limit 200 words.

```
FROM: <role>   TO: <role>   WORK: <story-id>
DONE: <bullets>
REMAINING: <bullets>
DECISIONS: <what was decided and why>
WATCH OUT: <pitfalls, assumptions, scene ownership>
FILES: <paths touched>
VERIFY: <the command the receiving agent should run>
```

---

## Conflict resolution

Two agents want to change the same file:

1. The owner is looked up in the table above.
2. The non-owner presents a **proposal**; it does not write.
3. If the owner accepts, the owner writes. If rejected, the rationale goes in
   `docs/DECISIONS.md`.
4. If the disagreement stands, escalate one level up the authority matrix.

**Scene files are the exception with teeth:** a Unity scene may have exactly one owning
story at a time. `producer` enforces this during `/milestone-plan`, because a scene
merge conflict costs more than the feature.
