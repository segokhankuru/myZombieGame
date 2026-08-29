# Test Code Rules

**Scope:** `Assets/_Project/Code/Tests/**`

## Naming
- One test per acceptance criterion, named for it:
  `AC3_LateJoinerSeesPlacedFurniture`. The name must say what broke without opening
  the file.
- Regression tests are named for the bug: `BUG042_DropWhileClimbingDoesNotCrash`.

## EditMode first
- EditMode wherever possible: milliseconds, no scene, no Editor session cost.
- If a rule can only be tested in PlayMode, the production code is in the wrong place -
  push the logic into a plain class and say so in the story.
- PlayMode is for seams only: scene flow, save/load, spawning, physics interaction,
  networked behaviour.

## Determinism
- Fixed seeds. Fixed timestep. No assertion that depends on `Time.deltaTime` or on
  frame ordering.
- No `Thread.Sleep`, no real-time waits. `yield return null` for a frame,
  `yield return new WaitForFixedUpdate()` for physics.
- **A flaky test is worse than no test.** It trains everyone to ignore failures. Fix the
  determinism or delete the test and say which.

## Isolation
- Tear down everything created. A PlayMode test leaking a `DontDestroyOnLoad` object
  poisons every test after it.
- Load a dedicated test scene, never a gameplay scene. Gameplay scenes change under you
  and take the tests with them.
- No shared mutable static state between tests.

## Assertions
- Every test asserts. A test that only runs code is a smoke check - label it as one.
- Assert the observable behaviour, not the implementation. A test that breaks when a
  private method is renamed is a maintenance tax with no safety benefit.
- One logical assertion per test. Several `Assert` calls proving one fact is fine.

## Coverage that matters
- Every `AC-N`, and an edge-case test per business rule: zero, negative, maximum,
  interrupted, repeated, called during a scene load.
- Save migration: one fixture per shipped version in `Tests/EditMode/Fixtures/`, kept
  forever.
- Net: two instances, including a late joiner. A single-instance netcode test proves
  nothing.

## Forbidden
- `[Ignore]`, `[Explicit]` or a commented-out test left in the suite
- Changing production code to make a test pass, without the owner's decision
- Reporting "tests pass" without running them
