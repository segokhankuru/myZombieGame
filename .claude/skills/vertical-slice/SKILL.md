---
name: vertical-slice
description: Builds one feature end to end - design, code, config, art, audio, UI and test - with every discipline coordinated. The game equivalent of proving a system works.
---

# /vertical-slice <epic>

Phase 3. Owner: `producer` coordinating. The most expensive skill in the studio, and the
one that produces the most certainty per token spent.

A game is proven by one complete feature, not by many partial ones. Run this once early
and it will change the plan; run it late and it will only confirm what you already fear.

---

## 1. Scope it down first

A vertical slice is **one loop iteration at shipping quality**, not a whole epic. Before
anything else, cut it to that:

```
The slice: <the single player action, from input to consequence>
In:  <the minimum that makes it complete and real>
Out: <everything else, explicitly>
```

If the slice takes more than a week, it is not a slice. Cut again.

## 2. Lock the contracts (sequential, cannot be parallelised)

These block everything downstream, so they go first and they go alone:

```
1. unity-architect  -> the assembly and data-flow shape for this slice, plus any ADR
2. systems-designer -> the config keys and starting values
3. art-director     -> confirm the style lock covers what this slice needs
```

Do not start production work while a contract is still moving. Everything built against
a moving contract gets rebuilt.

## 3. Parallel production (one message, multiple calls)

```
Track A  gameplay-programmer   the mechanic, against placeholder art
Track B  asset-generator       the art batch, under the locked style
Track C  ui-programmer         the HUD elements this slice needs
Track D  audio-director        the feedback sounds
```

Each gets a full task packet, exactly as in `/dev-task`. Placeholder art is the schedule:
Track A must never wait on Track B.

Give every track the same slice definition so they build the same thing. That shared
block is also prompt-cache friendly.

## 4. Integrate (mid-slice, never at the end)

```
technical-artist  -> promote the picked art, replace the placeholders
systems-programmer -> wire config, save state if the slice touches it
```

Then run it. Not the tests - **the game**. Someone plays the slice.

## 5. Quality pass (parallel)

```
test-engineer         -> tests for the slice's criteria
code-reviewer         -> CR-CODE across the whole slice, not per story
performance-engineer  -> a capture of the slice running, against the frame budget
```

## 6. The two gates that decide whether this worked

```
creative-director -> FEEL-CHECK  : does it feel like the pillars promised
playtest-analyst  -> PT-FUN      : does a person, unaided, want to do it again
```

These are the point of the exercise. A slice that passes every test and fails
`FEEL-CHECK` has told you something no amount of further production would have.

## 7. Report

```
## Vertical slice: <the action>

Built
  Code <n> files | Art <n> assets | Audio <n> | UI <n> | Config <n> keys
  Tests <passed>/<total>   Review CR-CODE <verdict>
  Frame p50 <x> / <cap> ms, p99 <y> / <cap>

Feel      FEEL-CHECK <verdict> - <the one-line diagnosis>
Fun       PT-FUN     <verdict> - <did they want to do it again>

What this changed our mind about
  <the honest list - this is the deliverable>

Cost
  <n> agent calls, <n> generations, <n> days
  Extrapolated to the full epic: <n> days
```

That last section is what the slice is for. If it changed nothing about the plan, either
the slice was too safe or it was run too late.

## 8. Route the outcome

| Outcome | Route |
|---|---|
| `FEEL-CHECK` rejected | `/feel-check` diagnosis -> `game-designer`, **not** the programmer |
| `PT-FUN` rejected | `/playtest` findings -> `creative-director`; this is a design problem |
| Over budget | `/perf-check` -> `performance-engineer` |
| Cost extrapolation exceeds the milestone | `/scope-check`, now, not later |
| All green | `/milestone-plan` the rest of the epic with real numbers |

---

## Token note

- **8-12 agent calls.** This is the expensive one, and it is worth it exactly once per
  epic, early.
- Contracts sequential, production parallel, quality parallel. The sequencing is the
  whole design: parallelising a moving contract wastes every call downstream of it.
- Never run this on an epic whose contracts are not settled. That is the failure mode
  that turns an expensive skill into a wasted one.
