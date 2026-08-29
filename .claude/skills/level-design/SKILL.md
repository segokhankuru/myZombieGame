---
name: level-design
description: Designs one level - intent, beat chart, teaching order, space, failure states and a blockout spec a tools script can execute.
---

# /level-design <name>

Phase 1-3. Owner: `level-designer`. Produces `design/levels/LVL-NN-<slug>.md`.

---

## 1. Inputs

`design/GDD.md` (loop and teaching order), the `SYS-*` docs for the mechanics this level
uses, and the previous level's spec if there is one. What the player already knows is the
most important input to a level, and it lives in the previous level.

## 2. One call - `level-designer`

```
<the core loop>
<the teaching-order table from the GDD>
<the SYS-* summaries for the mechanics in play>
<what the player already knows: the previous level's "introduces" and "tests" rows>

Task: design level <name>.
1. Intent in one paragraph. If it is "more of the same", say so - that is a scope finding.
2. Beat chart: number, beat, intensity 1-5, what the player does, what they feel,
   duration. The intensity column must vary. A flat curve is not pacing.
3. Teaching: for each new mechanic - introduced how (safely), tested how (mild pressure),
   twisted how (combined). Skipping the safe introduction is why players call a game unfair.
4. Space in words: zones, connections, elevation, sightlines, and the landmark that
   anchors orientation. Write it so a blockout script could follow it.
5. Critical path vs optional space, and what curiosity is rewarded with.
6. Failure states: how the player fails here, what it costs, where they restart, what
   they learned.
7. Three questions to ask an observer watching someone play this level.

Numbers that affect play go to config/content/levels.json, not into this document.
```

## 3. Blockout route

Decide with the user which applies:

| Situation | Route |
|---|---|
| Layout is geometric and describable | `tools-programmer` scripts the blockout from the spec |
| Layout is organic or sculpted | The **user** builds it in the Editor from the spec |
| A blockout already exists | `unity-inspect.ps1 -Path <scene>` and design against what is there |

Never edit the scene YAML. State the route in the spec so the story knows what it is
asking for.

## 4. Present

```
## LVL-NN: <name>
Duration <n> min | Introduces <mechanic> | Requires <mechanics>
Scene: <path>   Blockout: <scripted | manual | exists>

Beats: <n>   Intensity curve: 1-2-3-2-4-5-2
Teaching: <mechanic> safe -> tested -> twisted
Failure: <what it costs>

Observer questions
1. <...>
```

## 5. Write

`design/levels/LVL-NN-<slug>.md`, a row in `design/levels/index.md`, and level content
values into `config/content/levels.json` where the schema exists.

Claim the scene in the milestone plan. **One scene, one story** - a level being designed
and a level being tuned at the same time is a merge conflict waiting to happen.

## 6. Close

```
✓ LVL-NN: <n> beats, intensity <curve>.

Riskiest beat: <n> - <why, usually the twist>
Blockout: <route>   Scene owner: <story or unassigned>

▶ Next: /stories <epic>   turn this into buildable work
   or:   /level-design <next>
```

---

## Token note

- **One agent call.**
- The beat chart is the deliverable. A level document without an intensity column is a
  list of rooms.
