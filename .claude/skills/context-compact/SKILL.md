---
name: context-compact
description: Compacts bloated project documents and refreshes indexes, so every later session starts cheaper.
---

# /context-compact

Any phase. Owner: `producer`. Run it when the hooks start warning, or at every second
milestone boundary.

`docs/CONTEXT.md` is read by **every agent, every task**. Every line over the limit is
paid for hundreds of times.

---

## 1. Measure

```powershell
(Get-Content docs/CONTEXT.md | Measure-Object -Line).Lines      # limit 200
(Get-Content docs/DECISIONS.md | Measure-Object -Line).Lines    # limit 300
```

Plus: are the index files current?

```
design/systems/index.md
design/levels/index.md
design/backlog/epics/index.md
docs/architecture/adr/index.md
docs/qa/bugs/  - open bug count
```

An index that is out of date is worse than none: agents trust it and then go looking
anyway, paying twice.

## 2. Compact `docs/CONTEXT.md`

What belongs here is what is true **now**:

| Keep | Cut |
|---|---|
| Pitch, player, platform, stage, milestone | History of how we got here |
| Pillars (copied - the one allowed duplication) | Full pillar rationale, it is in PILLARS.md |
| Core loop, 5 lines | System detail, it is in the SYS docs |
| Stack table with ADR links | Why each was chosen, it is in the ADRs |
| Frame budget headline | The per-system split, it is in PERF-BUDGET.md |
| Current work, blocked items | Finished milestones |
| Live debt and risks | Resolved risks |

The test for every line: **would an agent starting a task tomorrow act differently
without this?** If not, cut it. Nothing is lost - it is in the file it belongs to.

## 3. Compact `docs/DECISIONS.md`

Append-only within a milestone, compacted between them:

- Superseded decisions collapse to one line pointing at the superseding one
- Decisions now permanent in an ADR become a link
- Tuning changes older than two milestones collapse to "economy tuned N times, see git"
- **Never delete a rejected option and its reason.** That is what stops the same debate
  returning every quarter, and it is the cheapest institutional memory available.

## 4. Refresh the indexes

Regenerate each from the files present, one line per item. Indexes are read first and
drilled into second, so an accurate index is a direct token saving on every later task.

## 5. Archive rather than delete

```
docs/archive/<date>/
  context-<date>.md
  decisions-<date>.md
```

Nothing is lost; it is just no longer in the path every agent reads.

## 6. Present

```
## Compacted

| File | Before | After | Limit |
| docs/CONTEXT.md | 284 | 176 | 200 |
| docs/DECISIONS.md | 341 | 198 | 300 |

Indexes refreshed: <n>
Archived to: docs/archive/<date>/

Saving: roughly <n> lines removed from the path every agent reads, every task.
```

## 7. Close

```
✓ Compacted. CONTEXT <n> lines, DECISIONS <n> lines.

▶ Next: back to work - the next session starts cheaper
```

---

## Token note

- **Zero agent calls.** This is editing, and you can see what is stale.
- The saving compounds: 100 lines cut from `CONTEXT.md` is 100 lines not sent with every
  agent invocation for the rest of the project.
