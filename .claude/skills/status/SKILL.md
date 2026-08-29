---
name: status
description: The project dashboard - phase, milestone, progress, blockers, open gates, budgets, risks and a token note. Then one next step.
---

# /status [--deep]

Owner: `producer`. **Invokes no agents by default** - it reads files and runs tools.

## 1. Gather (all cheap)

| Source | What is taken |
|---|---|
| `.state/project.json` | phase, milestone, mode, scale, counters, stack |
| `docs/CONTEXT.md` | current focus, known debt |
| `design/milestones/M-NN.md` | scope, assignments, scene ownership |
| Story files, **first 8 lines only** | status, owner, type |
| `.state/gates.jsonl` | gate history, open CONDITIONAL items |
| `design/risks.md` | active risks |
| `docs/qa/bugs/` header blocks | open bugs by severity |
| `.state/agent-log.jsonl` | agent call count for the token note |
| `.claude/tools/profiler-summary.ps1` | latest frame budget verdict |
| `.claude/tools/asset-index.ps1 -Heavy` | assets over budget |
| `.claude/comfy/comfy.ps1 status` | is generation available, is a style locked |

Read story **headers**, never whole story files in bulk. That single rule is the
difference between a free `/status` and an expensive one.

## 2. The dashboard

```
+- <Game> --------------------------------------------------
| Phase <p>   Milestone <NN>   Mode <mode>   Scale <scale>
| Unity <version>   Pipeline <URP>   Target <hardware>
+-----------------------------------------------------------

MILESTONE <NN> - <the question it answers>
  Exit criteria: <one line>   Day <x>/<y>
| Story | Owner | Type | Scene owned | Status |
| 012 | gameplay-programmer | Feel | - | In review |
| 013 | ui-programmer | UI | - | In progress |
| 014 | level-designer | Content | Level_01 | Blocked |

PROGRESS
  Epics    ########..  <a>/<b>
  Stories  ######....  <c>/<d> done

BLOCKED (<n>)
  story-014 - <reason> -> escalate to <role>

OPEN GATE ITEMS (<n>)
  ARCH-DESIGN CONDITIONAL - <n> items -> close with <command>

BUDGETS
  Frame    p50 <x> / <cap> ms   p99 <y> / <cap>   <ok|OVER>
  Assets   <n> files over budget   worst: <name> <mb> MB
  Build    <mb> MB / <cap>

QUALITY
  Tests <passed>/<total>   Bugs P0 <a> P1 <b> P2 <c>
  Last playtest: PT-<nn> <date> - <verdict>

ART
  Style lock: <name> on <model>   Generated this milestone: <n>

RISKS (high, active)
  | Risk | P x I | Owner | Mitigation |

DEBT
  <at most 3 lines from CONTEXT.md>

TOKEN NOTE
  <N> agent calls, <M> gates, <K> generations, mode=<mode>
  <a threshold line only if one was crossed - see producer's table>

▶ NEXT
  <one command> - <one sentence why>
```

## 3. Choosing the next step

First match wins:

```
1. A blocked story           -> the escalation, naming the role and the question
2. An open CONDITIONAL item  -> the command that closes it
3. An open P0 bug            -> /dev-task <fix story>
4. Frame budget broken       -> /perf-check
5. Milestone in progress     -> /dev-task <next on the critical path>
6. Milestone stories done    -> /playtest, then /dod-check
7. Milestone closed          -> /retro, then /milestone-plan
8. Release scope done        -> /build, then /release
```

## 4. `/status --deep`

Invokes `producer` once, with the dashboard data embedded:

```
<DASHBOARD>
Story completion over the last 2 milestones: <data>
Estimate vs actual: <data>
Agent calls per story: <data>

Task: delivery health.
1. Velocity trend and why
2. Estimate bias - are we systematically wrong in one direction
3. Bottleneck role - who is always on the critical path
4. Recurring block cause
5. Is the token cost per story rising, and what packet section is missing
At most 3 concrete changes, each with an owner.
```

## 5. Update

Refresh the "Current work" section of `docs/CONTEXT.md` and `lastUpdated` in
`.state/project.json`. Nothing else.

## Token note

The default mode is free. Running `/status` at the start of each session makes every
later step start from the right context - an indirect but large saving.
