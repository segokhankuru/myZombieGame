---
name: systems-design
description: Fully specifies one system - intent, rules, states, tunables, interactions and edge cases - to the level a programmer can build from without asking questions.
---

# /systems-design <system name or SYS-NN>

Phase 1. Owner: `game-designer`, with `systems-designer` on the tunables.
Produces `design/systems/SYS-NN-<slug>.md`.

This document is the source the story packets are cut from. Its quality decides how many
rounds `/dev-task` takes, which makes it the highest-leverage document in production.

---

## 1. Locate

Find the system row in `design/GDD.md`. No argument -> list unspecified systems and ask
which. Prefer the ones that block others.

## 2. Two parallel calls (one message)

### `game-designer` - behaviour
```
<PILLARS.md>
<the core loop>
<the GDD row for this system, plus any adjacent system rows it touches>

Task: specify SYS-NN fully.
1. Intent: one paragraph on what experience this system exists to produce.
2. Rules R-1..R-n, each stated so it can be implemented and tested.
3. States: entered when, exited when, what the player can do in each.
4. Interactions with other systems, and who wins when they conflict.
5. Edge cases: what happens at the boundaries, at zero, at maximum, when interrupted,
   when done twice, when done at the wrong time.
6. The one thing the player must understand without a tutorial, and how the system
   teaches it.
7. Tunables: what needs a number, what it controls, the SHAPE of the curve, and a
   suggested range. Do NOT pick values.
8. Out of scope: what this system explicitly does not do.

The edge cases section is what a programmer will actually read. Do not shorten it.
```

### `systems-designer` - the tuning surface
```
<the GDD row and the loop>

Task: for the system above, define the config surface.
1. Every key: name (with the unit in the name), what it controls, type.
2. The valid range for each, and one sentence on what the PLAYER experiences outside it.
3. Which keys interact - changing A meaningfully changes what B should be.
4. Which key is the one a designer will actually reach for when the system feels wrong.

Output as a schema-ready table. Do not write the values yet.
```

## 3. Reconcile (you)

The designer's tunable list and the systems designer's key list will not match exactly.
Where they differ, take the systems designer's naming (units in names) and the designer's
intent. Flag anything either one invented that the other did not ask for.

## 4. Present and approve

```
## SYS-NN: <name>
Intent: <one line>
Rules: <n>    States: <n>    Tunables: <n>
Teaches: <the one thing>

Edge cases covered: <n>
| Boundary | Behaviour |

Config keys
| Key | Controls | Range | Why that range |

Interacts with: <systems>   Conflicts resolved by: <rule>
Out of scope: <list>
```

## 5. Write

- `design/systems/SYS-NN-<slug>.md`
- Update the GDD row to `specified`
- **Do not write config yet.** `/data-schema <domain>` turns the table into a schema and
  `/economy` picks the values. Keeping specification and valuation separate is what makes
  tuning cheap later.

## 6. Close

```
✓ SYS-NN specified. <n> rules, <n> states, <n> tunables.

Riskiest rule: R-<n> - <why: usually the one with the most edge cases>

▶ Next: /data-schema <domain>   turn the tunables into a real config layer
   or:   /systems-design <next>  if more systems block production
   or:   /epics                  if this system is ready to be built
```

---

## Token note

- **Two agent calls, parallel, one message.**
- Specify systems on demand, not all at once. A system specified six months before it is
  built is specified against a design that has since changed.
- This document gets **copied into story packets**, which is why the effort here pays for
  itself: it is the difference between a story a programmer can execute and one they have
  to research.
