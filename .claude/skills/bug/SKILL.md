---
name: bug
description: Files a reproducible bug report with steps, evidence and a triage decision.
---

# /bug "<description>"

Any phase. Owner: `test-engineer`. Produces `docs/qa/bugs/BUG-NNN-<slug>.md`.

---

## 1. Establish reproduction - before anything else

```
Steps (from a clean start): <numbered>
Reproducible: <n> of <m> attempts
```

If it cannot be reproduced from written steps, it is not a bug report, it is a rumour.
Say so plainly and keep investigating - filing an unreproducible bug creates work for
someone who will also fail to reproduce it.

Exception: a crash or data loss gets filed immediately even at 1-in-20, with the
frequency stated. Rare and catastrophic outranks reproducible and cosmetic.

## 2. Gather evidence cheaply

```powershell
.claude\tools\unity-log.ps1 -Errors
```

Take the relevant excerpt, not the log. A screenshot path, a save file, or a capture if
the symptom is visual or temporal.

## 3. Triage - the decision that matters

| Severity | Meaning |
|---|---|
| `P0` | Blocks release: crash, data loss, progression blocker, unplayable |
| `P1` | Blocks the milestone: a core mechanic is wrong, an exploit, a bad first impression |
| `P2` | Fix when convenient: cosmetic, rare, has a workaround |

Then the question most bug processes skip: **is this a bug or a design problem?** A
mechanic doing exactly what it was specified to do, badly, is not a bug. Route it to
`game-designer` and say so - filing it as a bug sends it to a programmer who will
correctly implement the same wrong thing again.

## 4. Write

```markdown
# BUG-NNN: <what is wrong, not what you think causes it>
**Severity:** P<n> | **Type:** <story type> | **Owner:** <role>
**Build:** <version> | **Found in:** <scene or flow> | **Reproducible:** <n>/<m>
**Status:** Open

## Steps
1. <exact, from a clean start>

## Expected
## Actual
## Evidence
<log excerpt, screenshot path, save file>

## Scope
<what else is affected, what still works - this is what makes it triageable>

## Suspected area
<optional, clearly marked as a guess>
```

Title the symptom, not the theory. `BUG-042: crash when dropping an item while climbing`
survives being wrong about the cause; `BUG-042: null ref in GrabHandler` does not.

## 5. Attach a test if there is one

If a failing test reproduces it, name it in the report. A bug with a failing test is the
cheapest bug anyone will ever fix, and the test becomes the regression guard for free.

If there is no test, note whether one is possible. Some bugs are only reachable by hand,
and saying so saves the next person from looking.

## 6. Route

| Severity | Route |
|---|---|
| P0 | `/hotfix` if it is live, otherwise straight into the current milestone |
| P1 | Into the current milestone, at the front |
| P2 | Backlog, reviewed at the next `/milestone-plan` |

Design problems go to `game-designer` regardless of severity.

## 7. Close

```
✓ BUG-NNN <severity> - <title>
Reproducible <n>/<m>   Owner <role>
Test: <name, or "not covered - manual only">
<if design: "This is a design problem, not a bug. Routed to game-designer.">

▶ Next: <the routed command>
```

---

## Token note

Free unless the cause is genuinely unclear, in which case one call to the owning
programmer with the evidence embedded. Do not spawn an investigation for a bug with clean
reproduction steps - the steps are the investigation.
