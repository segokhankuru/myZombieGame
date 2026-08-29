# SYS-NN: <name>

> **Pillar:** PILLAR-<n> | **Status:** <designed|building|done>
> **Owner agent:** <role> | **Assembly:** <Game.X>

## Intent

<One paragraph: what experience this system exists to produce. Not what it does — what
it is FOR. This paragraph is copied into every story packet, and it is what lets a
programmer make a good decision in a case the criteria do not cover.>

## Rules

- **R-1:** <stated so it can be implemented and tested>
- **R-2:** ...

## States

| State | Entered when | Exited when | Player can | Player cannot |
|---|---|---|---|---|

## Tunables

*Shape and range only. Values belong to systems-designer, in config/.*

| Key | Controls | Shape | Suggested range | What breaks outside it |
|---|---|---|---|---|

## Interactions

| With | What happens | Who wins |
|---|---|---|

## Errors and edge cases

*The section a programmer actually reads. Do not shorten it.*

| Case | Behaviour | What the player sees |
|---|---|---|
| At zero | | |
| At maximum | | |
| Interrupted mid-action | | |
| Triggered twice in one frame | | |
| Triggered during a scene load | | |
| While another system owns the object | | |

## What the player must understand

<The one thing that must be legible without a tutorial, and how the system teaches it.>

## Feedback

| Event | Visual | Audio | Haptic | Within |
|---|---|---|---|---|

## Out of scope

<What this system explicitly does not do, and which system does.>

## Open questions

<Anything genuinely undecided. An open question here is honest; an invented answer is
a defect that reaches production.>
