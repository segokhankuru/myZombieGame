# Config and Data Rules

**Scope:** `config/**`, `Assets/_Project/Config/**`, `Assets/_Project/Data/**`
Full protocol: `.claude/docs/config-protocol.md`.

## Direction is one-way
```
config/schema/*.json  ->  config/balance/*.json  ->  generated ScriptableObject  ->  runtime
```
JSON is the source of truth. The `.asset` is build output. **Editing a generated
`.asset` breaks the pipeline** - the JSON and the engine then disagree and nobody knows
which one the game is running.

## Every file
- Names its `$schema` and carries an integer `version`.
- Carries `_meta` with `owner`, `system` and `lastTuned`.
- No comments (not valid JSON). Use `_note` keys, sparingly.

## Key naming
- **The unit is in the name**: `patienceSeconds`, `speedMetersPerSecond`,
  `payoutPerFloor`. Ambiguous units are the single most common balance bug in games.
- Lowercase camelCase. Group related keys into objects rather than flat files of forty.
- Content ids are **permanent**: `item.crate_small`. Renaming an id breaks saves,
  telemetry history and player mods. Add a `displayName` instead.

## No computed values
If B is always A times two, store A and compute B in code. Two stored values that
should agree will eventually not.

## Schema is design intent
Every property carries `minimum`, `maximum` and a **`description` saying what the player
experiences outside the range**. That sentence is what stops a later agent tuning the fun
out of the game. A range without a reason is a fence in a field.

`additionalProperties: false` - the schema is a contract, not a suggestion.

## Runtime
- Config is **read-only** at runtime. A system that mutates config has a bug.
- Injected once at boot, handed down. Never fetched from a static, so EditMode tests
  need no Unity asset loading.
- A missing required key is a **hard failure at boot** with the schema path in the
  message. Never a silent default - silent defaults hide balance bugs for weeks.

## Changing shape
Renaming or removing a key requires: a schema `version` bump, a line in
`docs/DECISIONS.md`, and a save migration if the key was persisted. All three, in the
same change.

## Changing values
Through `/tune`, with three lines: what changed, why, and what the player should feel
differently. A change without a predicted felt effect is a guess.

## Validation
`config-validate.ps1` runs in the `PostToolUse` hook. If it reports out of range, **do
not widen the range to fit the value** - either the value is wrong or the design changed
and the range needs a new reason. Say which.
