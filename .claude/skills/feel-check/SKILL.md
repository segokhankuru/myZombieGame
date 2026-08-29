---
name: feel-check
description: Judges whether a built feature feels like what the pillars promised. Runs the FEEL-CHECK gate - the step that separates a game that works from a game that is good.
---

# /feel-check <feature>

Phase 4. Owner: `creative-director`.

Correct and satisfying are unrelated properties. Every other gate in this studio checks
the first one. This is the only one that checks the second.

---

## 1. Establish the promise

Find the `FEELS LIKE:` line - in the story packet, or in `design/PILLARS.md` for a whole
system. If there is no promise written down, there is nothing to judge:

```
This feature has no FEELS LIKE line, so "does it feel right" has no answer.
Write one first - one sentence naming the sensation - then re-run.
```

Do not invent the promise retroactively to match what was built. That is how a game
talks itself into accepting whatever it happens to have.

## 2. Establish what it actually does

The gate needs observation, not code. In order of preference:

1. The user plays it and describes what happens
2. A capture or recording
3. The programmer's honest `FEEL:` line from `/dev-task`
4. The parameters as built - timings, curves, durations, thresholds

Reading the code is last and weakest. Say which source you used; a verdict from
parameters alone is a weaker verdict and should be labelled as one.

## 3. One call - `creative-director`

```
Feature: <name>
FEELS LIKE: "<the promise>"
Pillars it serves: <PILLAR-n: text>

Acceptance criteria (all passing): <the list>

What it actually does now:
<the observation, or the parameters: input delay, acceleration, animation lengths,
 camera behaviour, audio timing, what the player sees and when>

Task: the FEEL-CHECK gate. Judge ONLY the gap between the promise and the sensation.
Diagnose in this order - it is the order of impact:
1. RESPONSE   - does the game answer the input immediately? Anything above ~100 ms of
                unacknowledged input reads as broken, not slow.
2. READABILITY- can the player tell what happened and why, without being told?
3. WEIGHT     - do acceleration, recovery and stopping match the fiction of the object?
4. CONSEQUENCE- does the moment change anything the player cares about?
5. JUICE      - camera, audio, particles, shake. LAST. Juice on a dead mechanic is
                lipstick, and adding it first hides the real problem.

At most 5 fixes. Each names its layer and is specific: name the feedback, its trigger
and its duration. "Add more feedback" is not a fix.

Begin with "FEEL-CHECK: APPROVED|CONDITIONAL|REJECTED".
```

## 4. Present

```
## Feel check: <feature>

Promised: "<the line>"
Actual:   <one sentence, honest>

| Layer | Verdict | Note |
| Response | ok | 45 ms to first movement |
| Readability | weak | the state change is only audio |
| Weight | ok | |
| Consequence | missing | nothing changes if the player does this well |
| Juice | n/a | do not touch until Consequence is fixed |

Fixes
1. [Readability] <specific change, with duration and trigger>

FEEL-CHECK: <verdict>
```

## 5. Route the fixes - this is the part that matters

| Layer that failed | Goes to | Because |
|---|---|---|
| Response | `gameplay-programmer` | it is a code timing problem |
| Readability | `game-ux-designer` or `art-director` | it is a communication problem |
| Weight | `systems-designer` (curves) or `gameplay-programmer` | usually a tuning problem |
| **Consequence** | **`game-designer`** | the mechanic does not matter yet - this is a design problem, not a bug |
| Juice | `graphics-programmer`, `audio-director` | last, and only after the others |

A `Consequence` failure sent to a programmer will come back as more particles. Route it
correctly or the same verdict repeats next milestone.

## 6. Record

Append the verdict to `.state/gates.jsonl`. If `CONDITIONAL` or `REJECTED`, file the
fixes as stories with the layer named in each title, so the routing survives.

## 7. Close

```
✓ FEEL-CHECK <verdict>

Weakest layer: <layer> -> <role>
<if Consequence failed: "This is a design problem. Do not add juice to it.">

▶ Next: /dev-task <fix story>   or   /playtest   if it is worth putting in front of someone
```

---

## Token note

- **One agent call.**
- Run it on Feel stories and on every vertical slice. Skipping it does not save tokens;
  it defers the same discovery to a playtest, where it costs a milestone instead of a call.
