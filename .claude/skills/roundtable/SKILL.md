---
name: roundtable
description: Runs up to four roles in parallel on one hard decision, through different lenses, then separates agreement from disagreement and puts the choice to you.
---

# /roundtable "<topic>"

Any phase. The general-purpose tool for a decision that crosses disciplines.

---

## 1. Pick the participants

No argument -> ask what the topic is.

Suggest from the topic, confirm with `AskUserQuestion`, **at most 4**:

| Topic | Participants |
|---|---|
| Is this fun / does it land | `creative-director`, `game-designer`, `playtest-analyst` |
| Scope or priority | `studio-head`, `producer`, `game-designer` |
| Technology choice | `technical-director`, `unity-architect`, `build-engineer` |
| Architecture or boundary | `unity-architect`, the relevant programmer, `qa-lead` |
| Balance or progression | `systems-designer`, `game-designer`, `playtest-analyst` |
| Visual direction | `art-director`, `creative-director`, `technical-artist` |
| Performance versus fidelity | `performance-engineer`, `graphics-programmer`, `art-director` |
| Multiplayer approach | `netcode-programmer`, `unity-architect`, `game-designer` |
| Level or pacing | `level-designer`, `game-designer`, `playtest-analyst` |
| Release readiness | `studio-head`, `qa-lead`, `build-engineer` |

**Never more than four.** Marginal insight falls off fast while cost grows linearly, and
a five-way synthesis is mostly restatement.

## 2. One shared context block

Write it once, at most 50 lines, and send **the same block to everyone**. Identical
context is both the methodological point and prompt-cache friendly.

```
TOPIC: <topic>
CONTEXT: <the relevant project state, summarized - not file paths>
CONSTRAINTS: <what cannot change: budget, hardware, calendar, decisions already made>
DECISION REQUIRED: <the precise question, phrased so it has an answer>
CURRENT STATE: <what exists today>
```

Embed the content. A participant that has to go looking gives a worse answer for more
tokens.

## 3. Call them in parallel, each with its own lens

```
<THE SHARED CONTEXT BLOCK>

Lens: <the role-specific angle from the table below>

Produce:
1. What you see from your discipline - at most 5 bullets
2. The biggest risk, or the thing everyone else will miss
3. Your recommendation, and why
4. The cost you accept by recommending it
5. When would we know this decision was wrong?

At most 25 lines. Do not guess what the other roles will say - stay in your domain.
```

| Role | Lens |
|---|---|
| `studio-head` | player value, cost, reversibility, what it does to the date |
| `technical-director` | technology risk, maintenance cost, lock-in |
| `creative-director` | does it serve the pillars, does it dilute the game |
| `game-designer` | does it produce a decision, does it survive mastery |
| `systems-designer` | what it does to the curves and the dominant strategy |
| `level-designer` | pacing, teaching order, what it does to existing levels |
| `unity-architect` | boundaries, coupling, what it costs to reverse |
| `producer` | schedule, dependencies, capacity, scene ownership |
| `game-ux-designer` | can the player understand it, how many steps |
| `art-director` | consistency, production cost per asset |
| `technical-artist` | budget, import and pipeline consequences |
| `netcode-programmer` | authority, bandwidth, what breaks on a late join |
| `qa-lead` | testability, regression surface |
| `performance-engineer` | frame cost, behaviour at scale |
| `playtest-analyst` | what we have already observed players doing |

Participants cannot see each other's answers. That is deliberate: a second opinion that
has read the first is an echo.

## 4. Synthesize - you, not another agent

```markdown
## Round-table: <topic>
Participants: <roles>

### Where they agree
<bulleted - agreement across independent lenses is the strongest signal available here>

### Where they disagree
| # | Point | <Role A> says | <Role B> says | Why it matters |

### What nobody said
<the gap you noticed while reading - often the most valuable line in the output>

### Options
**A) <name>** - <what it means> | Gain <...> | Cost <...> | Reverse: <easy|hard>
**B) <name>** - ...

**Recommendation:** <A or B> - <one sentence>
```

## 5. Decide and record

`AskUserQuestion`, recommendation first, labelled `(Recommended)`.

Then:
- One line in `docs/DECISIONS.md`
- Architectural -> suggest `/adr`
- Scope-affecting -> suggest `/scope-check`
- Tuning-affecting -> suggest `/tune`
- Tell each affected role what changed, in one line each, in the report

## 6. Close

```
✓ Decided: <the choice>
Because: <one line>
Wrong if: <the observation that would tell us>
Recorded in DECISIONS.md

▶ Next: <the follow-up command, if any>
```

---

## Token note

- **Cost scales linearly with participants.** Three is the sweet spot; four is the
  ceiling.
- One round. A second round only if the user supplies genuinely new information.
- The synthesis is model work, not agent work. Do not spawn a fifth agent to summarize
  the other four.
