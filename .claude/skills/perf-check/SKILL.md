---
name: perf-check
description: Measures the frame budget on a real build and reports what is over, with an owner. Runs the PERF-BUDGET gate. Measures rather than estimates.
---

# /perf-check [scene]

Phase 4. Owner: `performance-engineer`.

---

## 1. Get a capture, or stop

```powershell
.claude\tools\profiler-summary.ps1
```

If there is no capture, the answer is not "read the code and estimate". Nobody has ever
guessed a bottleneck correctly often enough to be worth the tokens. Add the capture
harness instead:

```csharp
// Assets/_Project/Code/Systems/Debug/PerfCapture.cs - runtime, behind a debug flag
// Records frameMs per frame plus GC delta, draw calls and setPass, then writes
// docs/qa/performance/<date>-<scene>.json:
//   { "scene": "...", "hardware": "...", "frameMs": [16.2, 16.8, ...],
//     "gcAllocPerFrameKb": 0.0, "drawCalls": 940, "setPassCalls": 210,
//     "triangles": 1200000, "loadSeconds": 6.2 }
```

If it does not exist, that is a `systems-programmer` story and it takes an hour. It pays
for itself the first time it runs.

## 2. Capture correctly

The measurement conditions are most of the result:

- **On a build, not in the Editor.** Editor overhead flatters and misleads in different
  places.
- **On target hardware**, or say explicitly which machine it was.
- **The worst-case scene**, following the busiest route a player takes, for at least 60
  seconds. A capture of the menu proves nothing.
- With the same route every time, so runs are comparable.

## 3. Compare against the budget

```powershell
.claude\tools\profiler-summary.ps1 -Capture docs/qa/performance/<file>.json
.claude\tools\profiler-summary.ps1 -BuildSize
```

The tool reads the machine-readable block from `docs/architecture/PERF-BUDGET.md`. If
that block is missing, the budget was never set - route to `/architecture` rather than
inventing one.

## 4. Investigate what is over - `performance-engineer`

Only if something is over budget. If everything is inside, stop: optimizing something
that fits is how readable code becomes unreadable for nothing.

```
<profiler-summary output>
<the per-system budget table from PERF-BUDGET.md>
<the scene summary from unity-inspect.ps1>
<asset-index.ps1 -Heavy output>

Task: find the cause.
1. Which system, and is the cost steady or spiky? Hitches first - a single 80 ms frame
   damages the experience more than 5 ms of steady cost.
2. ONE hypothesis, and how it would be falsified.
3. The change to try, and what the measurement should look like afterwards.
4. If the budget cannot be met without cutting a feature, say so with the number. That
   is a design decision, not a performance finding.
At most 20 lines.
```

## 5. Present

```
## Perf - <scene>, <build>, <hardware>

| Metric | Actual | Budget | |
| p50 frame | 15.1 ms | 16.6 | ok |
| p99 frame | 28.4 ms | 22.0 | OVER |
| GC/frame | 1.2 KB | 0 | OVER |
| draw calls | 1340 | 1200 | OVER |

<n> of <n> frames over budget, <n> hitches above 2x

Hypothesis: <one>
Falsified by: <what would disprove it>
Try: <the change>

PERF-BUDGET: <verdict>
```

## 6. Fix and re-measure

One change, then measure again with the same scene, route and duration. Report both
sides in the budget's own units. **If a fix cannot be measured, revert it.**

## 7. Record

`docs/qa/performance/<date>.md` with the numbers and the conclusion, the gate into
`.state/gates.jsonl`, and any over-budget item as a story with the owning role from the
per-system table.

## 8. Close

```
✓ PERF-BUDGET <verdict>
Worst: <metric> <actual> against <cap>, owned by <role>
Headroom left: <n> ms

▶ Next: /dev-task <optimization story>   or   /dod-check
```

---

## Token note

- **Zero or one agent calls.** The tool does the comparison; the agent is only for
  investigating a genuine overrun.
- Never estimate frame cost by reading code. It is both expensive in tokens and usually
  wrong, which is the worst combination available.
