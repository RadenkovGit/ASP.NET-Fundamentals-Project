# Pass Orchestrator v1.5 — Operational Checklist

A concise checklist for running one X Pass end-to-end. See
`docs/orchestrator-v1-5-overview.md` and `docs/pass-orchestrator.md` for the
full explanation behind each step.

## 1. Starting a pass

- [ ] Fetch `origin` and confirm local `main` matches `origin/main`.
- [ ] Confirm the working tree is clean.
- [ ] Read the pass plan JSON and validate it against
      `pass-plans/pass-plan.schema.json`.
- [ ] Confirm `pass_branch` starts with `pass/`, is not `main`, and does not
      already exist (fresh start) or already has valid checkpoint commits
      (resume).
- [ ] If the plan is invalid, ambiguous, or the branch state is unclear, stop
      with `BLOCKED_CRITICAL` rather than guessing.
- [ ] Create `pass_branch` from `base_branch` (never from any other ref).

## 2. Executing sub-passes

- [ ] Process `sub_passes` strictly in array order, one at a time.
- [ ] Stay inside each sub-pass's `allowed_paths`; never touch
      `forbidden_paths` or `global_forbidden_paths`.
- [ ] Make the smallest change that satisfies the sub-pass `objective`.

## 3. Validating each sub-pass

- [ ] Run every command in the sub-pass's `validation` array; all must pass.
- [ ] Self-review the diff for correctness, regressions, and scope creep.
- [ ] Fix issues found, re-run validation until everything passes.
- [ ] Confirm changed paths match only this sub-pass's `allowed_paths`.
- [ ] Create exactly one checkpoint commit:
      `pass({sub_pass.id}): {sub_pass.title}`.

## 4. Reviewing and finishing the pass

- [ ] After all sub-passes have checkpoint commits, diff `base_branch` to
      `pass_branch` HEAD as a whole and review for cross-sub-pass effects.
- [ ] Run every command in `final_validation.commands`; confirm every
      statement in `final_validation.requirements` holds.
- [ ] Confirm **`main` (or the repository's default branch) was never
      modified, checked out for writes, or pushed to by the orchestrator at
      any point** — this must always hold, in every pass.
- [ ] If everything is clean, report `READY_FOR_HUMAN_REVIEW`. If not, report
      `BLOCKED_CRITICAL` and stop at the last good checkpoint.

## 5. Merging

- [ ] The orchestrator never opens, approves, or merges a pull request, and
      **never merges into `main`** under any circumstance.
- [ ] A human reviews the `pass_branch` and decides whether/how to merge it,
      typically by opening their own PR into `base_branch`.
- [ ] To revert one sub-pass, `git revert` its single checkpoint commit. To
      revert the whole pass, simply do not merge (or delete) `pass_branch`;
      `main` was never touched, so no revert on `main` is ever needed.
