---
name: onboard
description: Brings an existing Unity project into the studio. Reads the project through tools rather than files, writes CONTEXT.md and a draft architecture, and produces an honest debt list.
---

# /onboard

Owner: `unity-architect`. The point of this skill is to understand a project **without
reading it**, because reading a Unity project is how you spend a context window and
learn nothing.

---

## 1. Scan with tools only (free, no agents)

Run these and keep the output. Do not open a scene, prefab, meta file or the Editor log.

```powershell
.claude\tools\asset-index.ps1                     # what exists, how heavy
.claude\tools\asset-index.ps1 -Scripts            # code surface, assemblies
.claude\tools\asset-index.ps1 -Heavy              # what is over budget
.claude\tools\unity-log.ps1 -Errors               # does it currently compile
.claude\comfy\comfy.ps1 status                    # is generation available
```

Plus, cheaply:
- `ProjectSettings/ProjectVersion.txt` - the editor version
- `Packages/manifest.json` - the dependency surface
- `git log --oneline -30` - what has been happening
- The largest 3 scenes, each through `unity-inspect.ps1 -Depth 2`

## 2. Ask the four things a scan cannot tell you

One `AskUserQuestion` call:

1. **What is the game?** One sentence, in the words you would use to a friend.
2. **What state is it in?** `Prototype` / `Playable but not fun yet` /
   `Fun, needs content` / `Content complete, needs polish`
3. **What is the biggest problem right now?** Free text. This is the most useful answer
   in the whole skill.
4. **What must not be touched?** Working systems, third-party packs, anything fragile.

## 3. Architectural read - `unity-architect`, one call

Embed the tool output; do not send paths.

```
<asset-index summary>
<asset-index -Scripts summary, including the assembly list>
<the three scene summaries>
<manifest.json dependency list>
<the user's four answers>

Task: characterise this project.
1. What architecture is actually here? Name the pattern, including "none, everything is
   in Assembly-CSharp and finds each other at runtime" if that is the truth.
2. The 5 largest structural risks, ordered by what they will cost in the next 3 months.
3. Where does tuning live today? Serialized fields, prefabs, hardcoded constants, a
   config layer? This decides how much /data-schema work is ahead.
4. What is the cheapest first structural improvement with the highest payoff?
5. What is deliberately fine as it is? An honest "leave this alone" list is as useful
   as the risk list.

Do NOT propose a rewrite. Working code that offends an architecture doc is still working
code, and this project has to keep running while it improves.
At most 40 lines.
```

## 4. Write

### `docs/CONTEXT.md`
The template from `context-protocol.md`. Fill what the scan and the answers give you.
Mark everything else `<tbd>` - a `<tbd>` is information; a guess is a liability.

### `docs/architecture/ARCHITECTURE.md` - draft
Describe **what is there**, clearly labelled as observed rather than designed. Add a
`## Intended` section only if the user wants to change something now.

### `docs/DEBT.md`
```markdown
# Debt
| # | What | Cost of leaving it | Cost of fixing it | Trigger to fix |
```
The `Trigger` column is what makes this list useful: "when we add the second level",
"before multiplayer", "never, this is fine". Debt without a trigger is a guilt list.

### `.state/project.json`
Phase `production` unless the user said prototype. Roster from the scale.

## 5. Report and route

```
<Game> - onboarded

What is here
  Unity <version>, <pipeline>, <n> packages
  <n> C# files in <n> assemblies   <"all in Assembly-CSharp" if so>
  <n> scenes, largest <name> <n> objects
  Assets <n> MB, <n> over budget
  Compiles: <yes/no>

What the architect found
  <the 5 risks, one line each>

Tuning lives in: <serialized fields | prefabs | constants | a config layer>
  <if not config: "every number is buried. /data-schema is the highest-payoff next step.">

Leave alone: <list>

▶ Next: <one command>
```

## 6. The usual repair order

Most existing Unity projects are missing the design layer, not the code:

```
/core-loop      write down the loop the code already implements
                (this is often the first time it has been written down at all)
/data-schema    lift the buried numbers into config/ - this is what makes /tune possible
/architecture   document the boundaries that exist, decide the ones that should
/asset-pipeline stop the asset budget getting worse
```

Suggest this order, and let the user pick. Do not start it here.

---

## Token note

- **One agent call.** Everything else is tool output and four questions.
- Never open a scene, prefab, meta file or the Editor log. The tools already summarized
  them, and the summary is what the architect actually needs.
- A large project onboards for roughly the cost of one story. That is the whole design.
