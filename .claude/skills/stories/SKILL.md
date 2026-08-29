---
name: stories
description: Turns an epic into self-sufficient task packets. The single highest-leverage skill in the studio - packet quality decides how much every /dev-task costs.
---

# /stories <epic>

Phase 3. Owner: `game-designer` with `qa-lead`, gated by `unity-architect` in full mode.
Produces `design/backlog/epics/EP-NN-*/story-NNN-<slug>.md`.

A story here is a **task packet**: everything the programmer needs, copied in. Getting
this right turns eight file reads into one, which is where this studio's token savings
actually come from.

---

## 1. Gather the sources (you, cheaply)

| Source | What gets copied into the packet |
|---|---|
| `design/systems/SYS-*.md` | the intent paragraph, the relevant rules, the edge-case table |
| The governing ADR | its **Implementation guidance** section, verbatim |
| `config/balance/<domain>.json` | the relevant keys **with their current values** |
| `docs/architecture/ARCHITECTURE.md` | which assembly this lives in, and the dependency rule |
| `unity-inspect.ps1 -Path <scene>` | the scene or prefab **summary**, never the YAML |
| `design/ux/hud.md` | for UI stories: the element spec and character limits |
| `design/levels/LVL-*.md` | for content stories: the beat this serves |

Copying is correct here. It is the one place the no-duplication rule is deliberately
suspended, because the alternative is eight agents each opening eight files.

## 2. Two parallel calls (one message)

### `game-designer` - the split
```
<the EPIC.md>
<the SYS-* docs it covers>
<the architecture assembly list and the dependency rule>

Task: split this epic into stories.
Rules for the split:
- One story stays inside ONE assembly. Crossing assemblies means two stories.
- One story owns at most ONE scene or prefab. Two is a merge conflict.
- Contract stories come first: config schema before the system reading it, network model
  before replicated gameplay, data before the UI that shows it.
- A story is 1-3 days. Bigger means it is an epic; smaller means it is a task.
- Each story gets a TYPE: Logic | Feel | Content | Integration | Net | Data | Art |
  Audio | UI | Infra. The type decides the required evidence.
For each: title, type, owner agent, what to build in 2-3 sentences, dependencies, and
the scene or prefab it owns.
```

### `qa-lead` - the criteria
```
<the EPIC.md>
<the SYS-* rules and edge cases>

Task: acceptance criteria and test scenarios per story.
- Given/When/Then, observable, measurable. Reject "feels good", "is challenging",
  "is balanced" and rewrite them with numbers.
- Cover the edge cases from the SYS doc, not just the happy path.
- For a Feel story, also write the FEELS LIKE line - one sentence naming the sensation.
  Without it /feel-check has nothing to judge.
- Name the evidence type required by the DoD for each story type.
```

## 3. Assemble the packets (you)

Use `.claude/templates/story.md`. Every section filled, including:

- **Design intent** - the SYS paragraph, copied
- **Architecture decisions to apply** - the ADR guidance block, copied
- **Config keys** - names, current values, ranges
- **Files to touch** - identified paths, not guesses, with the assembly named
- **Scene context** - the `unity-inspect` summary if a scene is involved
- **Out of scope** - the neighbouring story, by number
- **Test scenarios** - from the QA lead
- **Required evidence** - from the type

A packet is finished when a programmer could execute it **without opening any other
file**. That is the test. Apply it literally to each one before writing.

## 4. ARCH-STORY gate (full mode)

`unity-architect`, one call, with the story list and their assemblies and scenes:

```
- Does each story stay in one assembly?
- Does any story own two scenes or prefabs?
- Do contract stories precede consumers?
- Is the governing ADR named where one applies?
Begin with "ARCH-STORY: APPROVED|CONDITIONAL|REJECTED".
```

## 5. Present

```
## <epic> - <n> stories

| # | Title | Type | Owner | Owns | Depends on | Est |

Contract stories first: <list>
Scene ownership: <scene> -> story <n>
Packets complete: <n>/<n>   <any missing sections listed>

Feel stories: <n>  - each has a FEELS LIKE line
```

## 6. Write and update

Story files, `.state/project.json` counters, the epic's story list.

The `PostToolUse` hook checks each packet for missing sections. **If it warns, fix it
now.** An incomplete packet is not a documentation problem, it is a bill that arrives
during `/dev-task` as extra rounds.

## 7. Close

```
✓ <n> stories. Packets complete.

Start with: <the contract stories>
Blocked until those land: <list>

▶ Next: /milestone-plan   or   /dev-task <first story>
```

---

## Token note

- **Two agent calls in parallel**, plus one gate in full mode.
- The copying in step 3 is the entire economic argument of this studio. A packet costs
  perhaps 400 extra tokens to assemble and saves several thousand every time a
  programmer would otherwise have gone looking.
- If `/dev-task` on these stories averages more than three rounds, come back and fix the
  packet template, not the programmer.
