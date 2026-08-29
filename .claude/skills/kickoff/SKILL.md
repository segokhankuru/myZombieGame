---
name: kickoff
description: Starts a new game. Frames the commercial goal with the studio head, picks the roster and review mode from scale, and creates the directory structure and state files.
---

# /kickoff "<game idea>"

Phase 0. Produces `design/00-brief.md`, `.state/project.json`, a `docs/CONTEXT.md`
skeleton, the folder structure and the chosen roster.

---

## 1. Take the input verbatim

No argument -> ask: *"What do you want to make? One sentence."*
Keep the answer **verbatim**. Do not interpret, expand or improve it yet.

## 2. Clarification round - free, no agents

Four questions in a **single** `AskUserQuestion` call. These decide roster and mode.

**1 - What kind of game**
`Single-player PC` / `Co-op or multiplayer` / `Mobile` / `Prototype - genre undecided`

**2 - Scale**
- `Prototype` - answering "is this fun", one person, weeks
- `Indie (Recommended)` - a real release, small team, months
- `Commercial` - Steam launch with marketing, publisher or funding at stake

**3 - Where the game lives or dies**
- `The moment-to-moment feel` - action, movement, physics
- `The systems` - economy, progression, simulation
- `The content` - levels, story, world
- `The social loop` - co-op, competition, sharing

**4 - The hardest constraint**
- `Time` - it has to exist soon
- `Art` - one person, and art is the bottleneck
- `Performance` - many entities, or weak target hardware
- `Multiplayer` - it must be networked and that is not negotiable

## 3. Derive roster and mode

| Scale | Mode | Base roster |
|---|---|---|
| Prototype | `solo` | game-designer, unity-architect, gameplay-programmer, systems-programmer, technical-artist, test-engineer, producer |
| Indie | `lean` | + creative-director, systems-designer, level-designer, game-ux-designer, ui-programmer, art-director, asset-generator, audio-director, qa-lead, code-reviewer |
| Commercial | `full` | Full roster (29) |

Answer 3 adjusts it:
- `feel` -> `playtest-analyst` active at every scale; `/feel-check` runs in lean
- `systems` -> `systems-designer` active at every scale
- `content` -> `level-designer`, `narrative-designer` active
- `social` -> `netcode-programmer` active, `NET-MODEL` gate on

Answer 4 adjusts it:
- `Time` -> mode drops one level; `/scope-check` scheduled at every milestone
- `Art` -> `art-director` + `asset-generator` active at every scale, `/art-direction` early
- `Performance` -> `performance-engineer` active, `PERF-BUDGET` gate on from phase 2
- `Multiplayer` -> `netcode-programmer`, `NET-MODEL` gate on

Show it and confirm:
```
Roster: <list>
Mode: <mode> - <one sentence on what that means for gate cost>
Disabled: <list> - <why>
Changeable later in design/review-mode.txt.
```

## 4. Commercial framing - `studio-head`, one call

Embed everything; have it read nothing.

```
Idea: <argument verbatim>
Kind: <a1> | Scale: <a2> | Lives or dies on: <a3> | Hardest constraint: <a4>

Task:
1. Who specifically plays this, and what are they playing instead tonight?
2. At most 3 measurable goals (GOAL-01..03): target, how measured, when.
3. At least 5 things that will NOT be in this game. Make at least two of them painful.
4. The 3 riskiest assumptions, and how each gets tested BEFORE the expensive work.
5. The failure scenario in one paragraph: the most likely way this project dies.

Be brief. Begin with "SH-GREENLIGHT: APPROVED|CONDITIONAL|REJECTED".
If something is genuinely unclear, do not assume - open a "QUESTION:" line.
```

If it returns `QUESTION:` lines, ask the user, then send the answers back **once**.
Two rounds maximum.

## 5. Present and get approval

```
## <Game name>
<one sentence>

Player: <who, and what they play instead>

Goals
  GOAL-01: <measurable> - <how measured>

Not in this game
  - <item> - <why>

Riskiest assumptions
  - <assumption> -> tested by <how>, before <when>

Dies most likely by: <one line from the failure scenario>

Roster: <n> roles  |  Mode: <mode>
```

`AskUserQuestion`: `Approve and set up (Recommended)` / `Change the goals` /
`Change roster or mode`

## 6. Create files (after approval, one Bash call for the folders)

```
design/{systems,levels,narrative,economy,ux,milestones,backlog/epics,backlog/deferred}
docs/{architecture/adr,art,audio,qa/{playtests,evidence,bugs,performance},ops,guides,publishing}
config/{schema,balance,content}
.state
```

Then write:
- `design/00-brief.md` - from the studio-head output, in the template shape
- `design/review-mode.txt` - one line: `<mode>`
- `design/risks.md` - the risky assumptions as register entries
- `docs/CONTEXT.md` - the template from `context-protocol.md`, unknowns as `<tbd>`
- `docs/DECISIONS.md` - first row: scale and mode, decided by the user
- `.state/project.json`:

```json
{ "project":"<name>", "phase":"concept", "reviewMode":"<mode>", "scale":"<scale>",
  "activeRoles":[...], "milestone":null, "openGateConditions":0,
  "stack":{"unity":"<version if a project exists>","pipeline":null},
  "counters":{"epics":0,"stories":0,"done":0,"bugs":0,"generations":0},
  "lastUpdated":"<today>" }
```
- `.state/gates.jsonl` - the SH-GREENLIGHT verdict as line one

## 7. Close

```
✓ <Game> is set up.
Goals GOAL-01..<n>  |  Roster <n>  |  Mode <mode>

▶ Next: /concept
   The creative director turns this into pillars that can reject a feature.
```

If the answer to question 4 was `Art`, add:
```
   Also early: /art-direction - lock the generation style before making any assets.
```

---

## Token note

- **One agent call** (`studio-head`), at most two rounds.
- Clarification goes to the user, not to an agent. That is free.
- Folders in a single Bash call.
- Do not draft the GDD here. `/kickoff` frames the business question; `/concept` and
  `/gdd` answer the design one, in a fresh session.
