# Story <NNN>: <title>

> **Epic:** <EP-NN> | **Type:** <Logic|Feel|Content|Integration|Net|Data|Art|Audio|UI|Infra>
> **Owner:** <agent> | **Assembly:** <Game.Gameplay> | **Status:** Ready
> **Estimate:** <XS/S/M/L> | **Milestone:** — | **Updated:** <YYYY-MM-DD>
> **Owns scene/prefab:** <path, or none>

## What to build

<2-3 sentences. The first thing the programmer reads. Concrete and technical.>

## Design intent

*Source: SYS-NN — copied, not referenced. The programmer will not open the GDD.*

<The intent paragraph from the system doc. This is what the code is FOR, and it is what
lets a programmer make a good decision when the acceptance criteria do not cover a case.>

<For a Feel story, add:>
**FEELS LIKE:** <one sentence naming the sensation this must produce>

## Acceptance criteria

*Source: SYS-NN rules — copied*

- [ ] **AC-1:** <criterion>
  - Given: <precondition>
  - When: <action>
  - Then: <observable, measurable result>
- [ ] **AC-2:** ...

## Business rules

- **R-1:** <rule, from the SYS doc>

## Errors and edge cases

*From the SYS doc's edge-case table. This is the section that prevents rework.*

| Case | Expected behaviour | What the player sees |
|---|---|---|
| | | |

## Architecture decisions to apply

*ADR-NNNN: <title> — the Implementation guidance section, copied verbatim.
The programmer will not open the ADR.*

<the block>

**Required pattern:** <...>
**Forbidden pattern:** <...>
**Dependency rule:** <e.g. Gameplay may depend on Systems, never on UI or Net>

## Config keys

*Source: config/balance/<domain>.json — copied with current values.
If a number you need is not here, STOP and escalate to systems-designer.*

| Key | Current | Range | Controls |
|---|---|---|---|
| | | | |

<or: "None — this story introduces no tunable values.">

## Contract

*Interfaces, events or data shapes this story consumes or produces — copied*

```csharp
// the relevant signatures
```

## Scene context

*From `unity-inspect.ps1` — a summary, never the YAML.
If the scene does not exist yet, say what will create it.*

```
<the 10-20 line hierarchy summary>
```

## Files to touch

*Identified paths, not guesses. Each with its assembly.*

- `Assets/_Project/Code/<...>.cs` — <what changes> [Game.<Assembly>]
- `Assets/_Project/Code/Tests/EditMode/<...>.cs` — new

## Out of scope

*Neighbouring stories handle these. Doing them here is a review finding.*

- Story <NNN+1>: <what>

## Test scenarios

*Written by qa-lead. Do not invent tests from scratch.*

**TC-01** — covers AC-1
- Given: <...> | When: <...> | Then: <...>
- Edge cases: <...>
- Platform: <EditMode | PlayMode | two-client>

## Required evidence

**Type:** <type>
**Required by the DoD:** <the type's evidence>
**Test file:** `Assets/_Project/Code/Tests/<...>`
**Status:** [ ] not yet created

<For player-facing types, the felt-experience note is also required:>
**Felt-experience note:** [ ] not yet written — what it actually feels like in the game

## Dependencies

**Must finish first:** <story-NNN, or None>
**Waiting on this:** <story-NNN, or None>

## Traceability

SYS-NN → PILLAR-<n> | ADR-NNNN | Level: <LVL-NN, if any>

---
<!--
Packet test: could a programmer execute this WITHOUT opening any other file?
If not, the missing content belongs above, not in a link.
-->
