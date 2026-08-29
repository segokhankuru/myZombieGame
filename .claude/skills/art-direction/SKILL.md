---
name: art-direction
description: Writes the art bible and locks the ComfyUI generation style. Runs the AD-STYLE gate. The single highest-leverage art decision in the project.
---

# /art-direction

Phase 2. Owner: `art-director`. Produces `docs/art/ART-BIBLE.md` and
`.claude/comfy/styles/project.json` - **the lock**.

Generating before this runs produces forty images that do not belong to the same game,
and a day spent choosing between them.

---

## 1. Find out what the pipeline can actually do

```powershell
.claude\comfy\comfy.ps1 status
.claude\comfy\comfy.ps1 caps -Refresh
```

If ComfyUI is down, the art bible can still be written - but the lock cannot, and no
model name may be guessed. Say so and continue with the bible only.

The caps output is the constraint list. A style the installed models cannot produce
repeatably is a mood board, not a direction.

## 2. One call - `art-director`

```
<PILLARS.md>
<the tone table from /concept>
<the readability needs: what categories of thing the player must tell apart, from the GDD>
<comfy caps: available models, loras, upscalers>
<the asset budget if /asset-pipeline has run>

Task: the art direction.
1. The style in one sentence, specific enough to exclude things.
2. Palette: 5-8 core values plus accents, each with what it is used for and what it is
   never used for. Reserve one accent exclusively for interactive objects.
3. Silhouette rules: the shape language. What is round, what is angular, what proportions
   repeat. If an object is not recognisable as a black shape at 64 px, no texture fixes it.
4. Lighting: key direction, contrast, shadow treatment. One light direction across all
   generated assets is most of what makes them belong together.
5. Material families and where each is used.
6. The readability contract: for each category (interactive, hazard, decoration), how the
   player identifies it in half a second, in motion, at target resolution.
7. The three nearest styles and why we are not them. This is the most useful section.
8. The generation lock: positive fragment (STYLE ONLY, no subjects), negative fragment
   (short), model chosen FROM THE CAPS LIST, steps, cfg, sampler, scheduler, sizes.

Is this producible with the models listed? If not, say what would need to change.
Begin with "AD-STYLE: APPROVED|CONDITIONAL|REJECTED".
```

## 3. Prove it before locking

Do not lock a style on paper. Write a candidate `styles/candidate.json`, then generate
one probe batch across the range the game actually needs:

```powershell
.claude\comfy\comfy.ps1 gen -Style candidate -Prompt "wooden crate, three quarter view" -Slug probe_prop -Count 4
.claude\comfy\comfy.ps1 gen -Style candidate -Workflow sprite -Prompt "hammer icon" -Slug probe_icon -Count 4
.claude\comfy\comfy.ps1 gen -Style candidate -Workflow tileable -Prompt "gravel ground" -Slug probe_tex -Count 4
```

Three different jobs, because a style that works for props and fails for icons is not a
style. Show the results and ask whether these three sit together as one game.

If they do not, adjust the candidate and probe again. Two or three iterations here save
weeks of inconsistent assets.

## 4. Lock

Copy the approved candidate to `.claude/comfy/styles/project.json`. Record the lock in
`docs/DECISIONS.md` with the model name and date.

Changing this file later silently invalidates the visual consistency of everything
generated before it, so from now on it takes an art-director decision and a decision-log
line.

## 5. Write

`docs/art/ART-BIBLE.md`, the style summary into `docs/CONTEXT.md`, the gate into
`.state/gates.jsonl`. Delete `styles/candidate.json` and the probe images - they are
evidence, not assets.

## 6. Close

```
✓ Style locked: '<name>' on <model>

Palette: <n> core + <n> accents   Interactive accent: <hex>
Probed: props, icons, textures - <they sit together | note the gap>

Generate with: /gen-asset "<subject>"
The lock supplies the style; you supply only the subject.

▶ Next: /asset-pipeline   budgets, naming, import presets
   or:   /gen-asset        start producing
```

---

## Token note

- **One agent call**, plus three cheap generation probes. Generation is GPU time, not
  tokens.
- The probe step is the part people skip and the reason locked styles fail. Three batches
  now is far cheaper than discovering at asset 60 that icons never matched the props.
