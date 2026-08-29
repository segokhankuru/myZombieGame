# Systems Code Rules

**Scope:** `Assets/_Project/Code/Systems/**` (assembly `Game.Systems`)
Read with `csharp-code.md`.

## Dependencies
- `Systems` depends on **no other game assembly**. It is the floor.
- If you need something from `Gameplay`, the dependency points the wrong way. Invert it
  with an interface defined in `Systems` and implemented in `Gameplay`.

## Boot contract
- After the Boot scene, a documented set of services exists. Nothing may assume more,
  nothing may assume less. The contract is in `ARCHITECTURE.md`; changing it is an ADR.
- Services are resolved once and handed down, not fetched from a static. EditMode tests
  must be able to construct them without loading a Unity asset.

## Config
- Runtime reads the generated ScriptableObject. Never JSON at runtime, never the
  `config/` folder at runtime.
- Config objects are **read-only**. A system that mutates config has a bug.
- A missing required key is a **hard failure at boot**, with the schema path in the
  message. Never a silent default.

## Save
- Every save carries an integer `version`.
- A schema change ships with its migration, in the same story, plus a test that loads a
  fixture from each previous shipped version. Fixtures live in
  `Tests/EditMode/Fixtures/` forever.
- Persist stable content ids from `config/content/`, never Unity object references and
  never display names. Ids are permanent; names are not.
- Write to a temp file, then move. A crash mid-write must not destroy the previous save.
- Loading an older save must not throw. Loading a newer one must fail with a clear
  message.

## Scene flow
- One transition owner. Two systems calling `LoadSceneAsync` is a race with a long fuse.
- Every transition defines: loading, failed, cancelled, already-there.
- Additive levels unload deterministically. Prove it with a load-unload-load test -
  that is where leaks appear.

## Pooling
- Pools have an owner and an explicit reset contract. A pooled object carrying state
  from its previous life is a bug that surfaces after an hour of play.
- Pools are bounded, and the behaviour at the bound is defined.

## Forbidden
- A game rule in `Systems`. Rules belong in `Gameplay`.
- Choosing a balance value - that is `systems-designer`.
- Hand-editing generated ScriptableObjects in `Assets/_Project/Config/`.
- Changing the save format without an ADR and a migration in the same change.
