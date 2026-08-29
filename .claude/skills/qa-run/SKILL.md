---
name: qa-run
description: Runs the EditMode and PlayMode suites, reports what failed and why, and files bugs for genuine failures.
---

# /qa-run [scope]

Phase 4. Owner: `test-engineer`.

---

## 1. Run them

Unity tests run through the Editor in batch mode:

```powershell
$editor = "<from ProjectSettings/ProjectVersion.txt via Unity Hub>"
& $editor -runTests -batchmode -projectPath . `
  -testPlatform EditMode -testResults Logs/tests-editmode.xml -logFile Logs/tests.log
& $editor -runTests -batchmode -projectPath . `
  -testPlatform PlayMode -testResults Logs/tests-playmode.xml -logFile Logs/tests-play.log
```

If the Editor is not available, say so plainly and stop. **Do not report test results you
did not obtain** - a fabricated green suite is worse than no suite.

Then read the results, not the log:
```powershell
.claude\tools\unity-log.ps1 -Path Logs/tests.log -Errors
```

## 2. Parse the results

The NUnit XML is small enough to read directly. Extract only:
- totals per platform
- the name and message of each failure
- duration outliers, which are usually the flaky ones

Never paste the whole XML into context.

## 3. Triage each failure - the important step

| Cause | Meaning | Action |
|---|---|---|
| Genuine regression | the code is wrong | `/bug`, then a fix story |
| Test is wrong | the design changed, the test did not | fix the test, note why |
| Flaky | passes on re-run | **treat as a bug** - see below |
| Environment | Editor version, missing asset, path | fix the environment, not the test |

**Flaky tests are not a lesser category.** A PlayMode test that fails one run in five
trains everyone to ignore failures, which is worse than having no test. Either fix the
determinism or delete the test and say so.

## 4. Present

```
## QA run - <date>

EditMode  <passed>/<total>   <duration>
PlayMode  <passed>/<total>   <duration>

Failures
| Test | Cause | Regression / Test wrong / Flaky | Action |

Slowest: <test> <duration>  <if it is an outlier, it is usually the flaky one>
Coverage gaps: <ACs from this milestone with no test>
```

## 5. File what is genuine

For each real regression, run `/bug` with the failing test named. A bug with a failing
test attached is the cheapest bug anyone will ever fix - do not lose that by describing
it in prose instead.

## 6. Close

```
✓ <passed>/<total>

Regressions: <n> -> BUG-<ids>
Tests to fix: <n>
Flaky: <n>   <if >0: "these will be ignored by everyone within a week - fix or delete">

▶ Next: /dev-task <fix story>   or   /dod-check
```

---

## Token note

- **Zero or one agent calls.** Running tests is a command; triaging is a judgement you
  can usually make from the failure message.
- Call `test-engineer` only when a failure cause is genuinely unclear after reading the
  message.
- Read the results file, never the log.
