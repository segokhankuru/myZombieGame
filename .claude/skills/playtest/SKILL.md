---
name: playtest
description: Designs and reads a structured playtest. Runs the PT-FUN gate - the only evidence in this studio that a test suite cannot produce.
---

# /playtest [focus]

Phase 4. Owner: `playtest-analyst`. Produces `docs/qa/playtests/PT-NN-<slug>.md`.

---

## 1. Name one question

A playtest with three questions answers none. Pick one:

```
This test answers: "<one question>"
```

Good: "does a new player understand the delivery loop without help", "is minute 20 still
interesting", "do two players naturally split roles".
Not good: "is the game fun" - too broad to observe.

## 2. Prepare the protocol - `playtest-analyst`

```
Question: <the one question>
Build: <version, what is in it, what is placeholder>
Players: <how many, what they already know about the game>
<PILLARS.md>
<the core loop>
<the previous playtest's open findings, if any>

Task: the protocol.
1. What the player is told before starting. Usually only: "here is a game, play it,
   think aloud". Anything more contaminates the result.
2. The observation sheet: what to write down, and at what moments.
3. The five checkpoints with times: first input, understands the core verb, completes
   the loop unaided, CHOOSES to repeat it, can articulate what they were trying to do.
4. The three questions to ask afterwards. Ask about the last thing that happened, never
   about the game as a whole.
5. What would falsify our assumption? Name the observation that would mean we are wrong.
   A test that cannot fail is a demo.
```

## 3. Run it - the user, not an agent

Present the protocol and the observation sheet. Then hand it over:

```
Run this with <n> people. Rules:
  - Do not explain the game. Not once. The moment you help, the test is over.
  - Write down the minute they stop leaning in. That minute is the finding.
  - Watch the hands, not the mouth. What they DO is data; why they say they did it is
    a hypothesis, usually wrong and always polite.
  - Three players find most of it. The fourth mostly confirms.

Bring back the observation sheets and I will read them.
```

If a build does not exist yet, stop here and run `/build`. There is no such thing as a
playtest of a description.

## 4. Analyse - `playtest-analyst`, one call

```
<the observation sheets, verbatim>
<the five checkpoints with actual times>
<PILLARS.md>
<telemetry summary if there is one>

Task: read this test.
1. Findings: observation (what happened) -> interpretation (why) -> confidence.
   Keep the two columns separate. Blurring them is how a studio acts on a guess.
2. The moment it landed, with a timestamp - or the fact that it did not.
3. The moment attention dropped, with a timestamp and what was on screen.
4. Pillar evidence: for each pillar, what supported it and what contradicted it.
5. For each finding: is this a DESIGN problem or a BUG? Most playtest failures are
   design problems wearing a bug costume.
6. Recommended changes, each traced to a finding, with a rough cost.

Begin with "PT-FUN: APPROVED|CONDITIONAL|REJECTED".
Checkpoint 4 is decisive: a player who completes the loop and does not want to repeat it
has told us the game is finished and not fun.
```

## 5. Present

```
## PT-NN: <question>
Players <n> | Build <version> | <date>

Checkpoints
| # | Checkpoint | P1 | P2 | P3 |
| 1 | first input < 15 s | 8s | 22s | 11s |
| 4 | chose to repeat | yes | no | yes |

Landed at: <time> - <what was happening>
Lost them at: <time> - <what was on screen>

Findings
| # | Observed | Interpretation | Confidence | Design or bug |

Against the pillars
| Pillar | For | Against |

PT-FUN: <verdict>
```

## 6. Route

- **Design** findings -> `game-designer` or `creative-director`. Never to the programmer
  whose code worked exactly as specified.
- **Bug** findings -> `/bug`, triaged normally.
- **Tuning** findings -> `/tune`, quoting the observation as the reason. This is the best
  possible input to a tuning change.

## 7. Write

`docs/qa/playtests/PT-NN-<slug>.md`, the gate into `.state/gates.jsonl`, the top two
findings into `docs/CONTEXT.md` under current work.

## 8. Close

```
✓ PT-NN: <verdict>

The one thing to fix: <finding #1>
  It is a <design | bug | tuning> problem -> <role>

Open from the previous test still unaddressed: <n>

▶ Next: <the routed command>
```

---

## Token note

- **Two agent calls** - protocol, then analysis. The test itself is human time.
- This gate cannot be automated or waived for a shipping milestone. That is not a process
  rule, it is the definition: a game nobody has watched someone else play has not been
  tested.
