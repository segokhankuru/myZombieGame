# Netcode Rules

**Scope:** `Assets/_Project/Code/Net/**` (assembly `Game.Net`)
Read with `csharp-code.md`.

## Authority
- Every replicated piece of state has a **named owner**, written in the network ADR.
  If it is not in the table, it does not replicate yet.
- The server is authoritative over anything with a persistent consequence: currency,
  progression, unlocks, completion.
- The client may be authoritative over its own cosmetic and predicted state only.

## Trust
- **Every RPC is a trust boundary.** Validate on the receiving side: is this sender
  allowed to do this, to this object, right now, at this rate?
- Never trust a client-supplied position, amount, id or timestamp for anything that
  matters.
- Rate-limit anything a client can call in a loop.

## Prediction
- The client predicts, the server corrects, the player does not see the correction.
- A visible correction must be smoothed. A snapping character reads worse than 100 ms
  of latency.
- Reconciliation replays input from the last acknowledged state. Never teleport as a
  correction strategy.

## Bandwidth
- Know the bytes per tick per object and the total at maximum players in the busiest
  scene. Write it in the story.
- Replicate at the lowest rate that still feels right, not at tick rate by default.
- Send deltas and quantized values. A full float transform every tick for 60 objects is
  a decision, not an accident.
- Culling by relevance and distance is a feature, not an optimization.

## The four cases - every Net story handles them
- **Late joiner**: arrives after the state exists and must see it correctly
- **Host disconnect**: what happens to the session
- **Client disconnect mid-action**: while holding, carrying or interacting
- **Same-tick conflict**: two clients act on one object

## Testing - a Net story is not done on one machine
- Host and joiner, both taking the action
- A joiner arriving **after** the state exists
- A disconnect during the action
- Artificial latency 150 ms, 5% loss applied

If it only works on localhost, it does not work.

## Forbidden
- Gameplay code referencing `Game.Net` types. Gameplay must run single-player.
- Replicating something that could be derived locally from replicated inputs.
- A client-authoritative RPC that grants anything persistent.
- Declaring done from a single-instance test.
