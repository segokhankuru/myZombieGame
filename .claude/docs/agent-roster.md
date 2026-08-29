# Agent Roster

29 roles in 6 tiers. Each row: the role's one-sentence job, its model, and what it may
write. Model assignment is the main lever on token cost - see `token-budget.md` section 3.

---

## Tier 0 - Executive

| Agent | Model | Job | Write access |
|---|---|---|---|
| `studio-head` | opus | Commercial vision, target player, success metrics, scope-vs-budget arbitration, ship or slip | `design/00-brief.md`, `design/vision.md` |
| `technical-director` | opus | Engine strategy, architectural authority, technical risk acceptance, package approval | `docs/architecture/TECH-STRATEGY.md`, ADR approval |

They do not write code, stories or design. They decide and open gates.

---

## Tier 1 - Design

| Agent | Model | Job | Write access |
|---|---|---|---|
| `creative-director` | opus | Pillars, tone, fantasy, the answer to "why is this fun", visual and narrative coherence | `design/PILLARS.md`, `design/vision.md` |
| `game-designer` | opus | Core loop, mechanics, systems intent, the GDD, feature acceptance | `design/GDD.md`, `design/systems/`, `config/content/` |
| `systems-designer` | opus | Economy, progression, difficulty and reward curves, every tunable number and its range | `config/balance/`, `config/schema/`, `design/economy/` |
| `level-designer` | sonnet | Space, pacing, encounter and beat layout, teaching order, blockout specs | `design/levels/`, `config/content/levels.json` |
| `narrative-designer` | sonnet | Story bible, characters, dialogue, barks, localization keys | `design/narrative/` |
| `game-ux-designer` | sonnet | HUD, menus, onboarding, readability, input mapping, accessibility | `design/ux/` |

**Creative director vs game designer:** the creative director owns *what this game
feels like*; the game designer owns *exactly how it behaves*. They hold a round-table in
`/concept` and `/gdd`; disagreements escalate to `studio-head`.

---

## Tier 1 - Production

| Agent | Model | Job | Write access |
|---|---|---|---|
| `producer` | sonnet | Milestones, sprint planning, assignment, dependency and risk management, status | `design/milestones/`, `design/risks.md`, `.state/project.json` |

---

## Tier 2 - Engineering

| Agent | Model | Job | Write access |
|---|---|---|---|
| `unity-architect` | opus | Assembly boundaries, scene and prefab architecture, data flow, ADRs, NFR mechanisms | `docs/architecture/` |
| `gameplay-programmer` | sonnet | Mechanics, controllers, state machines, abilities, interaction, game feel code | `Code/Gameplay/**`, `Tests/**` |
| `systems-programmer` | sonnet | Save/load, scene flow, config loading, input, addressables, audio plumbing, pooling | `Code/Systems/**`, `Tests/**` |
| `graphics-programmer` | sonnet | URP setup, shaders, lighting, post-processing, VFX, rendering cost | `Code/Gameplay/Rendering/**`, `Assets/_Project/Settings/**` |
| `netcode-programmer` | sonnet | Replication, ownership, RPCs, prediction, lag compensation, host migration | `Code/Net/**`, `Tests/**` |
| `ai-programmer` | sonnet | NPC behaviour, navigation, perception, behaviour trees, utility AI, spawning | `Code/AI/**`, `Tests/**` |
| `ui-programmer` | sonnet | HUD and menu implementation, canvas discipline, controller navigation, localization plumbing | `Code/UI/**`, `Tests/**` |
| `tools-programmer` | sonnet | Editor tooling, config importers, scene and prefab authoring scripts, asset postprocessors | `Code/Editor/**` |
| `build-engineer` | sonnet | Build pipeline, platform targets, IL2CPP, addressable builds, CI, Steam depots | `.github/**`, `docs/ops/`, `Code/Editor/Build/**` |

**Critical rule:** engineering agents **consume contracts, they do not author them.**
Config schemas, assembly boundaries and the save format belong to `systems-designer`,
`unity-architect` and `systems-programmer` respectively. They cannot be changed
unilaterally from a gameplay story.

---

## Tier 2 - Art and Audio

| Agent | Model | Job | Write access |
|---|---|---|---|
| `art-director` | opus | Style bible, palette, silhouette language, the locked ComfyUI style, art approval | `docs/art/`, `.claude/comfy/styles/project.json` |
| `technical-artist` | sonnet | Asset budgets, LODs, atlasing, import presets, shader authoring, cleanup and promotion | `Assets/_Project/Art/**`, `docs/art/ASSET-BUDGET.md` |
| `asset-generator` | sonnet | Writes asset requests, drives ComfyUI, produces contact sheets, records provenance | `Assets/_Project/Art/Generated/**`, `docs/art/ASSET-LOG.md` |
| `audio-director` | sonnet | Audio direction, mixer and bus design, SFX and music spec, implementation | `Assets/_Project/Audio/**`, `docs/audio/` |

---

## Tier 3 - Quality

| Agent | Model | Job | Write access |
|---|---|---|---|
| `qa-lead` | opus | Test strategy, acceptance-criteria quality, the DoD gate, risk-based coverage, playtest plan | `docs/qa/strategy.md`, `docs/qa/test-plan.md` |
| `test-engineer` | sonnet | EditMode and PlayMode tests, automation, regression suite, bug reports | `Tests/**`, `docs/qa/bugs/` |
| `code-reviewer` | sonnet | Independent C# and Unity review: correctness, allocation, scope fidelity, rule compliance | writes nothing, returns findings |
| `performance-engineer` | sonnet | Frame budget, profiler analysis, GC allocation, draw calls, load time, memory | `docs/qa/performance/` |
| `playtest-analyst` | sonnet | Playtest protocol, observation, telemetry reading, diagnosing why something is not fun | `docs/qa/playtests/` |

---

## Tier 3 - Support

| Agent | Model | Job | Write access |
|---|---|---|---|
| `tech-writer` | haiku | Guides, changelog, patch notes, README, in-game help text | `docs/guides/`, `CHANGELOG.md` |
| `publishing-manager` | sonnet | Steam page, capsules and tags, wishlist funnel, beat plan, demo and playtest strategy | `docs/publishing/` |

---

## Authority matrix

| Decision | Owner | Consulted | Informed |
|---|---|---|---|
| Is this game worth making | `studio-head` | `creative-director`, `publishing-manager` | everyone |
| Design pillars and tone | `creative-director` | `game-designer`, `art-director` | everyone |
| Core loop and mechanics | `game-designer` | `creative-director`, `level-designer` | engineering |
| Any tunable number | `systems-designer` | `game-designer`, `playtest-analyst` | engineering |
| Feature scope for a milestone | `studio-head` | `producer`, `game-designer` | everyone |
| Engine, package, third-party asset | `technical-director` | `unity-architect`, `build-engineer` | engineering |
| Assembly and scene architecture | `unity-architect` | relevant programmer | QA |
| Save format | `systems-programmer` | `unity-architect`, `game-designer` | QA |
| Network model | `netcode-programmer` | `unity-architect`, `technical-director` | gameplay |
| Visual style and the ComfyUI lock | `art-director` | `creative-director`, `technical-artist` | everyone |
| Asset budget | `technical-artist` | `performance-engineer` | art |
| Is it fun | `creative-director` | `playtest-analyst`, `game-designer` | everyone |
| Is it done | `qa-lead` | `test-engineer`, `game-designer` | `producer` |
| Ship or slip | `studio-head` | `qa-lead`, `build-engineer`, `publishing-manager` | everyone |

---

## Enabling and disabling roles

Every active agent costs tokens. `/kickoff` picks the roster from project scale.

| Scale | Active roles |
|---|---|
| **Prototype** (7) | `game-designer`, `unity-architect`, `gameplay-programmer`, `systems-programmer`, `technical-artist`, `test-engineer`, `producer` |
| **Indie** (17) | + `creative-director`, `systems-designer`, `level-designer`, `game-ux-designer`, `ui-programmer`, `art-director`, `asset-generator`, `audio-director`, `qa-lead`, `code-reviewer` |
| **Commercial** (29) | Full roster |

Multiplayer adds `netcode-programmer` at any scale. A game with NPCs adds
`ai-programmer`. A story-led game adds `narrative-designer`. A Steam release adds
`build-engineer` and `publishing-manager`.

The active roster lives in `.state/project.json` under `activeRoles`.
