# ASSET REQUEST <slug>

> **Category:** <hero prop | set dressing | UI icon | texture | concept | key art>
> **Requested by:** <role> | **Date:** <YYYY-MM-DD>

## Use

<Where the player sees it, at what size, still or in motion. This decides the budget and
the resolution more than the subject does.>

## Subject

<The noun and its distinguishing features. NO style words.

The lock supplies the style. If this subject needs "warm palette, flat stylised" to come
out right, the lock is wrong — escalate to art-director instead of writing it here.
A style word in a subject line is a lock change in disguise.>

## Readability category

<Which ART-BIBLE category this must read as: interactive, hazard, decoration.
Interactive things use the reserved accent; decoration must not.>

## Unusable if

<The specific failure that disqualifies a result. This is what makes the pick fast —
without it you get four images and no criterion for choosing.>

## Parameters

| | |
|---|---|
| Count | 4 |
| Workflow | <txt2img \| sprite \| tileable> |
| Target folder | `Assets/_Project/Art/Generated/<group>/` |
| Budget after promotion | <texture size> from `ASSET-BUDGET.md` |

## Command

```powershell
.claude\comfy\comfy.ps1 gen -Prompt "<subject>" -Slug <slug> -Count 4 -Out <target>
```

---

## Result

**Seed:** <n> — the same slug regenerates the same image
**Generated:** <n> | **Rounds:** <n>
**Picked:** <file>
**Promoted to:** <final path and name>
**Logged in:** `docs/art/ASSET-LOG.md`

<More than four rounds means the art direction is under-specified, not that the model is
bad. Escalate to art-director with what the results keep getting wrong.>
