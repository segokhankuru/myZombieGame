---
name: tune
description: Changes balance values in config, with the design reason and the predicted felt effect. The daily loop of shipping a game.
---

# /tune <domain> [key]

Phase 3+. Owner: `systems-designer`. Cheap by design - most runs are a single edit.

---

## 1. Show what is there

Read `config/balance/<domain>.json` and `config/schema/<domain>.schema.json`. Present the
values **with their design intent**, because the intent is the thing that stops a change
being a guess:

```
<domain> v<n>, last tuned <date>

| Key | Value | Range | What the range protects |
| startingCurrency | 250 | 0-2000 | above ~800 the first ten minutes lose their tension |
| patience.seconds | 180 | 30-600 | under 90 the game reads as unfair rather than tense |
```

If a key has no description in the schema, say so. A range without a reason is a fence in
a field, and tuning against it is guessing.

## 2. Establish why we are here

One of:
- **A playtest finding** - quote it from `docs/qa/playtests/`. Best case.
- **A `/balance-check` result** - the curve breaks somewhere.
- **A hunch** - fine, but say so, and prefer a smaller change.

If there is no reason at all, stop and ask. Tuning without a reason is how a game drifts
away from its design one plausible change at a time.

## 3. Propose - `systems-designer`, one call

Skip the agent entirely for a single obvious value inside range with a clear playtest
reason. Call it when the change touches interacting keys or a curve shape.

```
<the current values and their descriptions>
<the reason: playtest quote, balance-check output, or the stated hunch>
<the curve entry from design/economy/curves.md if there is one>

Task: propose the change.
For each key you would change:
  WAS -> NOW
  BECAUSE: <the observation, not the intuition>
  PLAYER SHOULD FEEL: <what is different in the experience>
  KNOCK-ON: <which other keys this makes wrong, if any>
Then: what observation in the next playtest would tell us this was the wrong change?
At most 15 lines.
```

## 4. Apply

Edit `config/balance/<domain>.json`. Bump `version`. Update `_meta.lastTuned`.

The `PostToolUse` hook validates automatically. If it reports an out-of-range value,
**do not widen the range to fit the value** - that is how ranges stop meaning anything.
Either the value is wrong, or the design changed and the range needs a new reason. Say
which.

## 5. Record

Append one line to `docs/DECISIONS.md`:

```
| <date> | <domain>.<key> 250 -> 400 | systems-designer | <because> | /tune |
```

If the change came from a playtest, note it in that playtest file too, so the loop from
observation to change is traceable.

## 6. Close

```
✓ <domain> v<n+1>

| Key | Was | Now | Player should feel |

Knock-on: <keys that may now need attention, or none>
Wrong if: <what you would see in the next playtest>

▶ Next: /balance-check <domain>   simulate before anyone plays it
   or:   run the importer, then play it
```

---

## The rule this skill exists to enforce

Every tuning change carries three lines: **what changed, why, and what the player should
feel differently.** The third line is the one people skip and the one that matters. A run
of changes with no predicted effects is not tuning, it is stirring - and after twenty of
them nobody can say what the game is supposed to feel like any more.

---

## Token note

- **Zero or one agent calls.** Most tuning is a value edit against a documented range.
- No Unity involvement. The whole point of the config layer is that tuning does not
  require opening the Editor.
