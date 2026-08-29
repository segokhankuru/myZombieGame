---
name: comfy
description: ComfyUI health, model discovery, style lock status and generation history. The diagnostic front end for the asset pipeline.
---

# /comfy [status | caps | jobs | styles]

Any phase. Owner: `asset-generator`. No agent calls - this is all script output.

---

## 1. `status` (default)

```powershell
.claude\comfy\comfy.ps1 status
```

Reports the server, GPU and free VRAM, the queue, and whether a style is locked.

If the server is down, that is the whole answer:

```
COMFY: server not reachable at http://127.0.0.1:8188
  Start ComfyUI Desktop, or: python main.py --listen 127.0.0.1 --port 8188
```

**The studio never starts ComfyUI itself.** GPU work is the user's call, and a background
process an agent started is a process nobody remembers to stop.

## 2. `caps`

```powershell
.claude\comfy\comfy.ps1 caps -Refresh
```

Queries `/object_info` and caches a short summary to `.state/comfy-caps.json`:
checkpoints, diffusion models, text encoders, VAEs, LoRAs, upscalers, ControlNets, and
whether background removal is available.

This is where `/art-direction` gets the model name. **No skill hardcodes a filename** -
installs change, and a hardcoded name fails silently by substituting a different look.

Re-run with `-Refresh` after installing anything.

## 3. `jobs`

```powershell
.claude\comfy\comfy.ps1 jobs
```

The last 15 generations with slug, count and seed, plus the live queue. Use it to collect
a batch queued with `-NoWait`, and to answer "what seed produced that image".

## 4. `styles`

```powershell
.claude\comfy\comfy.ps1 styles
```

Lists the style files and marks which one is the lock. Exactly one `project.json` should
exist. A leftover `candidate.json` after `/art-direction` is clutter - delete it, because
the next person will not know which one is real.

## 5. Diagnosing a failure

| Symptom | Cause | Fix |
|---|---|---|
| `server not reachable` | ComfyUI is not running | start it; the studio will not |
| `No locked style` | `/art-direction` has not run | run it - do not generate first |
| `locked style names model X but this ComfyUI does not have it` | model removed or renamed | `caps -Refresh`, then fix the lock via `/art-direction`. Never silently substitute - it changes the look of the game |
| `does not have these node types` | the template needs a custom node pack | install it, or export your own API workflow to `workflows/custom.json` |
| `still has an unfilled placeholder` | a custom workflow is missing a token | add `{{PROMPT}}`, `{{SEED}}`, `{{WIDTH}}`, `{{HEIGHT}}`, `{{BATCH}}` |
| No images returned | the graph ran but has no `SaveImage` | fix the workflow |
| Out of VRAM | model too large for the card | smaller model, smaller batch, or lower resolution |

## 6. Using your own workflow

The shipped templates cover the common SD and Flux shapes. For anything else, export from
ComfyUI (Settings, enable dev mode, Save (API Format)) to
`.claude/comfy/workflows/custom.json`, replace the values you want driven with
`{{PROMPT}}`, `{{NEGATIVE}}`, `{{SEED}}`, `{{STEPS}}`, `{{CFG}}`, `{{WIDTH}}`,
`{{HEIGHT}}`, `{{BATCH}}`, `{{MODEL}}`, `{{PREFIX}}`.

`custom.json` takes precedence over every built-in template. That is deliberate: your
pipeline beats the studio's assumptions about it.

## 7. Close

```
COMFY <status>
Style: <name> on <model>   Generated this milestone: <n>

▶ /gen-asset "<subject>"   or   /art-direction   if nothing is locked
```

---

## Token note

Free - script output only. Run it before a generation session rather than discovering the
server is down four calls in.
