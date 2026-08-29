---
name: scope-check
description: Compares what the game has become against what it was, and produces a cut list. Runs the PO-SCOPE gate. The skill nobody wants to run and everybody needs.
---

# /scope-check

Any phase. Owner: `studio-head`. Run it at every milestone boundary, and any time the
answer to "when will it be done" starts moving away from you.

---

## 1. Measure (free, no agents)

| Source | What to take |
|---|---|
| `design/GDD.md` | system count, and which are `not specified` |
| `design/systems/index.md` | specified vs built |
| `design/backlog/epics/` | story counts by status |
| `design/milestones/` | what was planned vs what happened, last two milestones |
| `docs/DECISIONS.md` | additions since the last scope check |
| `design/backlog/deferred/` | what has already been cut, so it is not re-litigated |
| `.state/agent-log.jsonl` | actual throughput: stories completed per week |

Compute the honest number:

```
Velocity: <stories>/week over the last <n> weeks
Remaining: <n> stories
Projected: <n> weeks   Target: <n> weeks   Gap: <n> weeks
```

That arithmetic is the entire value of this skill. Everything after it is about what to
do with a number nobody wanted.

## 2. One call - `studio-head`

```
<the velocity arithmetic>
<the system list with status>
<what has been added since the last check, from DECISIONS.md>
<PILLARS.md>
<the brief's goals and the "not in this game" list>

Task: scope judgement.
1. What has been added since the last check, and what was removed to pay for it?
   If nothing was removed, say that plainly.
2. Which features exist because they are fun, and which because they seemed expected of
   the genre? The second list is the cut list.
3. Which features does no pillar argue for? Those are free to cut.
4. If the calendar halved tomorrow, what is the version that still ships and still
   answers the brief's goals?
5. The three cuts with the best ratio of weeks saved to experience lost. For each: what
   the player loses, and whether they would notice.

Do not propose "optimise" or "work faster". Cut or move the date - those are the two
levers.
Begin with "PO-SCOPE: APPROVED|CONDITIONAL|REJECTED".
```

## 3. Present the decision

```
## Scope

Velocity <n>/week | Remaining <n> | Projected <n> weeks | Target <n> | Gap <n>

Added since last check: <n> systems, <n> stories
Removed since last check: <n>       <-- the line that matters

Cut candidates
| Feature | Weeks saved | What the player loses | Would they notice | Pillar argues for it |

Recommended: cut <a>, <b>, defer <c>
Result: <n> weeks, gap <closed | still n weeks>
```

`AskUserQuestion`: `Take the recommended cuts (Recommended)` / `Move the date instead` /
`Cut something else` / `Accept the overrun and record it`

The fourth option is legitimate and must be offered. Accepting an overrun knowingly is a
decision; discovering it later is an accident.

## 4. Record

- Cut items move to `design/backlog/deferred/` **with the reason**. That folder is what
  stops the same feature returning in three months as a fresh idea.
- One line in `docs/DECISIONS.md` per cut.
- Update `design/GDD.md`, the milestone plan and `docs/CONTEXT.md`.
- Append the gate to `.state/gates.jsonl`.

## 5. Close

```
✓ Scope checked.
Cut <n> | Deferred <n> | Projected now <n> weeks against <n> target

Next check: <the next milestone boundary>

▶ Next: /milestone-plan   replan against the new scope
```

---

## Token note

- **One agent call.** The arithmetic in step 1 is free and does most of the work.
- Run this on a schedule rather than in a crisis. Scope checked calmly at a milestone
  boundary costs one agent call; scope checked in a panic costs a project.
