---
name: gen-asset
description: Generates game art through the local ComfyUI, presents a contact sheet to pick from, then cleans up, promotes and records the chosen asset.
---

# /gen-asset "<subject>" [--batch <file>]

Phase 3. Owner: `asset-generator`, with `technical-artist` for promotion.

No agent ever opens a ComfyUI workflow graph. `.claude/comfy/comfy.ps1` owns the graphs.

---

## 1. Preconditions

```powershell
.claude\comfy\comfy.ps1 status
```

- Server down -> stop, tell the user how to start it, generate nothing.
- **No style lock -> stop and run `/art-direction` first.** Generating without the lock
  is how a project ends up with assets that do not belong to the same game.

## 2. Write the request before generating

`asset-generator`, or write it yourself for a single obvious asset:

```markdown
## ASSET REQUEST <slug>
Category:    <hero prop | set dressing | UI icon | texture | concept | key art>
Use:         <where the player sees it, at what size, still or in motion>
Subject:     <the noun and its distinguishing features - NO style words>
Count:       4
Target:      Assets/_Project/Art/Generated/<group>/
Readability: <which ART-BIBLE category this must read as>
Unusable if: <the specific failure that disqualifies a result>
```

`Unusable if` is what makes the pick fast. Without it you get four images and no
criterion.

Style words in the Subject line are a lock change in disguise. If the subject needs
"warm palette" to look right, the lock is wrong - escalate to `art-director`.

## 3. Generate

```powershell
.claude\comfy\comfy.ps1 gen -Prompt "<subject>" -Slug <slug> -Count 4 -Out <target>
```

| Need | Workflow |
|---|---|
| Prop, concept, key art plate | `-Workflow txt2img` (default) |
| Icon or sprite on a plain background | `-Workflow sprite` |
| Seamless surface | `-Workflow tileable` |
| Variations of an approved pick | `comfy.ps1 vary -Source <path> -Strength 0.35` |
| Bigger version of an approved pick | `comfy.ps1 upscale -Source <path> -Factor 2` |

**Batch mode:** with a list of subjects, queue them all with `-NoWait`, then go do other
work and collect with `comfy.ps1 jobs`. Do not idle-poll: generation is wall-clock, not
tokens, and waiting costs the user time for nothing.

## 4. Contact sheet

Present the paths and ask for a pick against the request's own criteria:

```
<slug> - 4 results, seed <n>

  a  <path>
  b  <path>
  c  <path>
  d  <path>

Judge against: <the Readability line>
Unusable if: <the criterion>
```

Never generate one image and present it as the answer. Generation is cheap; picking is
the bottleneck, and a set of four gives the eye something to judge against.

If nothing is usable after **four rounds**, stop generating. That is an art-direction
problem, not a model problem - escalate to `art-director` with what the results keep
getting wrong.

## 5. Promote - `technical-artist`

The picked file is raw material, not a shippable asset:

```
1. crop and align      consistent framing
2. alpha               clean edges, no halo
3. resize              power of two, within the category's budget
4. rename              <category>_<subject>_<variant>, lowercase ASCII
5. move                Art/Generated/ -> Art/Textures|Sprites|UI/
6. import preset       applied by the AssetPostprocessor automatically
7. budget              update the category row if this changes the picture
```

Nothing under `Art/Generated/` ships. Promotion is deliberate and includes a rename.

## 6. Record provenance

One line in `docs/art/ASSET-LOG.md`:

```
| <date> | <slug> | <workflow> | <seed> | <subject prompt> | <approved by> |
```

This is what lets the same asset be regenerated at a different resolution six months
later without re-inventing the prompt. Skipping it is how a studio loses the ability to
remake its own art.

## 7. Close

```
✓ <slug> promoted to <path>
  Seed <n> - regenerate any time with the same slug
  Budget: <category> <size> against cap <cap>

Generated this milestone: <n>   Rejected: <n>
<if rejection rate is high: "Art direction may be under-specified - consider /art-direction">

▶ Next: /gen-asset "<next>"   or   /dev-task   to use it
```

---

## Token note

- **One agent call for the request, one for promotion.** Zero for the generation itself -
  that is a script and a GPU.
- Batch with `-NoWait` and collect later. Idle polling burns the user's time and buys
  nothing.
- Deterministic seeds mean an approved asset is reproducible, which is why the slug and
  the seed go in the log.
