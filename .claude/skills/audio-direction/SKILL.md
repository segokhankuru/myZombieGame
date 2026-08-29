---
name: audio-direction
description: Sets the audio direction, bus map, SFX families and the feedback contract that says which player action gets which sound, when.
---

# /audio-direction

Phase 2. Owner: `audio-director`. Produces `docs/audio/AUDIO-BIBLE.md`.

Audio carries more of a game's feel than any other channel and is the one most projects
postpone until it cannot be fixed. Running this in phase 2 costs one agent call; running
it in phase 5 costs a milestone.

---

## 1. Inputs

`design/PILLARS.md`, the core loop, and the `SYS-*` docs for anything the player does
repeatedly. The verbs the player repeats are the sounds that matter.

## 2. One call - `audio-director`

```
<PILLARS.md and the tone table>
<the core loop and the verb list>
<the systems the player interacts with most>

Task: the audio direction.

1. The direction in one sentence, specific enough to reject a sound.

2. Bus map: bus, what it contains, default dB, what ducks it, whether the player gets a
   slider. Every bus a player might want quieter needs a slider - this is accessibility,
   not a preference.

3. SFX families: for each material or event class - the character of the sound, how many
   variants, the pitch randomisation range, and its priority when voices are limited.
   Anything triggered more than a few times a minute needs at least four variants, or
   players mute the game.

4. The feedback contract - the most important table here:
   | Player action | Sound at | Latency budget | What it tells the player |
   The first job of every SFX is to say that something happened and whether it worked.
   Atmosphere is the second job.

5. Music: how it starts, stops and changes. Layered, horizontal, or stingers only.
   And explicitly: where the score is ABSENT. Continuous music flattens tension.

6. Loudness targets and what gets checked before release.
```

## 3. Implementation shape

Note it now so the stories are right later:

```
One audio service in Code/Systems. Gameplay raises events; nothing outside the service
touches an AudioSource.
Sources are pooled. Instantiating one per shot is a hitch generator.
Every sound has a priority and a voice limit - forty simultaneous impacts must degrade,
not clip.
3D sounds set rolloff and max distance explicitly. The Unity defaults are wrong for
almost every game.
Prefer code triggers over animation events: a re-timed animation silently breaking the
feedback is a bug nobody finds until a playtest.
```

## 4. Sourcing and licences

ComfyUI is an image pipeline; it does not make the SFX. Sources are recorded, synthesised
or licensed. **Record the licence for every file** in the bible as it arrives - an
unlicensed sound discovered at release is a shipping blocker, and by then nobody
remembers where it came from.

## 5. Present

```
## Audio direction
"<the one sentence>"

Buses
| Bus | Contains | dB | Ducked by | Slider |

Families
| Family | Character | Variants | Pitch | Priority |

Feedback contract
| Action | Sound at | Latency | Tells the player |

Music: <strategy>   Absent during: <where>
Loudness: <target>
```

## 6. Write

`docs/audio/AUDIO-BIBLE.md`, and the mixer as a `tools-programmer` or `audio-director`
story - a bus map that exists only in a document is a bus map that will be reinvented.

## 7. Close

```
✓ Audio direction set. <n> buses, <n> families, <n> feedback entries.
Actions with no sound yet: <n>  <-- these are the stories

▶ Next: /stories   the feedback contract becomes audio stories
```

---

## Token note

- **One agent call.**
- The feedback contract is the deliverable. A player action with no audio entry is a
  story nobody has written yet, and this table finds them all at once.
