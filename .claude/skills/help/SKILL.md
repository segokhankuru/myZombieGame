---
name: help
description: Shows the Unity Studio command list grouped by phase. With an argument, explains one command.
---

# /help

## Without arguments

If `.state/project.json` exists, read `phase` and **put that phase's commands first**,
abbreviating the rest. Otherwise show everything.

```
# Claude Code Unity Studio

## Bootstrap
/start                    State detection + the one next step
/kickoff "<idea>"         Start a new game (studio-head)
/onboard                  Bring an existing Unity project into the studio
/status [--deep]          Dashboard, blockers, open gates, token note
/comfy [caps|jobs]        ComfyUI health and model discovery

## Phase 1 - Concept and Design
/concept                  Pillars, fantasy, references, the differentiator
/gdd                      The design document
/core-loop                The loop, and the 30s / 10min / 10h experience
/systems-design <name>    One system, fully specified
/economy                  Curves, ranges, and the config schema behind them
/level-design <name>      Space, beats, teaching order, blockout spec
/narrative                Story bible, characters, dialogue with keys
/game-ux                  HUD, menus, onboarding, input map, accessibility
/scope-check              What got added, and what pays for it

## Phase 2 - Technical and Art Direction
/architecture             Assemblies, scene flow, frame budget, first ADRs
/adr "<question>"         Architecture decision record
/data-schema <domain>     The tuning layer: schema, importer, ScriptableObject
/netcode-design           Authority, replication, the four hard cases
/art-direction            Art bible + the locked ComfyUI style
/asset-pipeline           Budgets, naming, import presets, postprocessor
/audio-direction          Bus map, SFX families, feedback contract

## Phase 3 - Production
/epics                    Break a milestone into epics
/stories <epic>           Task packets
/milestone-plan           Assignment, dependencies, scene ownership
/dev-task [story]         Implement one story
/vertical-slice <epic>    One feature end to end, all disciplines
/gen-asset "<subject>"    Generate art through ComfyUI
/tune <domain>            Change a number, and say what it should feel like
/handoff                  Handoff packet between agents

## Phase 4 - Quality
/code-review [scope]      Independent review
/qa-run [scope]           EditMode + PlayMode run
/playtest [focus]         The only evidence a test suite cannot produce
/feel-check <feature>     Does it feel like the pillars promised
/perf-check [scene]       Frame budget against measurement
/balance-check <domain>   Simulate the curves over a session
/bug "<description>"      Bug report + triage
/dod-check [story]        The done gate

## Phase 5 - Release
/build [target]           A real build, with a size and warning report
/steam-prep               Store page, capsules, tags, achievements, depots
/release <version>        Release plan, rollback, go/no-go
/patch-notes              Player-facing notes + CHANGELOG
/hotfix "<issue>"         Fast path for a live incident
/retro                    What to change next milestone

## Utility
/roundtable "<topic>"     Multi-role discussion on a hard decision
/context-compact          Compact docs, cut token cost
```

## With an argument - `/help <command>`

Read `.claude/skills/<command>/SKILL.md` and produce:

```
/<command>
Does: <1-2 sentences>
Owner: <agent>
Needs: <what must exist first>
Produces: <which files>
Before: <the command that usually precedes it>
After: <the sensible next one>
Costs: <roughly how many agent calls>
```

## Further reading

| Question | File |
|---|---|
| Who does what, and who decides | `.claude/docs/agent-roster.md` |
| How roles talk and escalate | `.claude/docs/coordination-rules.md` |
| How to keep this cheap | `.claude/docs/token-budget.md` |
| Unity rules that are not negotiable | `.claude/docs/unity-conventions.md` |
| How tuning works | `.claude/docs/config-protocol.md` |
| How asset generation works | `.claude/docs/comfy-protocol.md` |
| What "done" means | `.claude/docs/definition-of-done.md` |
| The gates | `.claude/docs/gates.md` |
