# Coordination Rules

Agents talk along two axes: **vertical delegation** (work handed down) and **horizontal
consultation** (peers reaching agreement).

---

## 1. Vertical delegation

```
              studio-head ──────────── technical-director
                   │                          │
        ┌──────────┴───────────┐              │
  creative-director        producer      unity-architect
        │                      │              │
   game-designer               │              │
    │        │                 │              │
systems-  level-/narrative-/   │              │
designer  ux-designer          │              │
                               │              │
    ┌──────────┬───────────────┼──────────┬───────────┐
 gameplay/  graphics/       art-director  build-     audio-
 systems/   netcode/ai/          │        engineer   director
 tools prog                technical-artist
                                 │
                           asset-generator
                               │
                          qa-lead ── test-engineer / code-reviewer
                                  ── performance-engineer / playtest-analyst
```

Rules:

- An agent delegates **one level down only**. `studio-head` does not hand work to
  `gameplay-programmer`; the chain is `game-designer` -> `producer` -> programmer.
- Delegated work travels as a **task packet** (`token-budget.md` §4).
- One story has exactly one owner. Work with two owners is split or it stalls.

---

## 2. Horizontal consultation

Same-tier agents may consult each other. The pairs that matter:

| Pair | When | Output |
|---|---|---|
| `creative-director` ↔ `game-designer` | Concept and GDD | Pillars that match mechanics |
| `game-designer` ↔ `systems-designer` | Any new tunable | Schema entry with a range and a reason |
| `game-designer` ↔ `level-designer` | Teaching order, pacing | Beat chart |
| `unity-architect` ↔ `systems-programmer` | Save format, scene flow, config loading | ADR |
| `unity-architect` ↔ `netcode-programmer` | What is authoritative, what is predicted | Network model ADR |
| `art-director` ↔ `technical-artist` | Style versus budget | Achievable style lock |
| `technical-artist` ↔ `performance-engineer` | Asset budget | Numbers in `ASSET-BUDGET.md` |
| `game-ux-designer` ↔ `game-designer` | Readability of a mechanic | HUD spec |
| `qa-lead` ↔ `game-designer` | Are the acceptance criteria testable | Revised criteria |
| `playtest-analyst` ↔ `creative-director` | Why it is not landing | Design change, not a bug |

**Round-table protocol** (`/roundtable`):

1. Every participant gets the **same input** through a **different lens**.
2. They work **in parallel** and cannot see each other's output. That is the point:
   it prevents the second opinion from being an echo.
3. The caller separates **agreement** from **disagreement**.
4. Disagreements go to the user as a decision via `AskUserQuestion`.
5. The decision becomes one line in `docs/DECISIONS.md`.

---

## 3. Escalation

An agent **stops** and escalates in these cases:

| Situation | Escalate to |
|---|---|
| Design intent is ambiguous or self-contradictory | `game-designer` -> `creative-director` |
| A mechanic cannot be made to feel right as specified | `game-designer` -> `creative-director` |
| A number is needed that has no config key | `systems-designer` |
| Scope is growing, the milestone is at risk | `producer` -> `studio-head` |
| An architecture rule blocks a design requirement | programmer -> `unity-architect` -> `technical-director` |
| A new package or marketplace asset is needed | programmer -> `unity-architect` -> `technical-director` (ADR) |
| The save format must change | programmer -> `systems-programmer` -> `unity-architect` |
| Replication cost is too high for the design | `netcode-programmer` -> `unity-architect` -> `game-designer` |
| The style lock cannot produce the asset | `asset-generator` -> `art-director` |
| An asset blows the budget | `technical-artist` -> `performance-engineer` |
| An acceptance criterion is not testable | `test-engineer` -> `qa-lead` -> `game-designer` |
| Two stories want the same scene | both -> `producer` |
| The frame budget cannot be met without cutting a feature | `performance-engineer` -> `technical-director` -> `studio-head` |

**Escalation format:**

```
ESCALATION -> <target role>
PROBLEM: <one sentence>
WHY I CANNOT RESOLVE IT: <authority or knowledge boundary>
OPTIONS: <2-3 options with trade-offs>
MY RECOMMENDATION: <choice + reason>
BLOCKED WORK: <waiting stories>
```

---

## 4. Parallel work rules

When `producer` plans a milestone it guarantees:

- **One scene, one story.** Two agents never touch the same `.unity` file in a sprint.
  This is the single most expensive conflict in Unity and it is preventable by planning.
- **One prefab, one story**, for the same reason, with variants as the escape hatch.
- Contract work finishes before consuming work: config schema before the system that
  reads it, network model before replicated gameplay, art style lock before asset batches.
- Independent vertical slices run in parallel: `[gameplay + systems]` ‖ `[art + audio]`
  ‖ `[tools + build]`.
- The integration point is planned for **mid-milestone**, never the last day.

Typical parallel shape for one feature:

```
Day 1     unity-architect  -> boundaries + ADR (lock)
          systems-designer -> config schema + starting values (lock)
Day 1-2   gameplay-programmer -> mechanic against placeholder art
          ‖ asset-generator -> concept batch, art-director picks
Day 2-4   systems-programmer -> save + scene flow
          ‖ technical-artist -> promote and import the picked assets
          ‖ audio-director -> SFX pass
Day 4     integration: real assets replace placeholders
Day 5     test-engineer -> tests ‖ code-reviewer -> review
Day 5     playtest-analyst -> first play ‖ qa-lead -> DoD gate
```

Placeholder art is not a compromise, it is the schedule. Gameplay never waits on art.

---

## 5. Communication discipline

- No pleasantries between agents. Data only.
- Replies use the format in `token-budget.md` §5.
- Noticing something outside your task: **do not fix it**, report it under `NOTE:`.
  Scope creep is a regression risk and a token cost.
- If an assumption had to be made, the reply opens with `ASSUMPTION:`.
- Never claim something compiles, runs or feels right without evidence. In this
  repository "it should work" is a review finding.

---

## 6. The user's role

You are the studio's founder and final decision-maker. Agents:

- Present **options** on anything strategic. They do not decide taste.
- Ask **before writing files**, except code inside an approved story.
- **Close the debate** once you have decided, and implement fully.
- If you reject a concern twice, it is recorded in `docs/DECISIONS.md` as an accepted
  risk and never raised again.
