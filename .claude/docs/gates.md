# Quality Gates

A gate is a **binding verdict** issued by the responsible agent at the end of a phase.
The invoking skill parses the **first line** of the reply.

---

## Verdict format

The invoked agent must open its reply with exactly one of:

```
<GATE-ID>: APPROVED
<GATE-ID>: CONDITIONAL
<GATE-ID>: REJECTED
```

Rationale follows. The verdict is never buried in a paragraph.

| Verdict | Meaning | Flow |
|---|---|---|
| `APPROVED` | The phase may proceed | Continue |
| `CONDITIONAL` | Passes once the listed items are closed | Apply items, do not re-invoke |
| `REJECTED` | Something fundamental is wrong | Phase goes back, user is told |

On `CONDITIONAL` the agent lists **at most 5** items, each one line and actionable.
"Could be better" is not an item.

---

## Gate catalogue

| Gate ID | Phase | Skill | Agent | The question | Mode |
|---|---|---|---|---|---|
| `SH-GREENLIGHT` | 0 | `/kickoff` | `studio-head` | Is this game worth making, and can we tell when it worked? | PHASE |
| `CD-PILLARS` | 1 | `/concept` | `creative-director` | Do the pillars describe one game rather than three? | PHASE |
| `GD-LOOP` | 1 | `/core-loop` | `game-designer` | Is the loop legible, repeatable and does it reward mastery? | PHASE |
| `SD-CURVES` | 1 | `/economy` | `systems-designer` | Do the curves hold across a full session without collapsing? | full |
| `PO-SCOPE` | 1 | `/scope-check` | `studio-head` | Is this scope survivable at the current team size? | PHASE |
| `TD-STACK` | 2 | `/architecture` | `technical-director` | Does the technology fit the game, the target hardware and the team? | PHASE |
| `ARCH-DESIGN` | 2 | `/architecture` | `unity-architect` | Do the assembly and scene boundaries hold under the planned features? | PHASE |
| `AD-STYLE` | 2 | `/art-direction` | `art-director` | Will 300 assets made this way look like one game? | PHASE |
| `NET-MODEL` | 2 | `/netcode-design` | `netcode-programmer` | Does the network model survive the worst-case player action? | full |
| `PM-PLAN` | 3 | `/milestone-plan` | `producer` | Does the plan fit capacity, dependencies and scene ownership? | PHASE |
| `ARCH-STORY` | 3 | `/stories` | `unity-architect` | Are stories cut along architecture and scene boundaries? | full |
| `CR-CODE` | 4 | `/code-review`, `/dev-task` | `code-reviewer` | Is the code correct, allocation-clean and in scope? | lean+ |
| `FEEL-CHECK` | 4 | `/feel-check` | `creative-director` | Does it feel the way the pillars promised? | lean+ |
| `PT-FUN` | 4 | `/playtest` | `playtest-analyst` | Did a real session produce the intended experience? | PHASE |
| `PERF-BUDGET` | 4 | `/perf-check` | `performance-engineer` | Do we hold the frame budget on target hardware? | PHASE |
| `QA-DONE` | 4 | `/dod-check` | `qa-lead` | Does the evidence satisfy the Definition of Done? | PHASE |
| `OPS-READY` | 5 | `/build`, `/release` | `build-engineer` | Is the build reproducible, signed, uploadable and revertible? | PHASE |
| `SH-SHIP` | 5 | `/release` | `studio-head` | Are we shipping this? | PHASE |

**Mode column:** `PHASE` runs in `lean` and `full`, skipped in `solo`. `full` runs only
in full mode. `lean+` runs in `lean` and `full`.

---

## The two gates that make this a game studio

Most pipelines only gate correctness. These two gate *experience*, and they are the
reason a game studio is not an app studio:

- **`FEEL-CHECK`** asks whether the thing that was built feels like the thing that was
  promised. It runs against the pillars, not the acceptance criteria. Code can satisfy
  every AC and still feel dead.
- **`PT-FUN`** is the only gate whose evidence is a human playing. It cannot be
  satisfied by a test suite, and `qa-lead` may not waive it for a shipping milestone.

If either returns `REJECTED`, the correct response is usually a **design** change, not
a bug fix. Route it back to `game-designer` or `creative-director`, not to the
programmer who wrote the code.

---

## Gate invocation pattern

Every skill runs this block before invoking a gate:

```
1. Read design/review-mode.txt (assume "lean" if missing)
2. Compare with the gate's Mode column:
   solo -> skip, note "<GATE-ID> skipped - solo mode"
   lean -> run PHASE and lean+ gates only
   full -> run all
3. If it runs: invoke the agent via the Agent tool. Embed in the prompt (CONTENT,
   not file paths):
     - The gate id and its question
     - A summary of what is being judged (<=100 lines)
     - The evaluation criteria
   End with: "Begin your reply with '<GATE-ID>: APPROVED|CONDITIONAL|REJECTED'."
4. Parse the first line and branch.
```

---

## Gate economics

- A gate is invoked **once**, not re-run after `CONDITIONAL` items are closed.
- Never embed a whole document, embed a summary.
- Gates in the same phase are invoked **in parallel**, in one message.
- To skip a gate deliberately: `/<skill> --gate=off`. That skip is logged.

---

## Gate history

Every result is appended to `.state/gates.jsonl`, one line each:

```json
{"gate":"ARCH-DESIGN","verdict":"CONDITIONAL","phase":2,"milestone":"M-02","items":3,"ts":"2026-08-29T14:02:11"}
```

`/status` reads this file to report open `CONDITIONAL` items. An open item blocks the
next phase transition, not the current work.
