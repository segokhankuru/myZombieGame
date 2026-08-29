# Asset and Art Rules

**Scope:** `Assets/_Project/Art/**`, `Assets/_Project/Prefabs/**`

## Naming
`<category>_<subject>_<variant>` - lowercase, underscores, ASCII only.

```
ok:  tex_crate_wood_01   sfx_ui_click_01   prefab_enemy_scout   mat_metal_brushed
no:  Crate Final v2      TEX_Crate          crate-wood           Kutu_01
```

Non-ASCII names break on other machines, in build pipelines and in Steam depots. This is
not a style preference.

## Import settings are code, not discipline
- Enforced by the `AssetPostprocessor`, keyed by folder. Never set by hand.
- Textures: power of two, compressed, mip maps **off** for UI, sRGB **off** for masks
  and data textures, read/write **off** unless something genuinely needs it - it doubles
  memory.
- Audio: streaming for music, decompress-on-load for short frequent SFX, compressed in
  memory for the rest.
- Models: mesh compression on, read/write off, rig stripped if unanimated.

## Budgets
Every asset belongs to a category in `docs/art/ASSET-BUDGET.md` with a texture cap, a
triangle cap and a material count. Over budget is a finding with a name, not a note.

Check with `.claude/tools/asset-index.ps1 -Heavy`.

## Generated art
- Nothing under `Art/Generated/` ships. Promotion is deliberate: crop, alpha, resize to
  power of two, rename, move, import preset, provenance line.
- Every promoted asset gets a line in `docs/art/ASSET-LOG.md` with its seed and prompt.
  That is what makes it regenerable at a different resolution months later.
- The style comes from the lock, never from a per-asset prompt. A style word in a subject
  prompt is a lock change in disguise.

## Marketplace assets
- Licence checked **before** import, and recorded.
- Triangle and texture budget checked before import, not after.
- Demo scenes, unused textures and example scripts stripped on arrival.
- An unreviewed pack is usually the single largest thing in the project.
- Third-party folders are **never edited**. Patch by ADR or not at all.

## Prefabs
- Variants over duplicates.
- Serialized fields expose **references**, not balance numbers. Numbers come from config.
- Nesting beyond three levels needs a stated reason.
- One prefab, one owning story at a time.

## Never
- Hand-edit a `.prefab`, `.unity`, `.asset` or `.meta` file
- Commit generated art that has not been promoted
- Ship a texture larger than its category cap without a recorded decision
