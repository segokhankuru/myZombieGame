---
name: game-ux
description: Designs HUD, menu flows, onboarding, input mapping and accessibility, with the failure states most UX specs forget.
---

# /game-ux [screen or flow]

Phase 1-2. Owner: `game-ux-designer`. Produces `design/ux/`.

---

## 1. Scope

No argument -> the HUD and the input map, which everything else depends on.
With an argument -> that screen or flow.

## 2. One call - `game-ux-designer`

```
<the core loop>
<the SYS-* docs for whatever the player needs to see>
<docs/art/ART-BIBLE.md readability contract, if it exists>

Task: design <scope>.

1. For every piece of information the player needs: can it be diegetic (in the world)?
   If not, HUD. If not, text. If not, a prompt. Move each one as far up that list as it
   goes, and say why it stopped where it did.

2. HUD table: element, information, position, when shown, how it changes, readable at
   what distance and resolution.

3. Attention budget: at most three things may compete at once. List them with priority.
   If there are four, one of them is not as important as its owner thinks.

4. Every screen in this flow with all four states: normal, loading, empty, error.
   A flow without failure states is half a flow.

5. Input map: action, keyboard, gamepad, hold or tap, rebindable, and what it conflicts
   with. Context-sensitive bindings get a context table - same button, different meaning
   is the top cause of "the game did something I did not ask for".

6. Safe area: what survives 16:9, 21:9, 4:3 and Steam Deck 1280x800.

7. Accessibility baseline: no information by colour alone, subtitles on by default with
   speaker names, full rebinding, hold alternatives for anything mashed, camera shake and
   motion blur toggles.
```

## 3. Onboarding check (you)

If the design needs a tutorial popup for a core mechanic, that is a **level design
problem wearing a UI costume**. Escalate to `level-designer` before accepting the popup.
The first level should teach; a popup is what you write when it did not.

## 4. Present

```
## <scope>
Diegetic <n> | HUD <n> | Text <n> | Prompt <n>
Attention budget: <the three, by priority>

Screens: <n>, all with four states
Input: <n> actions, <n> context-sensitive, all rebindable
Safe area verified for: <ratios>

Tutorial popups needed: <n>  <if >0: which mechanic, and why the level cannot teach it>
```

## 5. Write

`design/ux/hud.md`, `design/ux/flows/<name>.md`, `design/ux/input-map.md`,
`design/ux/accessibility.md`. Give `narrative-designer` the character limits - they will
need them and guessing produces clipped text.

## 6. Close

```
✓ <scope> designed. <n> screens, <n> states, <n> actions.

Watch: <the element most likely to be unreadable in motion>

▶ Next: /stories <epic>   the UI programmer builds from this
   or:   /game-ux <next flow>
```

---

## Token note

- **One agent call.**
- The four-states rule is what stops UI stories coming back. Specifying them here costs
  ten lines; discovering them in QA costs a story each.
