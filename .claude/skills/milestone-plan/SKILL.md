---
name: milestone-plan
description: Plans a milestone - the question it answers, scope, assignments, dependency order, scene ownership and the parallel track layout. Runs the PM-PLAN gate.
---

# /milestone-plan

Phase 3. Owner: `producer`. Produces `design/milestones/M-NN-<slug>.md`.

---

## 1. Gather (free)

Ready stories from `design/backlog/epics/**` (header blocks only), the previous
milestone's actual throughput from `.state/agent-log.jsonl`, open risks, and any open
`CONDITIONAL` gate items - those block the phase transition and must be scheduled.

## 2. Name the question first

A milestone is a question, not a quantity. Before any scheduling, state it:

```
M-NN answers: "<one sentence>"
Exit criteria: <observable by someone who was not in this planning session>
```

"Finish 14 stories" is not a question. "Is the delivery loop fun with two players and
real furniture" is. If the question cannot be written, the milestone is a container, and
containers do not produce decisions.

## 3. One call - `producer`

```
<the ready stories: id, title, type, owner, estimate, dependencies, scene owned>
<the milestone question and exit criteria>
<last milestone: planned vs actual throughput>
<open risks and open CONDITIONAL gate items>

Task: plan M-NN.
1. Which stories are in, which are out. Everything in must serve the question.
2. Dependency order. Contract stories first: config schema before its consumer, network
   model before replicated gameplay, art style lock before asset batches.
3. Scene and prefab ownership: exactly one story per scene at a time. If two need the
   same scene, sequence them and say so - this is the rule that prevents the most
   expensive conflict in Unity.
4. Parallel tracks by day. Gameplay never waits on art: placeholder art is the schedule,
   not a compromise.
5. Integration point, mid-milestone. Anything integrating on the last day does not
   integrate.
6. If the biggest story slips a week, does the milestone still answer its question?
   If not, the plan is too tight - say so now.
7. Risks with a trigger for each: what observation means we escalate.

Begin with "PM-PLAN: APPROVED|CONDITIONAL|REJECTED".
```

## 4. Check it yourself

Two things the agent will get wrong more often than not:

- **Scene collisions.** Scan the ownership column for duplicates. Any duplicate is a
  reject, not a note.
- **Capacity honesty.** Compare the total estimate against the previous milestone's
  actual throughput, not against its plan. Plans are optimistic; throughput is not.

## 5. Present

```
## M-NN: <name>
Answers: <the question>
Exit: <criteria>   Dates: <start> - <end>

Scope: <n> stories, <n> days estimated against <n> days available

| Story | Type | Owner | Owns | Depends on | Day |

Parallel tracks
| Day | Gameplay | Art/Audio | Tools/Build |

Scene ownership
| Scene | Story | Free after |

Integration: day <n>
Contracts locked first: <list>

Risks
| Risk | P x I | Trigger | Owner |

Not in this milestone: <list>
```

`AskUserQuestion`: `Approve (Recommended)` / `Too tight - cut` / `Reorder`

## 6. Write

`design/milestones/M-NN-<slug>.md`, `.state/project.json` (`milestone`, `phase`),
`docs/CONTEXT.md` current-work section, the gate into `.state/gates.jsonl`.

## 7. Close

```
✓ M-NN: <n> stories, <n> days.
Critical path: <the chain>
First: <story> - it unblocks <n> others

▶ Next: /dev-task <the first contract story>
```

---

## Token note

- **One agent call.** The scene-collision and capacity checks are yours and cost nothing,
  and they catch what the agent misses.
- Plan one milestone, not the whole project. A plan written three milestones ahead is
  fiction with dates.
