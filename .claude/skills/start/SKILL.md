---
name: start
description: Detects what state the project is in and names the single next command. The zero-cost entry point for any session.
---

# /start

Invokes no agents. Reads files, decides, suggests one thing.

## 1. Detect

| Check | Meaning |
|---|---|
| `.state/project.json` missing, `ProjectSettings/` present | Existing Unity project, studio not installed -> `/onboard` |
| `.state/project.json` missing, no `ProjectSettings/` | Nothing here yet -> `/kickoff` |
| `.state/project.json` present | Read `phase`, `milestone`, `reviewMode`, `counters` |

Then check, cheaply:
- `.state/gates.jsonl` last lines -> any open `CONDITIONAL`
- `.claude/comfy/styles/project.json` -> is a style locked
- `design/review-mode.txt` -> mode

## 2. Decide the next step

First match wins:

```
1. No project state                  -> /kickoff or /onboard
2. Open CONDITIONAL gate item        -> the command that closes it
3. A story marked Blocked            -> the escalation, with the target role
4. Phase 0, brief exists             -> /concept
5. Phase 1, no GDD                   -> /gdd    ; no loop spec -> /core-loop
6. Phase 1 complete, no architecture -> /architecture
7. Phase 2, no style lock            -> /art-direction
8. Phase 2 complete, no epics        -> /epics
9. Milestone active, stories ready   -> /dev-task <next on the critical path>
10. Milestone stories done           -> /playtest, then /dod-check
11. Milestone closed                 -> /retro, then /milestone-plan
12. Release scope done               -> /build, then /release
```

## 3. Output

```
<Game> - phase <p>, milestone <m>, mode <mode>
  stories <done>/<total>   open gate items: <n>

▶ <the one command>
  <one sentence on why this and not something else>

Also worth knowing:
  <at most 2 lines - a blocker, a stale style lock, a budget overrun>
```

Never print more than that. `/status` exists for the full picture; `/start` exists to
get moving.

## Token note

Entirely free: file reads only, no agent calls. Running this first every session is the
cheapest thing in the studio and prevents the most expensive mistake - working on the
wrong thing with a full context window.
