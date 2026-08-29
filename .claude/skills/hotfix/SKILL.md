---
name: hotfix
description: The fast path for a live incident - reproduce, contain, fix minimally, verify, ship. Skips ceremony, never skips verification.
---

# /hotfix "<issue>"

Any phase, when something is broken for players. Owner: `producer` coordinating.

The rule that makes this safe: a hotfix skips **process**, never **verification**. A
shipped fix that breaks something else is worse than the original bug, because it also
costs the trust of everyone who updated.

---

## 1. Contain first, fix second

Before touching code, ask what reduces the damage right now:

| Option | When |
|---|---|
| Roll back to the previous build | the regression is broad, or the cause is unknown |
| Disable the feature via `config/balance/features.json` | it is isolated and flag-gated |
| Post a known-issue notice | the workaround is easy and the fix is not |
| Nothing, fix forward | the fix is small, obvious and provable |

Rolling back is not a failure. It is the cheapest thing on this list and the one people
skip because it feels like one.

## 2. Reproduce - non-negotiable

```
Steps: <numbered, from a clean start>
Reproducible: <n>/<m>
Affects: <who, how many, in what circumstance>
Introduced in: <version> by <story or commit, if findable>
```

**No reproduction, no hotfix.** A fix for a bug you cannot reproduce is a guess shipped
to players, and you will not know whether it worked.

## 3. Fix minimally - one call to the owning programmer

```
<the reproduction steps>
<the log excerpt from unity-log.ps1>
<the relevant code, from a targeted Grep>
<the story that introduced it, if known>

Task: the SMALLEST change that fixes this.
- Fix the cause, not the symptom - but do not refactor around it. That is a separate
  story and it does not ship today.
- Add the regression test in the same change. A hotfix without a test ships the same
  bug again in three weeks.
- If the minimal fix is risky, say so and describe the safer larger one. The decision
  belongs to the user.
- State explicitly what else this change could affect.
```

## 4. Verify - the step that must not be skipped

```
[ ] The reproduction steps no longer reproduce it
[ ] The regression test fails on the old build and passes on the new one
[ ] The full suite is green (not just the new test)
[ ] Build succeeds
[ ] Cold boot works
[ ] A save from the affected version still loads
[ ] Multiplayer, if it touched networked code: host and joiner
```

Timeboxed does not mean untested. If any of these cannot be run, say which - and prefer
rolling back over shipping an unverified fix.

## 5. Ship

```powershell
.claude\tools\build.ps1 -Target StandaloneWindows64 -Config Release
```

Patch notes: one line, the symptom, in the player's words. Then **stop** - the upload is
the user's action, hotfix or not. Urgency is not authorization.

## 6. Afterwards - on the same day

Two questions, written down in `docs/qa/bugs/BUG-NNN`:

```
Which check would have caught this before release?
  -> add it to the DoD, the test suite, or the rules file. Today, not "later".

Why did it get through?
  -> if the answer is "we were in a hurry", that is a /retro item, not a personal failing.
```

A hotfix that does not change a check is a hotfix that will recur.

## 7. Close

```
✓ Hotfix <version>: <symptom>
Contained by: <rollback | flag | fix forward>
Fix: <n> lines in <n> files   Regression test: <name>
Verified: <n>/<n> checks

Check added: <what, and where>

▶ You upload. Then: /retro at the milestone boundary.
```

---

## Token note

- **One or two agent calls.** No gates, no planning, no ceremony.
- The verification checklist is free and is the entire safety margin. Everything else in
  this skill can be rushed; that list cannot.
