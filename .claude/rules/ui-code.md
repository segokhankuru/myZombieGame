# UI Code Rules

**Scope:** `Assets/_Project/Code/UI/**` (assembly `Game.UI`)
Read with `csharp-code.md`.

## Dependencies
- `UI` may depend on `Gameplay` and `Systems`. Nothing may depend on `UI`.
- A game rule in a UI script is a rule that cannot be tested and will be duplicated.
  Presenters read state and raise intent; they do not decide.

## No polling
- Subscribe to change events. A health bar that reads the value every frame is a
  per-frame cost for something that changes twice a minute.
- Update only when the value actually changed. Compare before assigning.

## Canvas discipline
- Split canvases by update frequency: static, occasional, per-frame.
- One dirty element rebuilds its entire canvas. A single canvas holding the whole HUD
  rebuilds all of it every frame.
- Disable the `Canvas` component, not the GameObject, to hide a screen - it keeps the
  layout intact and avoids a rebuild on re-show.
- `Raycast Target` off on every non-interactive graphic. It defaults to on and it costs.

## Text
- **Every player-facing string is a localization key.** No literals, ever. Retrofitting
  is far more expensive than starting correct.
- Assume translated strings are 40% longer than English. Fixed-width labels clip, and
  clipped text is found at release.
- Never concatenate sentence fragments - word order differs by language. One key, one
  full sentence, with parameters.
- No string allocation per frame. Cache, or update only on change.

## Layout
- Anchors, not absolute positions. A UI laid out at 1920x1080 with fixed positions is
  broken on the second monitor it meets.
- Respect the safe area from `design/ux/hud.md`: 16:9, 21:9, 4:3, Steam Deck 1280x800.
- Reserve the interactive accent colour from the art bible. UI must not use it for
  decoration.

## Navigation and states
- Explicit controller navigation on every interactive element. Unity's automatic
  navigation picks surprising neighbours.
- Every screen implements four states: normal, loading, empty, error. A screen without
  its failure states is half a screen.
- No information carried by colour alone.

## Timing
| Interaction | Budget |
|---|---|
| Button visual response | same frame |
| Screen transition | 150-300 ms, skippable |
| Value animation | 200-400 ms |
| Any wait over 400 ms | needs progress feedback |

## Tests
- PlayMode test per flow, including the failure states.
- Verify at more than one aspect ratio before calling a UI story done.
