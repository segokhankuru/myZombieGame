# Project Context

<!--
The first file every agent reads, on every task. HARD LIMIT 200 lines.
Every line here is paid for hundreds of times. The test for each one:
would an agent starting a task tomorrow act differently without it?
If not, cut it — it lives in the file it belongs to. Run /context-compact.
-->

**Game:** <name> — <one sentence, the pitch>
**Genre / reference:** <closest three games, and how this differs>
**Target player:** <who, and what they want out of an evening>
**Platform:** <PC / Steam / target spec>
**Stage:** <concept | preproduction | vertical slice | production | polish | ship>
**Milestone:** <M-NN — goal — date>
**Review mode:** <full | lean | solo>

## Pillars

<3-4 one-liners, copied from PILLARS.md. This is the one place copying is correct:
every agent reads this file first, and pillars must be free to reach.>

## Core loop

<5 lines maximum.>

## Deliberately not in this game

<3-5 bullets — the scope wall.>

## Technology

| Area | Choice | ADR |
|---|---|---|
| Unity | | |
| Pipeline | | |
| Input | | |
| Networking | | |

## Performance budget

Target <n> fps (<n> ms) on <hardware>. Worst case: <scene>.
Full split: `docs/architecture/PERF-BUDGET.md`

## Art

Style lock: <name> on <model> — `.claude/comfy/styles/project.json`

## Active roles

<the roster from .state/project.json>

## Current work

**Milestone:** <NN> — <the question it answers>
**In progress:** <story — owner>
**Blocked:** <story — reason — who it is with>

## Known debt and risks

<at most 5 lines>
