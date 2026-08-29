# C# Rules (all Unity code)

**Scope:** every `.cs` file under `Assets/_Project/Code/`.
These apply on top of the path-specific rules file.

## Allocation
- **Zero allocation per frame** in anything running in `Update`, `FixedUpdate`,
  `LateUpdate` or a per-entity tick.
- No LINQ, no `new`, no string concatenation, no boxing, no closures allocated per frame.
- Cache `GetComponent` results in `Awake`. Never call it, `Find` or `Resources.Load`
  in a per-frame path.
- Collections are allocated once and reused. Clear, do not recreate.
- `StringBuilder` or a cached string for anything displayed and changing.

## Structure
- `[SerializeField] private` over `public`. A public field is an API you did not design.
- One class, one responsibility. If the name needs "and", split it.
- Prefer `struct` for small value types under 16 bytes.
- `readonly` where it holds. `sealed` on classes not designed for inheritance.
- No inheritance chain deeper than two, unless Unity forces it.
- Constructor or `Awake` injection over service lookup, so tests can substitute.

## Unity lifecycle
- `Awake` builds internal state. `Start` reads other objects' state. Do not mix them.
- `OnDestroy` undoes exactly what `Awake` did: unsubscribe, release, stop.
- Physics in `FixedUpdate`. Input reading in `Update`. Camera in `LateUpdate`.
- Every started coroutine has a stop path. Every subscription has an unsubscribe.

## Errors
- No empty `catch`. Handle, rethrow, or log with context.
- Fail loudly at boot for missing configuration or required references. Silent defaults
  hide bugs for weeks.
- `Debug.Log` is not free in a player build. Never in a hot path; guard debug output.

## Determinism
- No `Random` without an explicit seeded stream for anything replayable, shareable or
  savable.
- One clock. Do not scatter `Time.time` across systems.

## Forbidden
- A balance number hardcoded in code (see `config-data.md`)
- `GameObject.Find`, `SendMessage`, `Resources.Load` in new code
- Unowned `TODO` - write `TODO(role, story-NNN)` or file it with `/bug`
- Commented-out code
- The same business rule in two places
- A new package or third-party asset without an ADR
