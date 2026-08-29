# ComfyUI Protocol - local asset generation

The studio produces its own concept art, textures, sprites, UI and icons through a
**local ComfyUI** instance. This document is the contract between agents and that
instance.

---

## 1. The one rule

**No agent ever reads or writes a ComfyUI workflow JSON.** A workflow graph is several
hundred lines of node wiring; putting one in context is the most expensive mistake
available in this repository.

Agents call one script:

```
.claude/comfy/comfy.ps1 <command> [options]
```

and receive at most a few lines back. The script owns the graphs, the placeholders, the
queueing, the polling and the file naming.

---

## 2. Commands

```powershell
# Is the server up, what can it do
.claude/comfy/comfy.ps1 status

# Discover installed models and cache the result to .state/comfy-caps.json
.claude/comfy/comfy.ps1 caps -Refresh

# Generate. Style comes from the locked project style, not from the caller.
.claude/comfy/comfy.ps1 gen `
    -Workflow txt2img `
    -Prompt "wooden shipping crate, three quarter view" `
    -Slug tex_crate_wood_01 `
    -Out "Assets/_Project/Art/Generated/props" `
    -Count 4

# Variations of an approved image (keeps the seed family)
.claude/comfy/comfy.ps1 vary -Source <path> -Count 4 -Strength 0.35

# Seamless tiling texture
.claude/comfy/comfy.ps1 gen -Workflow tileable -Prompt "..." -Slug tex_ground_gravel_01

# Transparent-background sprite or icon
.claude/comfy/comfy.ps1 gen -Workflow sprite -Prompt "..." -Slug ui_icon_hammer

# Upscale an approved pick
.claude/comfy/comfy.ps1 upscale -Source <path> -Factor 2

# What is queued, what finished
.claude/comfy/comfy.ps1 jobs
```

Output is always a compact block:

```
JOB 4a1c  txt2img  tex_crate_wood_01  4 images  seed 88213xx  12.4s
  Assets/_Project/Art/Generated/props/tex_crate_wood_01_a.png
  Assets/_Project/Art/Generated/props/tex_crate_wood_01_b.png
  ...
```

---

## 3. Style locking

`/art-direction` writes **one** file:

`.claude/comfy/styles/project.json`

```json
{
  "name": "flat-stylised-warm",
  "positive": "flat stylised game art, warm palette, soft ambient occlusion, clean silhouette",
  "negative": "photorealistic, text, watermark, harsh shadows, busy detail",
  "model": "flux-2-klein-base-9b-fp8.safetensors",
  "loras": [],
  "steps": 20, "cfg": 3.5, "sampler": "euler", "scheduler": "simple",
  "size": { "default": [1024,1024], "sprite": [768,768], "tileable": [1024,1024] },
  "seedFamily": 88213000
}
```

Every generation merges this file in. A caller supplies **subject only**; the style
comes from the lock. That is what makes 300 assets look like one game.

Changing this file is an art-direction decision, not a generation decision. It requires
`art-director` sign-off and a line in `docs/DECISIONS.md`, because it silently
invalidates the visual consistency of everything generated before it.

---

## 4. Model discovery, never model hardcoding

Installed checkpoints change. `comfy.ps1 caps` queries the running server's
`/object_info` and writes a **short** summary to `.state/comfy-caps.json`:

```json
{ "checkpoints": [...], "diffusionModels": ["flux-2-klein-base-9b-fp8.safetensors"],
  "loras": [], "upscalers": [...], "vae": [...], "hasBackgroundRemoval": true,
  "refreshed": "2026-08-29" }
```

`/art-direction` picks the model from this file. If the requested model is missing, the
script fails loudly with the list of what *is* available. No agent guesses filenames.

---

## 5. Generation lifecycle

```
1. /gen-asset            asset-generator writes an ASSET REQUEST (subject, count, use,
                         target path, acceptance notes) - never a raw prompt
2. comfy.ps1 gen         script merges project style + subject, queues, saves to
                         Art/Generated/<group>/
3. review                the user picks. Generation is cheap, picking is the bottleneck,
                         so generate 4 and show a contact sheet, never generate 1
4. cleanup               technical-artist: crop, alpha, power-of-two, naming, budget
5. promote               moves from Art/Generated/ to Art/Textures|Sprites|UI/
                         and records provenance in docs/art/ASSET-LOG.md
6. import                AssetPostprocessor applies the import preset automatically
```

Nothing under `Art/Generated/` ships. Promotion is a deliberate step with a name change.

---

## 6. Provenance

Every promoted asset gets one line in `docs/art/ASSET-LOG.md`:

```
| date | asset | workflow | seed | subject prompt | approved by |
```

This exists so an asset can be regenerated at a different resolution two months later
without re-inventing the prompt, and so the studio can answer where any image came from.

---

## 7. What ComfyUI is not for

| Do not generate | Reason | Instead |
|---|---|---|
| 3D models | Image models do not produce usable topology | Modelling, kitbash, or a marketplace asset with an ADR |
| Anything with readable text | Diffusion text is unreliable | Render text in Unity with a real font |
| Final UI layout | Needs pixel precision and states | Generate the *material* (icons, frames), lay out in UGUI |
| Animation frames needing coherence | Frame-to-frame drift | Sprite sheets from a rigged source |
| Logos and store capsules with wordmarks | Legibility and brand risk | Generate a background plate, set type in Unity or a vector tool |

---

## 8. Cost and etiquette

- Generation is GPU time, not tokens. It is cheap for the model and slow for the wall
  clock: **queue a batch, go do other work, collect later**. Do not idle-poll.
- Default batch is 4. Batches above 8 need a stated reason.
- If the server is down, `status` says so in one line and the skill stops. Agents never
  attempt to start ComfyUI themselves; they tell the user how.
