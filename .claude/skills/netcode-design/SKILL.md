---
name: netcode-design
description: Decides who is authoritative over what, how state replicates, and what happens in the four cases that break multiplayer games. Runs the NET-MODEL gate.
---

# /netcode-design

Phase 2. Owner: `netcode-programmer` with `unity-architect`.
Produces `docs/architecture/adr/ADR-NNNN-network-model.md` and the network section of
`ARCHITECTURE.md`.

Run this **before** any replicated gameplay is built. Retrofitting authority onto a
single-player codebase is close to a rewrite.

---

## 1. Establish the shape

From the brief and the GDD:
- How many players, and are they cooperating or competing? Competition raises the cheat
  stakes enormously.
- Listen server, dedicated, or relay? On Steam with a Facepunch transport, listen server
  is the default and host migration becomes a real question.
- What is the worst thing one player can do to another player's experience? That question
  drives more of the design than the player count does.

## 2. One call - `netcode-programmer`

```
<the core loop and the systems that involve more than one player>
<player count, topology, transport, platform>
<the physics and interaction model - what the player can grab, push, break>

Task: the network model.

1. Authority table: for every piece of state, who owns it, whether the client predicts
   it, how a correction is applied, and why. "The server owns it" is not a model until
   you say what the client does in the meantime.

2. Replication budget: per object, rate and bytes per tick, then the total at maximum
   players in the busiest scene. Replicating a transform every tick for 60 objects is a
   design decision, not an accident - price it.

3. The four hard cases, specified rather than handled:
   - a joiner arriving AFTER the state exists
   - the host disconnecting
   - a client disconnecting mid-action, holding something
   - two clients acting on the same object in the same tick

4. Correction visibility: what does the player SEE during the worst-case correction?
   A snapping character is worse than 100 ms of latency. If a correction is visible, say
   how it is smoothed.

5. Cheat surface: for each RPC and client-owned value, what a modified client could lie
   about and what that costs us. In a co-op game this may be acceptable - say so
   explicitly rather than by omission.

Begin with "NET-MODEL: APPROVED|CONDITIONAL|REJECTED".
```

## 3. Architecture review

`unity-architect`, one call, in parallel:

```
<the network model above>
<the assembly list and the dependency rule>

Task: does this fit the architecture?
1. Does Net stay a leaf, or does it require Gameplay to know about replication? The
   second is how a codebase becomes untestable offline.
2. Can the game still run single-player, in a test, without a network stack?
3. What does this mean for the save format - is authoritative state the same as saved
   state?
At most 15 lines.
```

## 4. Present

```
## Network model
Topology <...> | Transport <...> | Max players <n> | Tick <n>

Authority
| State | Owner | Predicted | Correction |

Budget: <n> bytes/tick/player, <n> KB/s at <n> players

The four hard cases
| Case | Behaviour |

Cheat surface
| What | Who can lie | Cost | Accepted? |

Offline: <can the game run and be tested without a network>
```

## 5. Write

The ADR, the `ARCHITECTURE.md` network section, the gate into `.state/gates.jsonl`.

The ADR's **Implementation guidance** section is what gets copied into every replicated
story, so write it for a programmer who will never open this document: the required
pattern, the forbidden pattern, and the ownership rule in one block.

## 6. Testing requirement

Record it now, so it is in every Net story's evidence section from the start:

```
A Net story is not done on one machine. Required:
  host + joiner, both taking the action
  a joiner arriving after the state exists
  a disconnect during the action
  artificial latency 150 ms and 5% loss applied
If it only works on localhost, it does not work.
```

## 7. Close

```
✓ Network model set. <n> replicated states, <n> KB/s at <n> players.
Riskiest: <the case most likely to break>
Cheat surface accepted: <what, and why that is fine here>

▶ Next: /adr if a second decision fell out of this, or /epics
```

---

## Token note

- **Two agent calls, parallel.**
- Do this once, early. Every story built against an unsettled network model is a story
  that gets rebuilt.
