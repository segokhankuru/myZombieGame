---
name: concept
description: Turns a brief into pillars that can reject features, plus tone, fantasy and the honest differentiator. Runs the CD-PILLARS gate.
---

# /concept

Phase 1. Owner: `creative-director`, with `studio-head` on the commercial lens.
Produces `design/PILLARS.md`.

---

## 1. Read the brief

`design/00-brief.md`. If it does not exist, run `/kickoff` first.

## 2. Two parallel calls, different lenses (one message)

### `creative-director`
```
<the brief, embedded>

Task: define this game's identity.
1. Three pillars, four at most. Each one sentence, phrased so it can REJECT a feature.
   For each: what it accepts, what tempting thing it kills, and what you would observe
   in a playtest if it were true.
2. The fantasy: who the player becomes, in the player's own words, one paragraph.
3. Tone across four dimensions: pace, stakes, humour, failure. Each as "this / not this".
4. Three references, and for each: what we take and what we deliberately leave.
5. The one thing a player would tell a friend about this game after one session.

A pillar that rejects nothing is decoration - do not write one.
Begin with "CD-PILLARS: APPROVED|CONDITIONAL|REJECTED" judged against your own output.
```

### `studio-head`
```
<the brief, embedded>

Task: the commercial lens, in at most 20 lines.
1. What is this game's shelf position - which existing game does a buyer compare it to,
   and what does it do that one does not?
2. Is the differentiator visible in a screenshot, in a GIF, or only after an hour?
   This decides whether the game is marketable at all.
3. What would make someone refund it in the first two hours?
```

## 3. Synthesize (you, not an agent)

```markdown
## <Game> - Concept

### Pillars
PILLAR-1: <one line>
  accepts <x> | rejects <y> | observable as <z>
PILLAR-2 ...

### Fantasy
<paragraph>

### Tone
| Pace | <this> / not <that> |
| Stakes | |
| Humour | |
| Failure | |

### Position
Compared to <game>, this one <difference>.
Visible: <in a screenshot | in a GIF | only after an hour>
Refund risk: <what makes someone quit in hour two>

### Tension between the two lenses
<where the creative and commercial answers disagree - this is the useful part>
```

Where they disagree, present it as a decision with `AskUserQuestion` rather than
picking. A pillar the studio head cannot sell and a hook the creative director does not
believe in are both real problems, and only the user can choose which to carry.

## 4. Write and record

After approval: `design/PILLARS.md`, one line in `docs/DECISIONS.md`, and the pillar
list copied into the `## Pillars` section of `docs/CONTEXT.md` - the one place copying
is allowed, because every agent reads CONTEXT first and pillars must be free to reach.

Append the gate result to `.state/gates.jsonl`.

## 5. Close

```
✓ <n> pillars.

The one that will do the most work: PILLAR-<n>
  because it rejects <the tempting thing>

▶ Next: /core-loop
   Pillars say what it should feel like. The loop says what the player actually does.
```

---

## Token note

- **Two agent calls, in parallel, in one message.** Both receive the brief; neither
  reads a file.
- The synthesis is yours. Do not spawn a third agent to reconcile the first two.
- Do not run `/gdd` in the same session. Pillars need to settle before the design
  document is written against them, and a fresh session is cheaper than a compacted one.
