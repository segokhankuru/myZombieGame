---
name: handoff
description: Produces a handoff packet when work moves between agents or sessions. At most 200 words, by design.
---

# /handoff

Any phase. Owner: whoever is handing over.

A handoff exists so the receiver does not have to reconstruct context by reading. If the
packet is longer than 200 words, it has become the thing it was meant to prevent.

---

## 1. Determine who and what

From the story's owner field and the escalation table in
`.claude/docs/coordination-rules.md` section 3. If the receiver is ambiguous, that is
itself the handoff problem - resolve it with `producer` first.

## 2. The packet

```
FROM: <role>   TO: <role>   WORK: <story-id>

DONE
- <what is finished and verified, not what was attempted>

REMAINING
- <what is left, in the order it should be done>

DECISIONS
- <what was decided and why - so the receiver does not re-open it>

WATCH OUT
- <pitfalls, assumptions made, which scene or prefab is claimed>

FILES
<paths touched>

VERIFY
<the exact command the receiver should run first>
```

The `VERIFY` line is the most useful part. It tells the receiver, in one command, whether
they are starting from a working state - which is the first thing anyone wants to know
and the last thing most handoffs answer.

## 3. Rules

- **200 words maximum.** Longer means the work should have been split, not documented.
- `DONE` means verified. "Wrote the class" is not done; "class written, EditMode tests
  pass, compiles clean" is.
- `WATCH OUT` must name any claimed scene or prefab. Scene ownership is the most
  expensive thing to get wrong between two agents.
- Never write "see the story file". The receiver has it; the packet exists for what is
  **not** in it.

## 4. Handing to a future session

A handoff to yourself tomorrow is the same packet. Write it into the story file under
`## Handoff` and update `docs/CONTEXT.md` current work. Then end the session - a fresh
context window with a good handoff beats a compacted one every time.

## 5. Close

```
Handoff: <from> -> <to>, story <id>
Verify with: <command>
Claimed: <scene or prefab, or none>
```

---

## Token note

Free - no agent calls. This is a formatting discipline, and it exists to keep the *next*
session cheap.
