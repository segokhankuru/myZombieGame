---
name: adr
description: Records one architecture decision - the forces, the options, the choice, and the implementation guidance that gets copied into story packets.
---

# /adr "<question>"

Any phase. Owner: `unity-architect`, approved by `technical-director`.
Produces `docs/architecture/adr/ADR-NNNN-<slug>.md`.

---

## 1. When this is the right skill

**ADR triggers:** a new package or marketplace asset, the save format, the network model,
object pooling strategy, render pipeline, addressables, a threading or job decision,
anything touching every system, anything irreversible after a public build.

**Not an ADR:** how one class is written, a naming choice, anything reversible in an
afternoon. Those belong in the code, not in a document nobody will read twice.

## 2. Frame the question

A good ADR question names the force, not the technology. "Should we use addressables"
is a shopping question; "how do we load level content without a loading screen on
min-spec" is a decision.

Restate the question that way before proceeding, and confirm it.

## 3. One call - `unity-architect`

```
<the question, restated>
<the relevant ARCHITECTURE.md sections>
<the relevant NFR or budget line from PERF-BUDGET.md>
<the constraint this has to live inside: target hardware, team size, calendar>

Task: ADR.
1. Context: which forces drive this, referencing SYS-* or PILLAR-* where they apply.
2. Options: at least three, including the do-nothing option. For each: pros, cons, and
   specifically why it is eliminated. An option eliminated without a reason was not
   considered.
3. Decision, as one imperative sentence: "We will X".
4. Consequences: what improves, and the cost we are accepting. Name the cost explicitly -
   an ADR with no downside is an advertisement.
5. Reversal cost: low, medium or high, and what specifically makes it so.
6. Implementation guidance: concrete instructions a programmer follows. Required pattern,
   forbidden pattern, the file or assembly it belongs in. THIS SECTION IS COPIED VERBATIM
   INTO STORY PACKETS, so write it for someone who will never open this document.
7. Verification: the test, lint rule or review item that proves the decision was applied.
```

## 4. Approval

If reversal cost is `high`, get `technical-director` sign-off before writing - one extra
call, and the only case where it is worth it. Low and medium reversal decisions do not
need a second opinion; asking for one on every ADR is how ADRs stop being written.

## 5. Present

```
ADR-NNNN: <title>
Decision: <the imperative sentence>
Cost accepted: <the downside>
Reversal: <low|medium|high> - <why>

Eliminated
| Option | Why not |

Implementation guidance (goes into every affected story)
<the block>

Verified by: <test / lint / review item>
```

## 6. Write

`docs/architecture/adr/ADR-NNNN-<slug>.md`, a row in `adr/index.md`, one line in
`docs/DECISIONS.md`.

If it changes the stack, update `docs/CONTEXT.md`. If it invalidates an earlier ADR,
mark that one `Superseded by ADR-NNNN` - never delete it. The superseded decision and
its reasoning are how the studio avoids re-making the same mistake.

## 7. Close

```
✓ ADR-NNNN: <title>
Reversal cost: <level>
Stories affected: <the ones whose packets need the guidance section>

▶ Next: /adr "<next question>"   or   back to what you were doing
```

---

## Token note

- **One agent call**, two if reversal cost is high.
- The Implementation guidance section is the point. It is copied into story packets so
  no programmer ever opens the ADR itself, which is what keeps `/dev-task` cheap.
