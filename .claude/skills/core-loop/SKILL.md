---
name: core-loop
description: Specifies the loop the player repeats, across three time scales, and what mastery looks like. Runs the GD-LOOP gate. The most important twenty minutes in the project.
---

# /core-loop

Phase 1. Owner: `game-designer`. Produces the loop section of `design/GDD.md`.

A game is a verb the player repeats and gets better at. Everything else is scaffolding.
If this document is wrong, no amount of production quality fixes it.

---

## 1. Inputs

`design/PILLARS.md` and `design/00-brief.md`. For an existing project, this skill is
often the first time the loop has ever been written down - that is normal and valuable.

## 2. One call - `game-designer`

```
<PILLARS.md, embedded>
<the brief's pitch and player sections, embedded>
<for an existing project: the user's description of what the game currently does>

Task: specify the core loop.

1. The loop in at most 5 lines. If it needs more, it is not a loop yet - say so and
   give the closest 5-line version, marking what you had to drop.

2. The three time scales:
   | Scale | What the player is doing | What they are getting better at | What changes |
   | 30 seconds | | | |
   | 10 minutes | | | |
   | 10 hours | | | |
   The 10-hour row is where most games have nothing. If this one has nothing, say so
   plainly rather than inventing a progression system.

3. The decision at the centre. In the 30-second loop, what is the player choosing
   between, and why is neither option obviously correct? If there is no real choice,
   name what would create one.

4. Mastery: what does a good player do that a new player does not? Be concrete -
   "plays better" is not an answer.

5. Failure: what does losing cost, what does it teach, and how does the player re-enter?

6. The minimum playable: the smallest version of this loop that could be built and
   played this month. Name what is in it and what is not.

7. The three riskiest assumptions in this loop, and how a playtest would test each.

Begin with "GD-LOOP: APPROVED|CONDITIONAL|REJECTED" judged against your own output.
```

## 3. Pillar check (you)

For each pillar, ask: **does the loop express it, or merely not contradict it?** A loop
that is compatible with the pillars but expresses none of them is the most common way a
game ends up generic. Report gaps rather than smoothing them over.

## 4. Present

```
## Core loop
<the 5 lines>

## The decision
<what the player weighs, every 30 seconds>

## Mastery
<what a good player does>

## Time scales
30s: <...>  |  10min: <...>  |  10h: <...>

## Minimum playable
<what gets built first>

## Riskiest assumptions
<3, each with the playtest that would settle it>

## Pillar expression
PILLAR-1 <expressed | only not contradicted> - <where>
```

`AskUserQuestion`: `Write it (Recommended)` / `The decision is not real yet` /
`The 10-hour scale is empty - address that first`

## 5. Write

Into `design/GDD.md` (creating it if needed), and the loop copied into
`docs/CONTEXT.md` under `## Core loop`. Gate result to `.state/gates.jsonl`.

## 6. Close

```
✓ Core loop specified.

The bet: <the single riskiest assumption>
  Settled by: <the playtest that would answer it>

▶ Next: /gdd            flesh out the systems around this loop
   or:   /architecture  if the minimum playable is small enough to start building now
```

For a prototype, going straight to `/architecture` is often correct. The loop is the
hypothesis; building it is the experiment. Do not write forty pages of GDD first.

---

## Token note

- **One agent call.** The pillar check is yours and costs nothing.
- Keep the output short by design. A five-line loop that a programmer can build beats a
  three-page description of a feeling.
