---
name: balance-check
description: Simulates a config domain's curves over a full session and reports where they break, before a player finds out.
---

# /balance-check <domain>

Phase 4. Owner: `systems-designer`.

Cheap, fast, and it runs without opening Unity - that is the whole point of keeping
tuning in JSON.

---

## 1. Validate first

```powershell
.claude\tools\config-validate.ps1 -Domain <domain>
```

Out-of-range values are found here, not by simulation. If it fails, fix that first: there
is no point simulating a config the game would refuse to load.

## 2. Establish the session shape

From `design/economy/curves.md` and the brief:
- How long is an intended session?
- What is the player doing at minute 0, 10, 30 and at the end?
- What route does a competent player actually take? Not the average route - the good one.

## 3. One call - `systems-designer`

```
<config/balance/<domain>.json, the values>
<the schema descriptions - what each range protects>
<design/economy/curves.md for this domain>
<the intended session length and the player's route>
<any playtest observations about this domain>

Task: simulate this domain over a full session.
1. Walk the numbers minute by minute for the intended session. Table: minute, what the
   player has, what they can afford, what they are doing.
2. Where does the curve break? Inflation, a wall they cannot pass, a point where nothing
   is worth buying, a state they cannot recover from.
3. The dominant strategy: the best route, how far ahead of the second best, and whether
   the gap is intentional. Every economy has one - find it before a player does and
   posts it.
4. The first ten minutes specifically. Most balance errors are visible there and
   invisible in a spreadsheet of the whole session.
5. The three keys most worth changing, each with the value and the predicted felt effect.

Show the arithmetic. A conclusion without the walk is an opinion.
```

## 4. Check the arithmetic yourself

Re-do the first ten minutes by hand. This is not distrust - it is the cheapest error
check available, and arithmetic over many steps is exactly where a language model is
least reliable. If the walk disagrees with the agent, trust the walk.

## 5. Present

```
## Balance - <domain>

Session <n> min, route: <the competent route>

| Minute | Has | Can afford | Doing |
| 0 | 250 | nothing | learning |
| 12 | 410 | first upgrade | as designed |
| 30 | 1900 | everything | PROBLEM: nothing left to want |

Breaks at: minute <n> - <what happens>
Dominant strategy: <route>, <n>% ahead - <intentional | not>

Suggested
| Key | Now | To | Player should feel |
```

`AskUserQuestion`: `Apply via /tune (Recommended)` / `Change the curve shape instead` /
`Leave it - a playtest will tell us more`

The third option is often right. A simulation is a model of a player, and models are
confidently wrong in ways real people are not.

## 6. Record

`design/economy/curves.md` gets the break points. If values change, that goes through
`/tune`, not through a direct edit - the felt-effect line is not optional.

## 7. Close

```
✓ <domain> simulated over <n> minutes.
Breaks at: <n>   Dominant strategy: <n>% ahead
Applied: <n> changes   <or: nothing, waiting on a playtest>

▶ Next: /tune <domain>   or   /playtest   to check the model against a person
```

---

## Token note

- **One agent call.** No Unity, no build, no capture.
- Runs in seconds and catches the class of problem that otherwise takes a playtest to
  find. Run it after every `/economy` and before every milestone playtest.
