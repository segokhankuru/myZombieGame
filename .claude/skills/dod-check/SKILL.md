---
name: dod-check
description: The done gate. Checks a story or a milestone against the Definition of Done, using the evidence rather than the claim of evidence. Runs QA-DONE.
---

# /dod-check [story | milestone]

Phase 4. Owner: `qa-lead`.

---

## 1. Assemble the evidence, not the assertions

For a story, gather:

| Item | Source |
|---|---|
| Acceptance criteria and their tick state | the story file |
| The type, and what the DoD requires for it | `definition-of-done.md` |
| Test results | the actual run output, not the story's claim |
| Compile state | `.claude/tools/unity-log.ps1 -Errors` |
| Config compliance | `.claude/tools/config-validate.ps1` |
| Review verdict | `.state/gates.jsonl` |
| Felt-experience note | `/feel-check` output or the evidence file |

The distinction that makes this gate worth running: a story that says "tests pass" is a
claim. The test output is evidence. Check the second one.

## 2. Mechanical checks first - free

```powershell
.claude\tools\unity-log.ps1 -Errors
.claude\tools\config-validate.ps1
```

Plus, from the diff:
- Did a `.unity`, `.prefab` or `.asset` file change by hand? -> automatic fail
- Is there a `[SerializeField]` number with a default in gameplay code? -> automatic fail
- Was anything in the out-of-scope section touched? -> automatic fail

These three need no judgement, so they cost nothing and catch the failures that recur.

## 3. One call - `qa-lead`

```
<the story file with its criteria and tick state>
<the DoD requirements for this story type>
<the actual test output>
<the review verdict and whether CONDITIONAL items were closed>
<the felt-experience note, if the type requires one>
<the mechanical check results>

Task: the QA-DONE gate.
Work through the DoD checklist against the EVIDENCE, not the claim.
For each unmet item, say specifically what is missing and what would satisfy it.
For a player-facing type, a green test is necessary and not sufficient: is there a note
saying what it actually feels like or looks like in the game?

QA-DONE: APPROVED     - evidence complete, criteria met
QA-DONE: CONDITIONAL  - at most 5 named, one-line, actionable items
QA-DONE: REJECTED     - a criterion is unmet, or the evidence does not exist
Begin with the verdict line.
```

## 4. Milestone mode

`/dod-check milestone` runs the milestone checklist instead: every story done or returned
with a reason, regression suite green, one full playtest filed, frame budget held, no
open CONDITIONAL items, `CONTEXT.md` current, risks reviewed.

The item most often skipped is the playtest. It is also the only one that cannot be
recovered later, because a milestone that shipped without being watched cannot be
retro-watched.

## 5. Present

```
QA-DONE: <verdict>
Story <NNN> - <type>

| Check | Result |
| Criteria ticked | 4/4 |
| Required evidence (<type>) | present - Tests/EditMode/Grab.cs, 6/6 |
| Compiles | yes |
| Config clean | yes |
| No hand-edited YAML | yes |
| No hardcoded tunable | yes |
| Out of scope untouched | yes |
| Review | CR-CODE APPROVED |
| Felt-experience note | MISSING |

Open items
1. <what, and what would close it>
```

## 6. Record

Status -> `DONE` on approval. Counters in `.state/project.json`. Gate into
`.state/gates.jsonl`. On `CONDITIONAL`, the items become the story's next round and
`openGateConditions` increments.

## 7. Close

```
✓ QA-DONE <verdict>
Stories done this milestone: <n>/<n>
Open gate items: <n>

▶ Next: /dev-task <next>
   or:   /playtest   if the milestone stories are finished
```

---

## Token note

- **One agent call.** The mechanical checks are free and catch most repeat failures.
- Do not run this per story in `solo` mode - run it once at the milestone boundary. The
  gate exists to prevent shipping something unfinished, not to add ceremony to a
  prototype.
