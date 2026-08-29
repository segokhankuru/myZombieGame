# Token Budget and Optimization Protocol

A Unity project is the most token-hostile codebase there is: scenes and prefabs are
thousand-line YAML, assets are binary, the Editor log is megabytes. This document is
**binding** and is referenced from every agent definition.

---

## 1. The context pyramid

Get information from the **cheapest layer that works**. Descend only when the layer
above is genuinely insufficient.

```
Layer 0 (free)      -> The task packet (story file) - self-sufficient by design
Layer 1 (free)      -> A tool summary: .claude/tools/*.ps1 output (10-40 lines)
Layer 2 (cheap)     -> docs/CONTEXT.md (<=200 lines) + .state/project.json
Layer 3 (medium)    -> The relevant SSoT section (GDD system / ADR / config schema)
Layer 4 (expensive) -> Targeted Grep in C# source
Layer 5 (very high) -> Reading whole C# files
Layer 6 (forbidden) -> Reading .unity / .prefab / .asset YAML, Editor.log, .meta files
```

**Layer 6 is not a budget item, it is a rule violation.** There is a tool for every one
of those; see section 7.

Before descending to Layer 5, state which Layer 3 document is missing. The answer is
usually "the design doc is thin", not "read more code".

---

## 2. Read budgets (per agent, per task)

| Tier | Whole-file reads | Greps | Tool calls | Note |
|---|---|---|---|---|
| Executive | 3 | 5 | 2 | Summaries and decisions only |
| Design | 6 | 8 | 4 | Design docs are their own domain |
| Production | 5 | 10 | 6 | Status aggregation is tool work |
| Engineering | 8 | 15 | 6 | Story + contract + the module being touched |
| Art / Audio | 4 | 6 | 10 | Their work is tool-driven, not read-driven |
| Quality | 8 | 20 | 8 | Broad search is reasonable when testing |

Budget exceeded, the agent **stops** and reports:
> "Budget exceeded. To continue I also need X. Approve?"

---

## 3. Model routing

| Work shape | Model | Examples |
|---|---|---|
| Ambiguous, strategic, taste-driven, many variables | `opus` | Core loop design, architecture, economy curves, is-this-fun judgement, scope negotiation |
| Defined input to defined output | `sonnet` | Story implementation, shader work, test writing, code review, level layout |
| Mechanical, templated, high volume | `haiku` | Changelog, index refresh, patch notes, naming checks, asset renaming |

Before promoting work to a bigger model, ask: **is the input actually clear?**
If it is not, the fix is not a bigger model, it is a better task packet.

Do not raise a whole skill's model because one step is hard. Split that step out.

---

## 4. The task packet principle - where the savings actually are

A story file must be **self-sufficient**. It contains, copied in (not referenced):

- The acceptance criteria
- The **design intent** (the relevant GDD paragraph) so the GDD is never opened
- The **decision summary** of the governing ADR so the ADR is never opened
- The exact file paths to touch, plus the asmdef they belong to
- The relevant `config/` keys and their current values
- The scene/prefab **summary** (from `unity-inspect.ps1`), never the YAML
- What is out of scope (the neighbouring story)
- Ready-made test scenarios and the playtest question

This turns 8 file reads into 1. **This is the single largest saving in the system.**

---

## 5. Subagent protocol

When a skill calls an agent:

- **Input:** the task plus embedded context. Never say "read these files" - put the
  content in the prompt. The subagent starts blind; the less it searches, the better.
- **Output:** a structured summary, never a transcript or a file dump:

```
VERDICT: <APPROVED | CONDITIONAL | REJECTED | COMPLETE | BLOCKED>
SUMMARY: <at most 3 sentences>
FINDINGS:
- [LEVEL] <file:line> - <one sentence>
NEXT STEP: <one line>
```

- **Parallelism:** independent calls go in one message. Dependent work is chained.
  Never spawn an agent "just in case".

---

## 6. Gate economics

`design/review-mode.txt` selects the mode:

| Mode | Gates | Overhead |
|---|---|---|
| `full` | All 18 | +60% |
| `lean` | Phase transitions only (6) | +20% |
| `solo` | None | +0% |

- A gate is invoked **once**. It is not re-run after `CONDITIONAL` items are closed.
- Never embed a whole document in a gate prompt, embed a summary of at most 100 lines.
- Multiple gates in one phase are invoked **in parallel**, in a single message.

---

## 7. The tool shim - the Unity-specific rule

Every expensive, mechanical read has a script. **Call the script, do not read the file.**

| You want | Do NOT | DO |
|---|---|---|
| Scene or prefab structure | Read `Level_01.unity` (12k lines) | `unity-inspect.ps1 -Path <file>` (~30 lines) |
| What art exists and how heavy it is | Recursive listing of `Assets/` | `asset-index.ps1` (~40 lines) |
| Compile errors | Read `Editor.log` (megabytes) | `unity-log.ps1 -Errors` (~15 lines) |
| Frame cost | Read a profiler capture | `profiler-summary.ps1` (~20 lines) |
| Whether config is valid | Read every JSON file | `config-validate.ps1` (~10 lines) |
| Generate art | Read a ComfyUI workflow graph | `comfy.ps1 gen ...` (~6 lines) |
| Build the game | Reason about build settings | `build.ps1 -Target ...` (~12 lines) |

If a tool does not cover what you need, **extend the tool**. That cost is paid once.
Reading the raw file is a cost paid every session, forever.

---

## 8. Prompt cache discipline

- **Append, do not rewrite.** `docs/DECISIONS.md`, `CHANGELOG.md` and `.state/*.jsonl`
  are append-only. Rewriting a file invalidates the cached prefix.
- Rules files are embedded **verbatim and unchanged** in dev prompts. Stable text
  caches well. Do not paraphrase them per story.
- The round-table context block is deliberately identical for every participant.

---

## 9. Session discipline

- **One phase per session.** At the end of a phase `/status` writes state to disk;
  start a fresh session. Long sessions pay compaction costs repeatedly.
- Never print a long output twice. Write it to a file, summarize on screen.
- Do not re-read a file to verify an edit you just made.
- Asset generation is **asynchronous**: queue a batch, do other work, collect later.
  Do not idle-poll ComfyUI.

---

## 10. Measurement

`/status` prints, for the current milestone:

```
Token note: <N> agent calls, <M> gates, <K> generations, mode=<lean>.
```

Thresholds the `producer` reports on:

| Signal | What it means | Fix |
|---|---|---|
| More than 30 agent calls in a milestone | Task packets are thin | Improve `/stories` output |
| A story needing more than 3 dev rounds | The packet was wrong, not the code | Fix that class of packet |
| Repeated `unity-inspect` on one scene | That result belongs in the story | Embed it in the packet |
| Regenerating one asset more than 4 times | Art direction is under-specified | Re-run `/art-direction` |
