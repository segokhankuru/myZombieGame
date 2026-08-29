---
name: patch-notes
description: Writes player-facing patch notes and appends to CHANGELOG.md. Templated, high volume, runs on the cheapest model.
---

# /patch-notes [version]

Phase 5. Owner: `tech-writer` (haiku).

---

## 1. Gather what changed

From the stories marked DONE since the last release, plus `docs/DECISIONS.md` for tuning
changes. Take facts from the stories - never infer what a change does from the code.
If a story does not say what the player will notice, ask rather than guess.

## 2. One call - `tech-writer`

```
<the DONE stories: title, type, and the "what to build" paragraph>
<balance changes from docs/DECISIONS.md, with their "player should feel" lines>
<open bugs that were fixed, with their symptom titles>

Task: patch notes for <version>.
Lead with what the player can now do. Never use an internal name.

### New      - what the player can now do
### Changed  - what is different, and what it means for them
### Fixed    - the symptom they experienced, not the cause

Rules:
- An invisible change is not a patch note. Refactors, pooling, architecture: omit them
  unless the player notices the stutter is gone.
- "Fixed NRE in GrabHandler" -> "Fixed a crash when dropping an item while climbing".
- Balance changes are ALWAYS player-facing. Say what got easier or harder and roughly by
  how much. Players find out anyway, and they trust notes that admit it.
- The "player should feel" lines from the tuning log are the best source for this section.
```

## 3. Present

```
## <version> - <date>

### New
- <...>
### Changed
- <...>
### Fixed
- <...>

Omitted as invisible: <n> changes
```

Show what was omitted. Occasionally one of them was actually player-visible and the story
did not say so.

## 4. Write

Append to `CHANGELOG.md`, newest at the top. **Append, never rewrite** - rewriting loses
the record and invalidates the cached prefix.

Copy the player-facing version into `docs/publishing/` for the Steam announcement, which
usually wants a shorter and warmer version of the same list.

## 5. Close

```
✓ <version>: <n> new, <n> changed, <n> fixed.
CHANGELOG updated.

▶ Next: /release <version>   or the Steam announcement
```

---

## Token note

- **One call on haiku.** This is mechanical, templated, repeated work - exactly what the
  cheapest model is for. Escalating it to a larger model buys nothing.
