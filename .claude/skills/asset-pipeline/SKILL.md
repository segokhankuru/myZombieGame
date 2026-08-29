---
name: asset-pipeline
description: Sets the asset budgets, naming rules and import presets, and builds the postprocessor that enforces them automatically.
---

# /asset-pipeline

Phase 2. Owner: `technical-artist` with `tools-programmer`.
Produces `docs/art/ASSET-BUDGET.md` and an `AssetPostprocessor`.

A budget nobody enforces is a wish. This skill exists to make enforcement automatic,
because the alternative is discipline, and discipline loses to a busy week.

---

## 1. Measure what is already there

```powershell
.claude\tools\asset-index.ps1
.claude\tools\asset-index.ps1 -Heavy
```

On an existing project this is usually uncomfortable and useful: a marketplace pack is
often the single largest thing in the repository, and nobody has looked.

## 2. One call - `technical-artist`

```
<asset-index output: totals by category, top-level folders, over-budget list>
<target hardware and the frame budget from PERF-BUDGET.md>
<docs/art/ART-BIBLE.md, if it exists>

Task: the asset budget.
1. Categories with a max texture size, max triangles, material count and a note on why.
   Categories a player sees up close get more; set dressing gets far less than people
   expect.
2. Import presets per folder: compression, mip maps, sRGB, read/write, max size.
   Mip maps off for UI. sRGB off for masks. Read/write off unless something needs it -
   it doubles memory.
3. The naming rule, with three examples and three counter-examples.
4. The LOD policy: which categories get LODs, at what distances, decided now rather than
   during optimization.
5. Atlas plan: what is drawn together should live together.
6. For the existing overruns: which matter, which are third-party and can be stripped,
   and which are fine.
At most 30 lines plus the tables.
```

## 3. Enforce it - `tools-programmer`, one call

```
<the budget tables and import presets>
<.claude/docs/unity-conventions.md section 5>

Task: an AssetPostprocessor in Assets/_Project/Code/Editor/.
1. Apply the import preset by folder, on import. Never require a human to set these.
2. Warn on import when an asset exceeds its category budget - name the asset, the
   category, the actual and the cap.
3. Warn on a name that breaks the convention.
4. A menu command that re-applies presets across the project, for the first run.
5. Never modify .meta files directly - use the importer API.
Idempotent. Re-running must not change anything that is already correct.
```

## 4. Present

```
## Asset pipeline

Budget
| Category | Texture | Tris | Materials | Why |

Import presets
| Folder | Preset | Enforced by |

Naming: <category>_<subject>_<variant>
  ok:  tex_crate_wood_01, sfx_ui_click_01, prefab_enemy_scout
  no:  Crate Final v2, TEX_Crate, crate-wood

Current state: <n> assets over budget, <n> MB recoverable
  <the top 3, with what to do about each>
```

## 5. Apply and verify

Run the menu command once, then re-measure:

```powershell
.claude\tools\asset-index.ps1 -Heavy
```

Report before and after. If nothing changed, the postprocessor is not wired up - check
that before declaring this done.

## 6. Close

```
✓ Pipeline set. <n> categories, <n> presets, enforced on import.
Recovered: <n> MB   Still over: <n> assets
Third-party to strip: <list>

▶ Next: /gen-asset   new art now lands correctly by default
   or:   a cleanup story for the existing overruns
```

---

## Token note

- **Two agent calls.**
- Doing this before generating assets means every later asset is correct on arrival.
  Doing it after means a cleanup pass over everything - the same work, done twice, by hand.
