# AI Code Rules

**Scope:** `Assets/_Project/Code/AI/**` (assembly `Game.AI`)
Read with `csharp-code.md`.

## Legibility over intelligence
- The player must be able to infer what an NPC knows and why it acted. An NPC whose
  reasoning cannot be inferred is a random number generator with animation.
- **Telegraph before act.** Every meaningful action has a wind-up the player can read
  and respond to. The wind-up duration is a config value.
- Predictable beats optimal. An agent that always makes the best decision is unbeatable
  and unfun. The mistakes are designed, not accidental.

## Perception has rules the player can learn
- Cone angle, range, reaction delay, memory duration, loss-of-sight time - all from
  config, all consistent across agents of the same type.
- Perception is evaluated on a budget, not every frame for every agent.
- What the agent knows is explicit state, never a direct read of the player's transform.

## Behaviour is data
- Trees, states and utility curves come from `config/content/`, so a designer can tune
  them without a recompile.
- No behaviour hardcoded in a class hierarchy that requires a programmer to adjust.

## Budget
- Time-slice thinking. Not every agent evaluates every frame.
- Cull by distance and relevance. An off-screen agent thinks less or not at all.
- State the per-agent think cost and the agent count at budget, in the story.
- Spawning is pooled and bounded. An unbounded spawner is a frame-rate bug scheduled
  for later.

## Navigation
- Every navigation failure is a defined state: off-mesh, stuck, unreachable target,
  destination inside geometry. All four happen.
- Every agent has a stuck-recovery path. Shipping without one guarantees a bug report
  with a video.
- Path requests are throttled and asynchronous. Never a synchronous path in `Update`.

## Groups
- Group behaviour needs an arbiter. Five agents each independently choosing the best
  flanking position will all choose the same one.
- Reservation or assignment, not independent optimization.

## Debug
- Every agent exposes current state, current target, perception radius, last decision
  and why, behind a debug flag. It pays for itself in the first playtest.

## Tests
- EditMode tests for decision logic, constructed without a scene.
- PlayMode tests for navigation and perception against a fixture scene.
