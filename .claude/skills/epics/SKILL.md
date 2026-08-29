---
name: epics
description: Breaks a milestone's scope into epics - vertical slices of player-visible value, cut along architecture boundaries.
---

# /epics [milestone]

Phase 3. Owner: `game-designer` with `unity-architect`.
Produces `design/backlog/epics/EP-NN-<slug>/EPIC.md`.

---

## 1. Inputs

`design/GDD.md` system list, the specified `SYS-*` docs, `docs/architecture/ARCHITECTURE.md`
assembly list, and the milestone question if one exists.

## 2. Two parallel calls (one message)

### `game-designer` - value slices
```
<the system list with status>
<the core loop>
<the milestone question, if there is one>

Task: cut this scope into epics.
An epic is a slice of PLAYER-VISIBLE value, not a technical layer. "The delivery loop
works end to end" is an epic; "implement the physics layer" is not.
For each: name, the player-visible outcome, which systems it touches, which pillar it
serves, and a rough size in stories.
Order them by what has to exist before what - and by what would teach us the most if it
were built first.
```

### `unity-architect` - technical shape
```
<the same system list>
<the assembly list and the dependency rule>

Task: for the epics above, name the technical shape.
1. Which assemblies each epic touches. An epic spanning four assemblies is a scheduling
   risk - say so.
2. What contract each epic produces or consumes (config schema, save format, network
   model, API between systems).
3. The order constraint: which epic must land before which, and why.
4. Which epic carries the most technical unknown. That is usually the one to build first,
   not last.
```

## 3. Reconcile (you)

The designer orders by value; the architect orders by dependency and risk. Where they
disagree, the usual right answer is **build the risky thing early** - a value-ordered
plan that discovers its hardest problem in the last week is the standard way projects
slip.

Present the disagreement rather than silently picking.

## 4. Present

```
## Epics - <milestone>

| # | Epic | Player sees | Systems | Assemblies | Stories | Serves |

Order: <the sequence, with the reason>
Highest unknown: EP-<n> - <what is unknown>
Contracts produced: <which epic produces what>
```

## 5. Write

One folder per epic with `EPIC.md` from `.claude/templates/epic.md`, plus
`design/backlog/epics/index.md`. Update `.state/project.json` counters.

## 6. Close

```
✓ <n> epics, roughly <n> stories.

Build first: EP-<n> - <because it carries the unknown / produces the contract>

▶ Next: /stories EP-<n>
```

---

## Token note

- **Two agent calls, parallel, one message.**
- Do not generate stories here. `/stories` produces packets on demand, per epic, so the
  project never carries the cost of specifying work it has not started.
