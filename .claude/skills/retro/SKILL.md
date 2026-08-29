---
name: retro
description: Reviews a finished milestone against what was planned, including how much it cost in tokens, and produces owned actions.
---

# /retro

End of a milestone. Owner: `producer`.

---

## 1. Gather the facts before the opinions

| Source | What |
|---|---|
| `design/milestones/M-NN.md` | planned scope, exit criteria, the question it asked |
| Story headers | done, returned, blocked, and for how long |
| `.state/gates.jsonl` | verdicts, and how many were CONDITIONAL |
| `.state/agent-log.jsonl` | agent calls, and calls per story |
| `.state/comfy-jobs.jsonl` | generations, and regenerations per asset |
| `docs/qa/bugs/` | bugs found, and at which stage |
| `docs/qa/playtests/` | what the playtest said, and what was done about it |

Compute:

```
Planned <n> stories, delivered <n>
Agent calls <n>, per story <n.n>
Generations <n>, per promoted asset <n.n>
Gates: <n> run, <n> CONDITIONAL, <n> still open
Bugs found: <n> in dev, <n> in playtest, <n> after
Did the milestone answer its question? <yes | no | partly>
```

That last line is the one that matters. A milestone that delivered every story and did
not answer its question was a container, not a milestone.

## 2. One call - `producer`

```
<the numbers above>
<the milestone question and exit criteria>
<blocked stories and how long each was blocked>
<the playtest verdict>

Task: the retrospective.
1. Did the milestone answer its question? If not, what did we learn instead - that is
   often the more valuable answer.
2. Estimate bias: were we systematically wrong in one direction, and by how much?
3. The bottleneck: which role or which dependency was on the critical path most often?
4. Token behaviour: calls per story against the 30-per-milestone threshold. If it is
   high, WHICH story-packet section was missing? Name it - "packets were thin" is not
   actionable.
5. Regenerations per asset: if above 4, the art direction is under-specified, not the
   model.
6. Bugs found after the milestone: which check would have caught each one earlier?
7. At most 3 changes for next milestone, each with an OWNER and a concrete first step.

Three changes. Not ten. A retro with ten actions produces zero.
```

## 3. Present

```
## Retro - M-NN

Question: "<the question>"   Answered: <yes | no | partly>

Delivered <n>/<n> stories | <n> days planned, <n> actual
Agent calls <n> (<n.n>/story)   Generations <n> (<n.n>/asset)
Gates <n>, CONDITIONAL <n>, open <n>
Bugs: <n> dev / <n> playtest / <n> escaped

Went well
- <...>

Cost more than it should have
- <...> -> because <...>

Changes for M-<NN+1>
| # | Change | Owner | First step |
```

## 4. Feed the changes forward

A retro action that does not change a file is a wish. Each one lands somewhere concrete:

| Finding | Where it goes |
|---|---|
| Packets were thin | the `/stories` checklist, or `.claude/templates/story.md` |
| A rule kept being violated | `.claude/rules/<file>.md` |
| A bug class escaped | `.claude/docs/definition-of-done.md` |
| Assets kept being regenerated | `/art-direction`, re-run |
| A role was always blocking | `/milestone-plan` sequencing |
| Estimates were consistently low | the estimate multiplier in the next plan |

## 5. Write

`design/milestones/M-NN-retro.md`, the actions into the next milestone plan, and
`docs/CONTEXT.md` debt section updated.

## 6. Close

```
✓ Retro M-NN.
Question answered: <yes/no>
Actions: <n>, each with an owner and a file it changes

Carried forward: <open gate items, unaddressed playtest findings>

▶ Next: /milestone-plan   the actions go in as scope, not as good intentions
```

---

## Token note

- **One agent call.** The arithmetic is free and is most of the value.
- The token metrics here are the studio's feedback loop on itself. A rising calls-per-
  story number always has a packet cause, and finding it once saves it every milestone
  after.
