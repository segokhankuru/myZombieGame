# Definition of Done

Done is not a feeling, it is **evidence**. `/dod-check` uses this file as a checklist
and refuses to close a story when evidence is missing.

The game-specific twist: for anything the player touches, a green test is **necessary
and not sufficient**. There is a felt-experience clause on every player-facing type.

---

## Story types and required evidence

The type is assigned during `/stories` and decides the evidence.

| Type | Assigned when | Required evidence |
|---|---|---|
| **Logic** | Rule, calculation, state transition, validation | Passing EditMode test in `Tests/EditMode/`, named for the AC |
| **Feel** | Movement, camera, input response, hit reaction, animation timing | PlayMode test for the mechanics **plus** a `/feel-check` note: what it should feel like, what it does feel like |
| **Content** | Level, encounter, item set, dialogue, tuning pass | Loads without error **plus** a playthrough note in `docs/qa/evidence/` recording the intended beat and the actual beat |
| **Integration** | Two or more systems, save/load, scene flow, addressables | Passing PlayMode test covering the seam, plus a cold-boot run |
| **Net** | Anything replicated | Two-client test: host and joiner, including a joiner arriving late |
| **Data** | Config schema, content pipeline, save migration | `config-validate.ps1` clean, plus an old-save load test if persisted |
| **Art** | Asset promotion, material, VFX, shader | In-engine screenshot, budget entry updated, provenance line in `ASSET-LOG.md` |
| **Audio** | SFX, music, mixer routing | In-engine capture or note, bus assignment recorded |
| **UI** | Screen, HUD element, menu, onboarding | PlayMode test or `docs/qa/evidence/` note with steps, plus a readability check at target resolution |
| **Infra** | Build, CI, pipeline, tooling | A green build output and written revert steps |

For a mixed story, the **highest-risk type** applies. `Feel` and `Net` outrank
everything else.

---

## Checklist for every story

```
[ ] Every acceptance criterion ticked in the story file
[ ] Traceability complete: story -> SYS-* -> PILLAR-*, and ADR-* if applicable
[ ] The type's required evidence exists and passes
[ ] No balance number was hardcoded in C# (it lives in config/)
[ ] No .unity / .prefab / .asset file was hand-edited
[ ] Out-of-scope section untouched - no neighbouring story's work done here
[ ] No rule violated in the relevant .claude/rules/ file
[ ] A new package or third-party asset has an ADR
[ ] Failure paths handled: at least two non-happy-path cases
[ ] Zero new per-frame allocation in gameplay code (profiler or review evidence)
[ ] Code review verdict APPROVED, or CONDITIONAL items closed
[ ] Design docs updated if behaviour changed from the GDD
[ ] A new decision is one line in docs/DECISIONS.md
```

---

## Milestone Definition of Done

```
[ ] Milestone goal met, or the variance is written down with a reason
[ ] Every story either DONE or returned to backlog with a reason
[ ] Regression suite green (EditMode + PlayMode)
[ ] One full playtest run, findings filed in docs/qa/playtests/
[ ] Frame budget held on target hardware, PERF-BUDGET evidence current
[ ] No open CONDITIONAL gate items in .state/gates.jsonl
[ ] docs/CONTEXT.md updated: stage, work in progress, debt
[ ] design/risks.md reviewed
[ ] Retrospective held, actions owned
```

---

## Release Definition of Done

```
[ ] Every story in the release scope DONE
[ ] Cold-boot to first playable input under the target time on min-spec
[ ] Save compatibility verified: previous version's save loads
[ ] Multiplayer: host + late joiner + disconnect + host migration path exercised
[ ] Regression + smoke green on the shipped build, not on the Editor
[ ] PERF-BUDGET APPROVED, measured on target hardware
[ ] PT-FUN APPROVED from a session with someone who did not build the game
[ ] Localization keys complete, no raw keys visible
[ ] Steam: depots, achievements, cloud paths, capsules, tags, age rating filled in
[ ] Rollback plan written and the previous build still uploadable
[ ] CHANGELOG.md and patch notes current
[ ] OPS-READY APPROVED
[ ] SH-SHIP APPROVED
```

---

## Acceptance criterion quality

`game-designer` and `qa-lead` reject these:

| Bad | Why | Good |
|---|---|---|
| "Movement should feel good" | Not verifiable | "Input to first visible movement under 60 ms; direction change completes in 0.12 s; the player can cancel into a stop at any point" |
| "The enemy should be challenging" | Not measurable | "An average player loses 2 of 5 first encounters; a mastered player clears it without damage in under 20 s" |
| "The economy should be balanced" | Unscoped | "After 30 minutes a player has between 400 and 700 currency on the intended route; the cheapest upgrade is affordable at minute 12" |
| "The UI should be clear" | Not testable | "At 1920x1080 the fuel gauge is readable from 60 cm; a first-time player finds the map without prompting in 3 of 4 sessions" |

Preferred form: **Given / When / Then**, plus edge cases.

For a `Feel` story, add one more line that no app studio ever writes:

```
FEELS LIKE: <the one-sentence sensation this must produce>
```

That sentence is what `/feel-check` judges against, and it is the difference between a
game that works and a game that is good.
