---
name: release
description: Runs the release checklist, confirms rollback, and asks for the ship decision. Runs OPS-READY and SH-SHIP.
---

# /release <version>

Phase 5. Owner: `build-engineer` and `studio-head`.

---

## 1. Check the release Definition of Done - mechanically

From `.claude/docs/definition-of-done.md`, and from evidence rather than memory:

```
[ ] Every story in the release scope DONE
[ ] Cold boot to first playable input inside the target, on min spec
[ ] A save from the previous public version loads
[ ] Multiplayer: host, late joiner, disconnect, host migration path exercised
[ ] Regression and smoke green ON THE BUILD, not in the Editor
[ ] PERF-BUDGET APPROVED, measured on target hardware
[ ] PT-FUN APPROVED from a session with someone who did not build the game
[ ] Localization complete, no raw keys visible
[ ] Steam depots, achievements, cloud paths, capsules, tags, age rating done
[ ] Rollback written down AND executed at least once
[ ] CHANGELOG and patch notes current
```

For each unchecked item, name what would satisfy it. Do not soften an item because the
date is close - the date is `producer`'s problem, and this list is what stops it becoming
the player's problem.

## 2. Two gates, parallel (one message)

### `build-engineer` - OPS-READY
```
<the build report>
<the DoD check results>
<docs/ops/release.md>

Task: OPS-READY.
- Reproducible on a machine that has never built it?
- Editor version pinned and recorded in the release notes?
- Runs on min spec, clean profile, no Editor installed?
- Previous-version saves load?
- Rollback executed at least once, by someone, recently?
- Depots, branches, achievements, cloud paths configured?
Begin with "OPS-READY: APPROVED|CONDITIONAL|REJECTED".
```

### `studio-head` - SH-SHIP
```
<the DoD check results>
<the latest playtest verdict and its findings>
<the frame budget result>
<the refund-risk answer from /steam-prep>

Task: SH-SHIP.
- Do QA-DONE, PT-FUN, PERF-BUDGET and OPS-READY all hold?
- Did a person who did not build this game reach the end of the first session unaided?
- What will the first hour make a buyer feel? Answer it as a buyer, not as the studio.
- What is the most likely reason for a negative review in week one, and is that
  acceptable?
You are the only role that may say "not yet".
Begin with "SH-SHIP: APPROVED|CONDITIONAL|REJECTED".
```

## 3. Present

```
## Release <version>

DoD: <n>/<n>
| Unchecked | What would satisfy it |

OPS-READY: <verdict>
SH-SHIP:   <verdict>

Rollback: <steps> - last tested <date>
Watch in the first 24h: <what, and the threshold that triggers a hotfix>
```

## 4. The decision

`AskUserQuestion`: `Ship it` / `Hold - <the specific blocker>` / `Ship to a beta branch first`

The third option is usually right for a first release. A beta branch costs a day and buys
the ability to find the thing nobody thought of.

## 5. What happens on approval

Write the release plan to `docs/ops/release.md`. Tag the version. Update `CHANGELOG.md`.
Record both gates in `.state/gates.jsonl`.

**Then stop.** The upload is the user's action:

```
Prepared. The upload is yours to run:
  <the exact steamcmd or ContentBuilder command from docs/ops/release.md>

Nothing in this studio pushes a public build. Rollback if needed:
  <the exact steps>
```

## 6. Post-release

Record what to watch and the threshold that turns an observation into a `/hotfix`:

```
| Signal | Threshold | Action |
| Crash rate | >2% of sessions | hotfix |
| Refund rate | >8% in 48h | investigate the first hour |
| A review naming one thing | 3 times | it is real, not noise |
```

## 7. Close

```
✓ Release <version> prepared.
OPS-READY <verdict> | SH-SHIP <verdict>
Rollback tested <date>

▶ You upload. Then: /retro
```

---

## Token note

- **Two agent calls, parallel.** The DoD check is mechanical and free.
- The gates are worth their cost exactly here: this is the last point at which anything
  is cheap to fix.
