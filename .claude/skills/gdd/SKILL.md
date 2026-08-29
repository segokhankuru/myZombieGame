---
name: gdd
description: Writes or updates the game design document - systems, verbs, progression, teaching order and what is deliberately not designed.
---

# /gdd

Phase 1. Owner: `game-designer`. Produces `design/GDD.md`.

A GDD in this studio is a **routing table, not a novel**. It says what systems exist,
what each is for, and where the detail lives. The detail lives in `design/systems/`.

---

## 1. Preconditions

`design/PILLARS.md` and the core loop section. If the loop is not written, run
`/core-loop` first - a GDD around an unspecified loop is a wish list.

## 2. One call - `game-designer`

```
<PILLARS.md>
<the core loop section>
<the brief's player and goals sections>

Task: the design document.

1. Verbs: every action the player can take. For each: input, result, and why the player
   would choose it over the alternatives. A verb nobody would choose is not a verb.

2. Systems: the list, each with a one-line intent and the pillar it serves.
   Any system that serves no pillar goes in the "not designed" section instead.

3. Progression and teaching order: the sequence in which mechanics arrive, how each is
   taught, and what it makes possible. The order is a design decision - justify it.

4. Failure: what losing costs, what it teaches, how re-entry works.

5. Content shape: how much content, of what kind, and what the player does when it runs
   out. "More levels" is a plan; "the game ends" is also a plan; "we will see" is not.

6. Deliberately not designed: mechanics considered and rejected, each with the reason.
   Be generous here - this section is what stops the same argument every month.

Keep each system to one line in the GDD. The detail belongs in /systems-design.
```

## 3. Scope reality check (you)

Count the systems. For each, ask what a minimum implementation costs in stories. If the
total exceeds the milestone capacity in `design/milestones/`, say so **now** and route
to `/scope-check`. A GDD that cannot be built is a document that will be quietly ignored,
which is worse than a smaller one that is followed.

## 4. Present and approve

```
## <Game> - Design

Verbs: <n>       Systems: <n>      Estimated: <n> stories
Teaching order: <the sequence in one line>

Systems
| ID | System | Serves | Status |

Not designed
| Rejected | Why |

Scope check: <n> systems against <capacity> - <fits | over by n>
```

`AskUserQuestion`: `Write it (Recommended)` / `Too big - cut first` / `A system is missing`

## 5. Write

`design/GDD.md`. Create `design/systems/index.md` with a row per `SYS-*` at status
`not specified`. Update `docs/CONTEXT.md`. Append to `docs/DECISIONS.md` only where a
real decision was made - not for every system.

## 6. Close

```
✓ GDD: <n> systems, <n> verbs.

Unspecified systems: <n>  -> /systems-design <name>
Specify in this order: <the 2-3 that block everything else>

▶ Next: /systems-design <the first one>
   or:   /economy   if the game lives on its numbers
   or:   /architecture   if the systems are clear enough to structure
```

---

## Token note

- **One agent call.**
- The GDD stays a routing table. Detail per system is generated on demand by
  `/systems-design`, so a project never carries the cost of specifying systems it has
  not started building.
- Do not regenerate the whole GDD to add one system. Append the row and run
  `/systems-design`.
