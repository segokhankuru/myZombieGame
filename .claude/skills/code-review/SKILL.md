---
name: code-review
description: Independent review of a change set for correctness, per-frame allocation, hardcoded tunables, scope fidelity and rule compliance. Runs the CR-CODE gate.
---

# /code-review [scope]

Phase 4. Owner: `code-reviewer`. Findings only - the reviewer never fixes anything.

---

## 1. Determine the change set

No argument -> `git diff` against the milestone branch point. With a path or story id ->
that scope.

Get the diff cheaply:
```bash
git diff --stat
git diff -- "*.cs"
```

Never diff `.unity`, `.prefab` or `.asset` files. If one changed, that is itself a
finding: those are authored through editor scripts, not hand-edited.

## 2. Assemble the context

| Include | Why |
|---|---|
| The diff, C# only | the thing being reviewed |
| The story's criteria, config keys and out-of-scope section | what it was supposed to do |
| The relevant `.claude/rules/*.md`, verbatim | the rules it must satisfy |
| The assembly each file belongs to | dependency-direction violations |

Pick the rules file from the paths touched: `Code/Gameplay` -> `gameplay-code.md`,
`Code/Net` -> `netcode.md`, `Code/UI` -> `ui-code.md`, shaders -> `shader-graphics.md`,
tests -> `test-code.md`, `Code/Editor` -> `editor-tools.md`. More than one applies if the
diff spans them.

## 3. One call - `code-reviewer`

```
<the diff>
<the story's acceptance criteria, config keys, out-of-scope section>
<the rules file content, verbatim>
Assemblies touched: <list>. Dependency rule: <the one-line rule>

Task: the CR-CODE gate. Review in this order:
1. CORRECTNESS - is each AC actually met, point at the code. Edge cases: empty, zero,
   negative, max, first frame, called twice, called during a scene load. Lifecycle
   errors. Order dependence: physics in Update, input in FixedUpdate.
2. UNITY COST - per-frame allocation is a BLOCKER in a hot path: LINQ, new, string
   concatenation, boxing, closures, GetComponent, Find. Coroutines never stopped.
   Unbounded Instantiate. Debug.Log in a hot path.
3. THE CONFIG RULE - a [SerializeField] numeric with a default in Gameplay/AI/Net/Systems
   is a MAJOR finding unless the story says it is a reference or an engineering constant.
4. SCOPE FIDELITY - work outside the story is a finding, not a bonus.
5. RULE COMPLIANCE - each item in the rules file above.
6. TEST QUALITY - a test per AC, real assertions, edge cases, no [Ignore], no leaked
   state between PlayMode tests.

Each finding: [BLOCKER|MAJOR|MINOR|NOTE] <file:line> - one-sentence claim plus reason.
At most 15. No style preferences, no praise, no repeating one issue five times.
Begin with "CR-CODE: APPROVED|CONDITIONAL|REJECTED".
```

## 4. Present

```
CR-CODE: <verdict>
<n> files, <n> lines changed

BLOCKER <n>
  <file:line> - <claim>
MAJOR <n>
MINOR <n>
NOTE <n>
```

## 5. Act on the verdict

| Verdict | Action |
|---|---|
| `APPROVED` | done |
| `CONDITIONAL` | MAJOR findings to the owner in **one** round; do not re-invoke the gate |
| `REJECTED` | BLOCKER findings to the owner; re-invoke the gate **once** |

Never send the reviewer's findings back to the reviewer for re-argument. A finding is a
finding; the decision to accept or override belongs to the code's owner, and an override
goes in `docs/DECISIONS.md`.

## 6. Record

Append `CR-CODE` to `.state/gates.jsonl`. If findings were overridden, one line in
`docs/DECISIONS.md` with the reason - a knowingly accepted finding is a decision, an
unrecorded one is a surprise for whoever hits it later.

## 7. Close

```
✓ CR-CODE <verdict>
Fixed: <n>   Accepted as-is: <n>   Deferred to backlog: <n>

Most common finding type this milestone: <type>
<if it repeats: "This keeps coming back - add it to the rules file or the story template.">

▶ Next: /dod-check <story>
```

---

## Token note

- **One agent call**, two only on `REJECTED`.
- The rules file is embedded verbatim and unchanged across reviews - stable text caches
  well, so the repetition is nearly free.
- A finding type that recurs across milestones is a **template** problem. Fix it in
  `.claude/rules/` once instead of finding it forever.
