---
name: dev-task
description: Implements one story end to end - readiness check, the right programmer, tests, compile verification and code review. The main production loop.
---

# /dev-task [story path]

Phase 3. Owner: the relevant programmer. Produces code, tests and an updated story.

---

## 1. Load and check readiness (free, no agent)

No argument -> suggest the next story in the milestone, critical path first.

```
[ ] Status is Ready (Blocked -> show why and stop)
[ ] Acceptance criteria present, in Given/When/Then form
[ ] Design intent section filled
[ ] Architecture guidance filled, or "N/A - <reason>"
[ ] Config keys listed with current values, or "none"
[ ] Files to touch listed, with the assembly named
[ ] Out of scope filled
[ ] Test scenarios filled
[ ] Required evidence named
[ ] Feel story: FEELS LIKE line present
[ ] Dependencies DONE
[ ] The scene or prefab it owns is not owned by another in-progress story
```

Anything missing:
```
Story not ready: <the missing sections>
Fix: re-run /stories <epic>, or complete the packet by hand.
Continuing anyway means the programmer has to go looking, which costs more tokens
and produces worse code than fixing the packet.
```
`AskUserQuestion`: `Fix the packet (Recommended)` / `Continue anyway`

A dependency not DONE, or a scene owned elsewhere: **stop unconditionally.** Neither is
worth the merge.

## 2. Route to the right agent

The story's `Owner` field decides. Otherwise, from the type:

| Type / content | Agent |
|---|---|
| Mechanic, controller, ability, interaction, game feel | `gameplay-programmer` |
| Save, scene flow, config, input, pooling, addressables | `systems-programmer` |
| HUD, menu, screen, localization plumbing | `ui-programmer` |
| Shader, lighting, VFX, rendering cost | `graphics-programmer` |
| Replication, RPC, ownership, prediction | `netcode-programmer` |
| NPC behaviour, navigation, perception, spawning | `ai-programmer` |
| Editor tool, importer, blockout script, postprocessor | `tools-programmer` |
| Build, CI, pipeline | `build-engineer` |
| Audio implementation | `audio-director` |
| Test automation | `test-engineer` |

## 3. Invoke - embed the entire packet

**Embed the whole story file.** Do not pass a path. The packet was written to be
self-sufficient; handing over a path throws that away.

```
<THE COMPLETE STORY FILE>

Coding rules that apply (follow them exactly):
<THE CONTENT of the relevant .claude/rules/*.md - about 60 lines, embedded verbatim>

Project: Unity <version>, <pipeline>, assemblies <list>

Task: implement this story.
1. Turn the acceptance criteria into a checklist. One test per AC, named for it.
2. Work only in the "Files to touch" list. If it is wrong, targeted Grep - never a
   directory scan, never a scene or prefab read.
3. Extend existing code rather than duplicating it.
4. Balance numbers come from config. If you need one that is not in the packet, STOP and
   escalate to systems-designer.
5. Write tests against the "Test scenarios" section. Do not invent tests from scratch.
6. RUN the tests. Then run .claude/tools/unity-log.ps1 -Errors and report what it said.
   "It should compile" is a review finding, not a result.
7. Do nothing in "Out of scope". Report what you noticed under NOTE.

<For a Feel story, add:>
This is a Feel story. FEELS LIKE: "<the line>". Implement toward that sensation, then
check the criteria. Report honestly what it feels like now, including if it does not
land yet.

Output:
VERDICT: COMPLETE | BLOCKED
SUMMARY: <3 sentences>
FILES: <paths>
TESTS: <command> -> <passed/total>
COMPILES: <unity-log result, or "not verified - Editor not running">
ACCEPTANCE: AC-1 ok | AC-2 ok | AC-3 failed <why>
FEEL: <Feel stories only>
NOTE: <out-of-scope observations - do NOT fix them>
NEXT STEP: <one line>
```

`BLOCKED` -> classify the cause, escalate per `coordination-rules.md` section 3, tell the
user, stop. Do not have the agent work around a blocker; that is how scope leaks.

## 4. Code review (lean and full modes)

`code-reviewer`, one call:

```
<the diff or the changed file contents>
<the story's acceptance criteria, config keys and out-of-scope section>
<the relevant .claude/rules/*.md content>

Task: the CR-CODE gate. Order: correctness -> Unity cost (per-frame allocation) ->
the config rule (a [SerializeField] number with a default is a MAJOR finding unless the
story says otherwise) -> scope fidelity -> rule compliance -> test quality.
Each finding: [BLOCKER|MAJOR|MINOR|NOTE] <file:line> - one-sentence claim plus reason.
At most 15. No style preferences.
Begin with "CR-CODE: APPROVED|CONDITIONAL|REJECTED".
```

| Verdict | Action |
|---|---|
| `APPROVED` | step 5 |
| `CONDITIONAL` | send MAJOR findings back in **one** round, fixed, do not re-invoke the gate |
| `REJECTED` | send BLOCKER findings back, fixed, invoke the gate **once** more |

Skipped in `solo` mode.

## 5. Update the story

Tick the criteria. Status -> `In review` (the DoD has not run yet). Write the test path
and result into `## Required evidence`. Update the date.

## 6. Present

```
✓ Story <NNN>: <title>
  <owner> | <type> | <assembly>

Files: <n> changed, <n> new
Tests: <passed>/<total>     Compiles: <yes | not verified>
Acceptance: <x>/<y>
Review: CR-CODE <verdict> (<n> findings fixed)
<Feel stories: FEEL: <what the programmer reported>>

NOTE (out of scope, not fixed)
- <what was observed>

▶ Next: /dod-check <story>
   or:   /dev-task <next>
   <if a Feel story: "or: /feel-check <feature> - the criteria pass, the question is
    whether it lands">
```

Out-of-scope notes: ask whether to add them to the backlog. Do not act on them here.

---

## Token note - why this skill should be cheap

- **One or two agent calls.**
- The programmer opens **no documentation**. Everything is in the packet.
- The rules file is embedded verbatim and unchanged - stable text caches well, so
  repeating it across stories is nearly free.
- The readiness check is free and prevents round-trips, which is the real saving.
- More than three rounds on one story is a **packet** defect. Report it to `producer`
  and fix that class of packet in the next `/stories` run rather than absorbing the cost
  again.
