---
name: economy
description: Designs the curves - reward, cost, progression, difficulty - picks the values, writes the config schema and balance files. Runs the SD-CURVES gate.
---

# /economy

Phase 1. Owner: `systems-designer`. Produces `config/schema/*.json`,
`config/balance/*.json` and `design/economy/curves.md`.

Games are shipped by tuning. This skill exists to make tuning cheap for the rest of the
project, which means the work here is mostly about **ranges and reasons**, not values.

---

## 1. Inputs

`design/GDD.md`, the relevant `design/systems/SYS-*.md` tunable tables, and the intended
session length from the brief. If no system has been specified, run `/systems-design`
first - you cannot balance behaviour that has not been defined.

## 2. One call - `systems-designer`

```
<the tunable tables from the relevant SYS-* docs>
<the core loop and the intended session length>
<the progression and teaching order from the GDD>

Task: design the curves and pick the starting values.

1. For each curve: its shape, why that shape (in player-experience terms), and a table
   of what the player is doing and holding at minute 0, 10, 30 and at the end of the
   intended session.

2. Values for every key, with the range and one sentence on what the player experiences
   outside it. That sentence goes into the schema description and is what stops a later
   agent tuning the fun out of the game.

3. The dominant strategy: what is the mathematically best route, how far ahead of the
   second best is it, and is that gap intentional? Every economy has one - find it
   before a player does.

4. Failure modes: where does the curve collapse? Inflation, a wall the player cannot
   pass, a point where nothing is worth buying, a state they cannot recover from.

5. The three keys most likely to need retuning after the first playtest, and what
   observation would trigger each change.

Begin with "SD-CURVES: APPROVED|CONDITIONAL|REJECTED" judged against your own output.
```

## 3. Sanity-check the curve yourself

Before writing anything, walk the numbers by hand for the first ten minutes of play.
Most balance errors are visible in the first ten minutes and invisible in a spreadsheet.
If the walk contradicts the agent's table, trust the walk and say so.

## 4. Present

```
## Economy

Curves
| Curve | Shape | Minute 0 | Minute 10 | Minute 30 | End |

Dominant strategy: <route> - <n>% ahead of the next - <intentional | needs a fix>

Breaks when: <the condition>

Keys: <n> across <n> domains
Most likely to change after playtest: <3 keys>
```

`AskUserQuestion`: `Write the config (Recommended)` / `The dominant strategy needs fixing
first` / `Change a curve shape`

## 5. Write

For each domain:
- `config/schema/<domain>.schema.json` - types, required, ranges, and a `description` on
  every property
- `config/balance/<domain>.json` - the values, `version: 1`, `_meta.system`

Then **validate**:

```powershell
.claude\tools\config-validate.ps1
```

If it fails, fix it here. A schema that its own balance file violates is worse than none.

Write `design/economy/curves.md`, append the gate to `.state/gates.jsonl`.

## 6. Close

```
✓ <n> keys across <n> domains, all inside their ranges.

The dial to reach for first: <key> - <what it controls>
Watch after the first playtest: <3 keys>

▶ Next: /data-schema <domain>   generate the importer and the ScriptableObjects
   then: /balance-check <domain>  simulate before anyone plays it
```

---

## Token note

- **One agent call.**
- The manual ten-minute walk is free and catches more than a second agent would.
- Writing ranges and reasons now is what makes every later `/tune` a one-line change
  instead of a design discussion.
