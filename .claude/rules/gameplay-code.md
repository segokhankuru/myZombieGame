# Gameplay Code Rules

**Scope:** `Assets/_Project/Code/Gameplay/**` (assembly `Game.Gameplay`)
Read with `csharp-code.md`.

## Dependencies
- `Gameplay` may depend on `Systems`. It may **not** depend on `UI`, `Net`, `AI` or
  `Editor`. The asmdef enforces this; if you need a reverse dependency, invert it with
  an interface or an event.
- Gameplay never assumes a network. It must run and be tested single-player.

## Numbers
- **Every gameplay number comes from config.** Speeds, damage, durations, cooldowns,
  thresholds, capacities, patience, payouts.
- A `[SerializeField] float` with a default is a review BLOCKER unless the story says it
  is a reference or an engineering constant, and says why in a comment.
- If a needed key is not in the story packet: **stop and escalate to `systems-designer`.**
  Do not invent a value and do not add a field.

## State
- State machines, not boolean fields. Three interacting booleans is eight states, of
  which two have been tested.
- Every state names what the player can and cannot do in it.
- Transitions are explicit and in one place. A transition scattered across three files
  is a bug with a delay fuse.

## Feel
- Acknowledge input on the frame it arrives, even when the result takes longer.
  Unacknowledged input is the most common cause of a game feeling dead.
- Target under 60 ms from input to first visible response; 100 ms is the hard ceiling.
- Acceleration and recovery curves match the implied mass of the object.
- Every action is cancellable or committed, and the player can tell which.
- Coyote time and input buffering wherever a jump, dash or grab is involved.
- Animation follows state. State never waits on animation.

## Physics
- All physics work in `FixedUpdate`. Forces, not transform writes, on rigidbodies.
- Colliders that never move are static. A moving static collider rebuilds the physics
  scene every frame.
- Cache `Physics` query buffers - `NonAlloc` variants, allocated once.
- Never scale a rigidbody at runtime.

## Events
- Data down, events up. Gameplay reads config and Systems services; it raises events for
  UI and audio rather than calling them.
- No gameplay object reaching upward for a manager.

## Tests
- One EditMode test per `AC-N`, named for it.
- Logic lives in plain classes that a test can construct without a scene. If a rule can
  only be tested in PlayMode, the code is telling you it is in the wrong place.
- An edge-case test per business rule: zero, negative, maximum, interrupted, repeated.
