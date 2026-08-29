---
name: architecture
description: Designs the assembly boundaries, scene flow, data flow and frame budget, and picks the technology. Runs the TD-STACK and ARCH-DESIGN gates.
---

# /architecture

Phase 2. Owner: `unity-architect` with `technical-director`. Produces
`docs/architecture/ARCHITECTURE.md`, `TECH-STRATEGY.md`, `PERF-BUDGET.md` and the first ADRs.

---

## 1. Inputs

`design/GDD.md` (the system list), the specified `SYS-*` docs, and the target hardware
from the brief. For an existing project, also the `/onboard` scan output.

## 2. Two parallel calls (one message)

### `technical-director` - what technology
```
<the system list with one-line intents>
<target hardware and platform from the brief>
<the hardest constraint answer from /kickoff>
<for an existing project: the current manifest.json and editor version>

Task: technology strategy, at most 30 lines.
1. Render pipeline, and why. URP unless there is a reason.
2. The package list: for each, what it replaces, its runtime cost, and what breaks if it
   is abandoned mid-project. That last column is the normal case, not the edge case.
3. What we deliberately will not use, and what would change our mind. DOTS, custom
   pipelines and bespoke netcode go here unless there is a written reason and an exit plan.
4. Can the target hardware carry the worst-case scene this design implies? If not, that
   is a design decision that has not been made yet - name it.
5. The one-way door in this stack.

Begin with "TD-STACK: APPROVED|CONDITIONAL|REJECTED".
```

### `unity-architect` - what structure
```
<the system list>
<the core loop>
<PILLARS.md>
<for an existing project: asset-index.ps1 -Scripts output>

Task: the architecture.
1. Assemblies: each one's responsibility, its dependencies, and the RATE-OF-CHANGE reason
   it is separate. An assembly with no such reason is decoration - merge it.
2. The one-line dependency rule, enforced by asmdefs. The compiler is the gate; a layer
   diagram is an opinion.
3. Runtime composition: what exists after boot, what is created per level, per entity.
4. The boot contract: exactly what is guaranteed to exist when the Boot scene finishes.
5. Scene graph: which scenes, loaded when, additive or not, what each owns.
6. Data flow: config -> systems -> gameplay -> UI, and where events cross back.
7. Cross-cutting: save/load, config loading, input, scene flow, pooling, audio routing,
   time and determinism.
8. The frame budget divided per system with an owner. An undivided budget cannot be
   enforced by anyone.
9. What you deliberately did not do, and why.

Simplest structure that survives the next 20 stories. Begin with
"ARCH-DESIGN: APPROVED|CONDITIONAL|REJECTED".
```

## 3. Reconcile (you)

Where the two disagree - typically the director wants fewer packages and the architect
wants a cleaner seam - present it as a decision, not a merge. Both are right about
different costs.

## 4. Present

```
## Architecture

Assemblies: <n>
| Assembly | Owns | Depends on | Separate because |
Rule: <the one line>

Boot contract: <what exists after boot>
Scenes: <n>   Additive: <which>

Frame budget (<target> fps = <n> ms)
| System | Budget ms | Owner |

Technology
| Area | Choice | Why | ADR |
Not using: <list>

One-way doors: <list>
ADRs to write: <n>
```

`AskUserQuestion`: `Write it (Recommended)` / `Simplify - too many assemblies` /
`Reconsider a technology choice`

## 5. Write

- `docs/architecture/ARCHITECTURE.md`
- `docs/architecture/TECH-STRATEGY.md`
- `docs/architecture/PERF-BUDGET.md` **including the machine-readable block** that
  `profiler-summary.ps1` parses:

      ```budget
      targetFps=60
      frameMs=16.6
      frameMsP99=22.0
      gcAllocPerFrameKb=0
      drawCalls=1200
      loadSeconds=8
      buildMb=4000
      ```

  Without that block the performance gate has nothing to check against.
- `docs/architecture/adr/index.md` with a row per ADR to write
- The stack summary into `docs/CONTEXT.md`
- Both gate results into `.state/gates.jsonl`

## 6. Create the assemblies

If this is a new project, or an existing one with everything in `Assembly-CSharp`, the
next concrete step is asmdef files. That is a `tools-programmer` story, not something to
do here - but say so, because a documented boundary that the compiler does not enforce
will be gone within a month.

## 7. Close

```
✓ Architecture set. <n> assemblies, <n> ms frame budget across <n> systems.

Riskiest decision: <the one-way door>
ADRs to write: <list>  -> /adr "<question>"

▶ Next: /adr "<the first one>"
   or:   /art-direction   lock the look before assets start
   or:   /epics           if the ADRs can wait
```

---

## Token note

- **Two agent calls, parallel, one message.**
- ADRs are written on demand by `/adr`, not batched here. An ADR written before the
  question is urgent is an ADR written against a design that will change.
