# Quick Start

## Install into a Unity project

From the studio repo, pointing at your Unity project folder:

```powershell
.\install.ps1 -Target "C:\path\to\YourUnityProject"
```

This copies `.claude/`, creates `design/`, `docs/`, `config/` and `.state/`, and merges
`.gitignore` / `.gitattributes`. It never touches `Assets/`, `Packages/` or
`ProjectSettings/`.

Then open Claude Code in the Unity project and run `/start`.

---

## Path A - a brand new game

```
/kickoff "co-op flat-pack furniture delivery sim on Steam"
/concept          pillars, fantasy, what makes it different
/core-loop        the 30-second loop, and what mastery looks like
/gdd              the design document
/economy          the numbers, with ranges and reasons
/architecture     assemblies, scene flow, performance budget
/art-direction    the style bible and the locked generation style
/epics            break the first milestone into epics
/stories EP-01    task packets
/milestone-plan   who does what, in what order, who owns which scene
/dev-task         implement, story by story
/gen-asset        generate the art as you go
/playtest         does it actually work
/build            a real build
```

Realistically: `/kickoff` through `/architecture` is one session. Do not try to do the
whole list in one context window - see `token-budget.md` §9.

---

## Path B - a Unity project that already exists

```
/onboard          reads the project through tools, not by reading YAML, and writes
                  CONTEXT.md, a draft ARCHITECTURE.md and a debt list
/status           where things stand
```

`/onboard` will tell you what is missing. Usually it is the design layer: there is code
but no written loop, and no config layer, so every number is buried in prefabs.
The recommended repair order:

```
/core-loop        write down the loop the code already implements
/data-schema      pull the buried numbers out into config/
/architecture     document the boundaries that exist, decide the ones that should
```

---

## Path C - you just want assets

```
/comfy            is ComfyUI up, what models does it have
/art-direction    lock a style (do this once, it pays for itself immediately)
/gen-asset "wooden crate, three quarter view"
```

Generating without locking a style first produces 40 images that do not belong to the
same game. The style lock is the whole point.

---

## The four commands you will actually use daily

| Command | When |
|---|---|
| `/status` | Start of every session |
| `/dev-task` | The main production loop |
| `/tune <domain>` | Every time something feels off by a number |
| `/playtest` | End of every milestone, and any time you are unsure |

---

## Keeping it cheap

- One phase per session. `/status` at the start, then work.
- If a story needs more than three rounds with the developer agent, the story was
  wrong, not the code. Fix `/stories`, not the programmer.
- Never let an agent read a `.unity` or `.prefab` file. If you see it happening, stop
  it - that single read can cost more than the rest of the session.
- `solo` mode exists. A weekend prototype does not need sixteen gates.
