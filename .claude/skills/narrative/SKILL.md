---
name: narrative
description: Writes the story bible, characters and dialogue with localization keys, and checks that the fiction and the mechanics are telling the same story.
---

# /narrative [scene or topic]

Phase 1-3. Owner: `narrative-designer`. Produces `design/narrative/`.

---

## 1. Scope the ask

No argument -> the bible. With an argument -> that scene, character or bark set.

Get the character budget from `design/ux/hud.md` before writing anything that appears in
the UI. Guessed limits become clipped text, and clipped text is found at release.

## 2. One call - `narrative-designer`

For the bible:
```
<PILLARS.md>
<the core loop - the verbs the player actually performs>
<the brief's player section>

Task: the story bible.
1. Premise in three sentences.
2. The rules of this world: what is possible, what is not, what is normal here.
3. Voice per speaker: register, sentence length, and what they never say.
4. Naming conventions for places, items and characters, so later additions fit.
5. The mechanics-fiction check: where does what the player DOES contradict what the
   fiction CLAIMS? Report the contradictions; do not paper over them with dialogue.
   If the fiction says "gentle community" and the loop says "extract value from
   neighbours", the loop wins and the fiction is lying.
```

For dialogue or barks:
```
<the bible's voice table>
<the trigger conditions from the relevant SYS-* doc>
<the character limits from design/ux/hud.md>

Task: write the lines.
Table: key | speaker | line | trigger | repeats | max chars.
Key format: <area>.<subject>.<detail>[_NN], lowercase, dots.
Any line on a repeatable trigger needs a pool of at least 4 plus a no-repeat-within-N
rule. A single bark on a common event is a bug that ships.
Assume +40% length in German and Turkish. Never concatenate sentence fragments -
word order differs by language.
```

## 3. The contradiction report

If the mechanics-fiction check found anything, present it as a decision, not a note:

```
The fiction says: <claim>
The mechanics say: <what the player actually does>
Options
  A) Change the fiction to match the loop - cheap, honest
  B) Change the loop to match the fiction - expensive, and it is a design decision
  C) Make the contradiction the point - only if it is deliberate and legible
```

`AskUserQuestion` with the recommendation first. This is the highest-value thing this
skill produces and it costs nothing to surface.

## 4. Write

`design/narrative/bible.md` or `design/narrative/dialogue/<scene>.md`. Keys go straight
into the localization table - never a literal string in code, from the first line.

## 5. Close

```
✓ <n> lines, <n> keys, <n> bark pools.

Longest line: <n> chars against a <n> limit
Contradictions found: <n>  <resolved | escalated to game-designer>

▶ Next: /game-ux   if the text needs somewhere to live
   or:   /stories   if the UI already exists
```

---

## Token note

- **One agent call.**
- Get character limits from the UX doc rather than guessing. One question now is cheaper
  than a localization pass later.
