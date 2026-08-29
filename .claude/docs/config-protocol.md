# Configuration Protocol - How the game is tuned

Every number a designer would ever want to change lives in **JSON under `config/`**,
never in C#, never typed into an Inspector field. This is what makes the studio able to
*configure* the games it builds instead of only writing them.

---

## 1. The three layers

```
config/schema/<domain>.schema.json     Shape + valid ranges          owner: systems-designer
config/balance/<domain>.json           The numbers                   owner: systems-designer
config/content/<domain>.json           Content instances (items, enemies, levels)
        |
        |  /tune, /balance-check, config-validate.ps1  operate here (cheap, plain JSON)
        v
Assets/_Project/Config/*.asset         ScriptableObjects, generated  owner: tools-programmer
        |
        |  ConfigImporter (editor script) - never hand-edited
        v
Runtime                                 systems read the SO, never the JSON at runtime
```

**Direction is one-way.** JSON is the source of truth; ScriptableObjects are build
output. An agent that edits a `.asset` file directly has broken the pipeline.

Why this shape:
- JSON is diffable, mergeable, greppable and cheap to read. Unity `.asset` YAML is none
  of those.
- Balance changes become one-line diffs a human can review.
- `/tune` and `/balance-check` can run without opening Unity at all.
- The same JSON can be shipped to `StreamingAssets` for live tuning or modding.

---

## 2. File conventions

`config/balance/<domain>.json`

```json
{
  "$schema": "../schema/economy.schema.json",
  "version": 3,
  "_meta": {
    "owner": "systems-designer",
    "system": "SYS-ECON-01",
    "lastTuned": "2026-08-29"
  },
  "startingCurrency": 250,
  "deliveryPayout": { "base": 40, "perFloor": 8, "perkgMultiplier": 0.35 },
  "customerPatience": { "seconds": 180, "impatientAt": 0.4 }
}
```

Rules:
- Every file names its schema and carries an integer `version`.
- Every key is a **noun with a unit implied by its name** - `patienceSeconds`, not
  `patience`. Ambiguous units are the top source of balance bugs.
- No computed values. If B is always A times 2, store A and compute B in code.
- No booleans that gate whole features; feature flags live in
  `config/balance/features.json` and are listed in the GDD.
- Comments are not valid JSON: use `_note` keys, sparingly.

---

## 3. Schema files

Each domain has a JSON Schema with **ranges**, because a range is design intent:

```json
{
  "type": "object",
  "required": ["startingCurrency", "deliveryPayout"],
  "properties": {
    "startingCurrency": { "type": "integer", "minimum": 0, "maximum": 5000,
      "description": "Cash the player starts a run with. Above 1000 removes early tension." }
  }
}
```

The `description` is where the designer records *why the range is what it is*. That
sentence is what stops a later agent from tuning the fun out of the game.

`config-validate.ps1` enforces types, required keys and ranges. It runs in the
`PostToolUse` hook, so an out-of-range value is caught the moment it is written.

---

## 4. Who may write what

| Path | Owner | Everyone else |
|---|---|---|
| `config/schema/**` | `systems-designer` (approval `game-designer`) | proposes |
| `config/balance/**` | `systems-designer` | proposes via `/tune` |
| `config/content/**` | `game-designer` + `level-designer` | proposes |
| `Assets/_Project/Config/**` | generated only | never hand-edited |
| `Code/Systems/Config/*.cs` | `tools-programmer` | - |

A gameplay programmer who needs a new tunable **does not add a serialized field**.
They add the key to the schema, escalate to `systems-designer` for a value, and read it
from config.

---

## 5. The runtime contract

```csharp
// Generated. Do not edit by hand.
public sealed class EconomyConfig : ScriptableObject
{
    [SerializeField] private int startingCurrency;
    public int StartingCurrency => startingCurrency;
}
```

- Config objects are **read-only at runtime**. A system that mutates config has a bug.
- Config is injected, not fetched: `Systems` resolves it once at boot and hands it down.
  This keeps EditMode tests free of Unity asset loading.
- A missing key is a **hard failure at boot**, not a silent default. Silent defaults
  make balance bugs invisible for weeks.

---

## 6. Tuning workflow

```
/tune economy                 -> shows the current values plus their design intent,
                                 proposes a change, writes the JSON, re-validates
/balance-check economy        -> simulates curves over the intended session length
                                 and reports where the curve breaks
/dev-task <story>             -> reads the relevant config keys straight from the packet
```

After a tuning change, the story or note records: **what changed, why, and what the
player should feel differently**. A balance change without a felt-effect statement is
a guess, and guesses are how games drift.

---

## 7. Versioning and saves

- Increasing `version` in a balance file is free.
- Changing the **shape** (renaming or removing a key) requires:
  1. a schema bump,
  2. an entry in `docs/DECISIONS.md`,
  3. a save migration if the key was persisted.

Content ids (`item.crate_small`) are **permanent**. Renaming an id breaks saves,
telemetry history and player mods. Add a `displayName` instead.
