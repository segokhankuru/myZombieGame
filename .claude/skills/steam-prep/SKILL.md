---
name: steam-prep
description: Prepares the Steam page, capsules, tags, achievements and depot layout. Prepares only - the user approves and uploads.
---

# /steam-prep

Phase 5, and ideally much earlier. Owner: `publishing-manager` with `build-engineer`.
Produces `docs/publishing/`.

The page should exist as soon as there is one honest GIF. Wishlists compound; a launch
does not.

---

## 1. Inputs

`design/00-brief.md` (the target player), `design/PILLARS.md`, the core loop, and a
build to capture from. Without a build there are no screenshots, and a Steam page
without screenshots is not a page.

## 2. Two parallel calls (one message)

### `publishing-manager` - the page
```
<the brief: pitch, target player, what they play instead>
<PILLARS.md>
<the core loop and the verb list>
<what is actually in the build today, and what is placeholder>

Task: the store page.
1. Short description, at most 300 characters: the verb, the twist, the hook. Readable in
   one breath. Describe what the player DOES, not the world they do it in.
2. The About section: the pitch, then 3-5 feature blocks, each naming the GIF it needs.
3. Tags in priority order, with which list each puts us in. Tags are the algorithm, not
   metadata - this is a positioning decision.
4. Capsule briefs: header 460x215, small 231x87, library 600x900. For each, the ONE
   element that must be legible at that size. In a Steam list a player sees a small image
   and four words.
5. Screenshots in order. The first must show the core verb - not a menu, not a landscape.
6. The first GIF: three seconds, the verb, a consequence, a reaction.
7. Refund risk: what in the first two hours would make someone ask for their money back?
   Answer it honestly - a page that implies more content than exists is how refunds and
   negative reviews are manufactured.
```

### `build-engineer` - the technical side
```
<the build report>
<docs/ops/release.md if it exists>

Task: the Steam technical setup.
1. Depot layout: which depots, what goes in each, which platforms.
2. Branches: default, beta, playtest - who can see each.
3. Achievements: id, display name, description, hidden or not, and WHERE in the game each
   is granted. An achievement with no grant site is a bug waiting for launch day.
4. Cloud save paths, and what must NOT be synced.
5. The upload sequence, with the approval point marked explicitly.
6. Rollback: the exact steps, and confirmation that they have been executed at least once.
```

## 3. Capsule art

Capsules go through `/gen-asset`, with one rule: **generate the background plate, set the
wordmark in type afterwards.** Diffusion text is unreliable, and a broken wordmark is a
brand problem that outlives the launch.

Ask `art-director` for the capsule direction. A capsule that does not match the game's
actual look sells the wrong game and produces refunds.

## 4. Present

```
## Steam prep

Short description (<n>/300)
"<text>"

Tags: <in order, with the list each targets>

Capsules
| Asset | Size | Must be legible | Status |

Screenshots: <n> planned, <n> captured
First GIF: <the three seconds>

Depots: <n>   Branches: <list>
Achievements: <n>, all with a grant site: <yes | the ones missing>
Cloud: <paths>

Refund risk: <the honest answer>
Rollback tested: <yes | no - this blocks OPS-READY>
```

## 5. What this skill does not do

It does not upload. It does not publish. It does not set the page live.
`steamcmd` and the ContentBuilder are denied in `.claude/settings.json` on purpose.

Everything here is prepared, written down and handed to the user, who does the
irreversible part themselves.

## 6. Close

```
✓ Steam prep complete.
Page copy: docs/publishing/steam-page.md
Assets needed: <n> capsules, <n> screenshots, 1 GIF
Achievements without a grant site: <n>  <-- these are stories
Rollback: <tested | untested>

▶ Next: /gen-asset for the capsule plates
   then: the user uploads. Nothing here does that.
```

---

## Token note

- **Two agent calls, parallel.**
- Run it early. The page written at launch week is written in a panic; the page written
  at vertical slice has been collecting wishlists for months.
