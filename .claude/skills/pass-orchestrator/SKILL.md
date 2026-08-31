---
name: pass-orchestrator
description: Use ONLY when explicitly asked to start, resume, or continue a multi-sub-pass "X Pass" orchestration driven by a JSON pass-plan file under pass-plans/ (e.g. "/pass-orchestrator start pass-plans/example-docs-pass.json", "continue the docs-example pass", "run sub-pass docs-example.2", "resume the X pass"). Do not activate for ordinary one-off coding requests, or when no pass-plan file is named or in progress on the current branch.
---

# Pass Orchestrator v1

You are supervising a **multi-pass, gated workflow** defined by a JSON pass plan
(schema: `pass-plans/pass-plan.schema.json`). Follow this contract exactly. It exists
to keep large, multi-step changes safe, reviewable, and reversible without requiring
a human to babysit every step.

## 0. Resolve inputs

1. Identify the pass plan file (path given by the invoker, e.g.
   `pass-plans/example-docs-pass.json`). Read and parse it as JSON. If it does not
   conform to `pass-plans/pass-plan.schema.json` (missing required fields, wrong
   types), stop and report `BLOCKED_CRITICAL` - do not guess at intent. Also stop
   if `pass_branch` is the same as `base_branch`, is the repository's default
   branch, or does not start with `pass/`.
2. Determine whether this is a **fresh start** or a **resume**:
   - Fresh start: `pass_branch` does not exist yet.
   - Resume: `pass_branch` exists and already has some checkpoint commits
     (see commit format below). Inspect `git log base_branch..pass_branch` to
     find the last completed sub-pass id and continue from the next one.
3. Confirm every `sub_pass.id` starts with `{pass_id}.` and appears in the same
   numeric order as the `sub_passes` array. If ids are missing, duplicated,
   out of order, or inconsistent with `pass_id`, stop with `BLOCKED_CRITICAL`.
4. Before making any change, fetch current remote refs if a remote is configured,
   confirm the working tree is clean, and confirm no untracked files would be
   accidentally captured by a checkpoint. If the working tree is dirty, stop with
   `BLOCKED_CRITICAL` unless every dirty path is explicitly part of the current
   sub-pass and you are resuming after a known interrupted local edit.
5. On resume, audit `git log --reverse base_branch..pass_branch` before continuing:
   every existing pass commit must match `pass({sub_pass.id}): {sub_pass.title}`
   in the same order as the plan, with no missing, duplicate, out-of-order, or
   extra commits. If history does not match the plan exactly, stop with
   `BLOCKED_CRITICAL`.

## 1. Hard safety invariants (never violate these)

- The entire X Pass lives on exactly one branch: `pass_branch` from the plan.
- Never check out, commit to, or push `base_branch` or the repository's default
  branch (typically `main`).
- Never modify anything outside a sub-pass's declared `allowed_paths`, and never
  touch anything in its `forbidden_paths`, the plan's `global_forbidden_paths`,
  or anything forbidden by `global_constraints`.
- Never force-push, never rewrite history of existing checkpoint commits, never
  delete branches.
- Never open, merge, or auto-approve a pull request or merge into `base_branch`.
  Producing `READY_FOR_HUMAN_REVIEW` is the end of your responsibility — the
  merge decision belongs to a human.
- If `pass_branch` does not exist yet, create it from `base_branch` (fresh
  checkout) before doing any work. Do not create it from any other ref.
- If `pass_branch` already exists, check it out directly and verify it contains
  `base_branch` in its history. If it appears to be based on the wrong branch or
  has diverged in a way that cannot be explained by checkpoint commits, stop with
  `BLOCKED_CRITICAL`.
- Before every commit, review the staged diff by path and content. Stage only the
  files allowed by the current sub-pass. Never use a broad commit that can include
  unrelated files.

## 2. Per-sub-pass loop (strict order, one at a time)

For each sub-pass in `sub_passes`, in array order, skipping any already covered by
an existing checkpoint commit on `pass_branch`:

a. **Understand** — restate the sub-pass `objective` and `acceptance_criteria` to
   yourself; identify exactly what `allowed_scope` means and what `allowed_paths`
   permits at the file-path level.
b. **Implement** — make the smallest change that satisfies the objective, staying
   strictly inside `allowed_paths`. If you find you need to touch something in
   `forbidden_paths`, `global_forbidden_paths`, or outside `allowed_paths` to do
   the job properly, that is a scope conflict — treat it as a potential
   `BLOCKED_CRITICAL` (see §4), don't silently expand scope.
c. **Validate** — run every command/check listed in the sub-pass's `validation`
   array (and any `visual_checks` if present, using available browser tooling).
   All must pass. Treat items beginning with `manual:` as explicit checks to
   verify and report; all other validation items are shell commands and must be
   run exactly as written from the repository root.
d. **Self-review the diff** (`git diff` against the last checkpoint) specifically
   for: correctness, regressions, scope creep beyond `allowed_scope`,
   architecture issues, security/data risks, and UX/UI consistency where
   applicable.
e. **Fix** anything the self-review finds, autonomously, as long as the fix stays
   inside `allowed_paths`. If a fix would require leaving that scope, treat it as
   in (b) above.
f. **Re-run validation** from (c) until everything passes.
g. **Scope-check changed paths** — list changed paths since the last checkpoint
   and confirm every path matches the current sub-pass's `allowed_paths`, and no
   path matches `forbidden_paths` or `global_forbidden_paths`.
h. **Checkpoint commit** — create exactly ONE commit for this sub-pass, on
   `pass_branch`, with a deterministic message:

   ```
   pass({sub_pass.id}): {sub_pass.title}
   ```

   Example: `pass(docs-example.1): Add CONTRIBUTING.md`

   Do not bundle multiple sub-passes into one commit, and do not split one
   sub-pass across multiple commits. If validation or review finds a problem
   before the checkpoint commit, amend the working tree first and commit only
   after the whole gate passes.

Do not begin the next sub-pass until the current one's checkpoint commit exists
and its gate has passed.

## 3. Between sub-passes

After each checkpoint commit, re-confirm the hard invariants in §1 still hold
(especially: still on `pass_branch`, `base_branch` untouched, no unrelated files
staged). Only then proceed to the next sub-pass.

## 4. BLOCKED_CRITICAL

Stop the entire pass immediately — do not attempt further sub-passes — if you hit
any of:

- A sub-pass's scope is ambiguous or contradictory, or satisfying its
  `objective` genuinely requires leaving `allowed_paths` or entering
  `forbidden_paths` / `global_forbidden_paths`.
- A serious, unresolved correctness, security, or data-integrity problem that
  autonomous fixing did not resolve after a reasonable attempt.
- An ambiguous product/design decision that materially changes user-facing
  behavior and isn't settled by the plan's text.
- Validation for a sub-pass cannot be made to pass without violating scope.
- Changed-path checks show files outside `allowed_paths`, or files matching
  `forbidden_paths` / `global_forbidden_paths`.

When this happens, output the exact literal marker on its own line:

```
BLOCKED_CRITICAL
```

followed by a clear, specific explanation: which sub-pass, what the problem is,
what you tried, and exactly what decision or input is needed from a human. Do not
proceed further in the same run. Leave the branch in its last good (fully
committed) state - never leave partially-applied, uncommitted work. If the
blocking condition is discovered after local edits but before the checkpoint
commit, discard only those uncommitted edits for the current sub-pass after
confirming they are limited to the current sub-pass's `allowed_paths`. If unrelated
or unsafe uncommitted edits exist, do not guess; report them in `BLOCKED_CRITICAL`.

Minor implementation problems (typos, small bugs surfaced by validation, style
issues) are NOT `BLOCKED_CRITICAL` — fix those yourself per §2(e).

## 5. Final pass validation (after all sub-passes pass)

Once every sub-pass has a checkpoint commit and passed its own gate:

1. Diff the entire pass from `base_branch` to the current `pass_branch` HEAD
   (`git diff base_branch...HEAD` / `git log base_branch..HEAD`).
2. Re-review that full diff for interaction effects between sub-passes (not just
   each one in isolation).
3. Run every command in `final_validation.commands` and confirm every statement
   in `final_validation.requirements` holds.
4. Confirm `base_branch` (e.g. `main`) has no new commits and was never pushed
   to by this run.
5. Confirm there are no unrelated/stray changes (only files touched are ones
   declared across the sub-passes' `allowed_paths`, and no files match
   `global_forbidden_paths`).

If anything in this final check fails, treat it as a defect. Because all
sub-pass checkpoint commits already exist at this stage, do not create extra
fixup commits automatically and do not rewrite existing checkpoint commits. If
the defect is serious enough to block readiness, stop and emit `BLOCKED_CRITICAL`
with the failing requirement, the affected files/commits, and a recommended next
step. A human can then decide whether to add a new explicit sub-pass, revise the
plan, or restart the pass branch.

## 6. READY_FOR_HUMAN_REVIEW

Only once the full pass (§5) is clean, output the exact literal marker on its
own line:

```
READY_FOR_HUMAN_REVIEW
```

followed by a concise summary: pass id/title, list of sub-passes with their
checkpoint commit hashes/messages, confirmation that `base_branch` is untouched,
and a pointer to the branch (`pass_branch`) for human review. This is the signal
that a human should now review and decide on merging — you do not merge.

## 7. What a human does with the result

- `BLOCKED_CRITICAL` → human resolves the ambiguity/problem, then the pass can
  be resumed (this skill supports resume — see §0).
- `READY_FOR_HUMAN_REVIEW` → human reviews `pass_branch` (e.g. by opening a PR
  from `pass_branch` into `base_branch` themselves) and decides whether/how to
  merge. This skill never opens or merges that PR.
- To revert one sub-pass: `git revert <checkpoint-commit-sha>` on `pass_branch`
  (each sub-pass is exactly one commit, so this is always a single clean
  revert).
- To revert the whole X Pass: simply do not merge `pass_branch`, or delete it;
  `base_branch` was never modified, so no revert on `base_branch` is ever
  needed.

See `docs/pass-orchestrator.md` for the full policy write-up and
`pass-plans/pass-plan.schema.json` / `pass-plans/example-docs-pass.json` for the
plan format and a worked example.
