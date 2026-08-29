# Shader and Graphics Rules

**Scope:** shaders, materials, render features, VFX, `Assets/_Project/Settings/**`

## Measure, do not estimate
- Every visual change reports before and after in the frame budget's units: p50, p99,
  draw calls, setPass. A change without a measurement on both sides is a guess.
- Optimize only what is over budget. Optimizing something that fits makes code
  unreadable for nothing.

## Cost order (where the frame usually goes on an indie PC title)
1. **Draw calls and setPass calls** - batch, atlas, instance
2. **Overdraw from transparency** - stacked alpha and full-screen effects eat frames
   geometry never touched
3. **Real-time lights and shadows** - bake anything that does not move
4. **Post-processing** - every effect is per-pixel, always, over the whole screen
5. Triangle count - usually last on this class of hardware

## Shaders
- Shader Graph for anything an artist will touch. Hand-written HLSL only where Graph
  cannot express it, with the reason in the file header.
- Every custom shader declares its quality tier and its fallback.
- **Strip variants aggressively.** Every keyword multiplies compile time and build size,
  and the first appearance of an unstripped variant is a visible stutter.
- No per-pixel work that could be per-vertex. No branches on a uniform that could be a
  variant.
- Warm up shaders at load. A shader compiled on first appearance is a hitch the player
  blames on the game.

## Materials
- Share materials. A material instance per object is a draw call per object.
- `MaterialPropertyBlock` for per-instance values, never `renderer.material` - that
  clones and leaks.
- Materials come from the art bible's material families, not invented per asset.

## Lighting
- Bake what does not move. One well-placed baked bounce beats three dynamic lights.
- Light probes for dynamic objects in baked scenes.
- Shadow distance and cascade count are budget items with numbers, not defaults.

## VFX
- Pool particle systems. Cap lifetime and max particles on every system.
- No unbounded emission. Ever.
- Sub-emitters and collision are expensive - justify each use in the story.

## Pipeline settings
- Per-quality-level pipeline assets in `Assets/_Project/Settings/`.
- Never branch behaviour on `Application.isEditor`.
- Changing the render pipeline is an ADR, not a story.
