# Audio Rules

**Scope:** `Assets/_Project/Audio/**`, audio code in `Code/Systems/Audio/**`

## Architecture
- One audio service in `Systems`. Gameplay raises events; **nothing outside the service
  touches an `AudioSource`.**
- Sources are pooled. Instantiating one per shot is a hitch generator.
- Every sound has a priority and a voice limit. Forty simultaneous impacts must degrade
  gracefully, not clip.

## Timing is the whole job
- Attack timing beats sample quality. A mediocre sound on the exact frame of impact
  feels better than a beautiful one 80 ms late.
- Feedback sounds fire from **game state**, not from animation events. A re-timed
  animation silently breaking the feedback is a bug nobody finds until a playtest.
- The first job of every SFX is to say something happened and whether it worked.
  Atmosphere is the second job.

## Variation
- Anything triggered more than a few times a minute needs **at least four variants**
  plus pitch randomisation. Otherwise players mute the game.
- A no-repeat-within-N rule on every pool. A single bark on a common event is a bug that
  ships.

## Buses
- Route through the bus map in `docs/audio/AUDIO-BIBLE.md`. No source plays unrouted.
- Every bus a player might want quieter has a slider. That is accessibility, not a
  preference.
- Ducking is declared in the bus map, not implemented ad hoc per sound.

## 3D
- Rolloff curve and max distance set explicitly. The Unity defaults are wrong for almost
  every game.
- Spatial blend deliberate per sound. UI is 2D. World feedback is 3D. A UI sound at 3D
  is a bug the player experiences as "the menu is quiet on one side".

## Assets
- Music streams. Short frequent SFX decompress on load. Everything else is compressed
  in memory.
- Mono for 3D sources - a stereo file positioned in 3D wastes memory and spatialises
  badly.
- Naming: `sfx_<subject>_<variant>`, `mus_<track>`, `amb_<place>`.

## Licences
Every audio file's licence is recorded in `docs/audio/AUDIO-BIBLE.md` **as it arrives**.
An unlicensed sound discovered at release is a shipping blocker, and by then nobody
remembers where it came from.

## Mix
- Mix at the target volume, then check it quiet, on laptop speakers, and with music off.
- Loudness target and peak ceiling checked before release.

## Never
- Leave a player-facing action with no audio feedback and call the story done
- Ship a repeated sound with one variant
- Change a mechanic's timing to fit a sound - escalate to `game-designer`
