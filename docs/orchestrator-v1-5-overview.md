# Pass Orchestrator v1.5 — Overview

Pass Orchestrator v1.5 is a supervised workflow for making large, multi-step
changes to this repository safely, reviewably, and reversibly, without a human
needing to babysit every individual step.

## How it works

- A **pass plan** is a JSON file under `pass-plans/` (validated against
  `pass-plans/pass-plan.schema.json`) that defines an ordered set of small,
  narrowly-scoped **sub-passes**, each with an explicit objective, allowed and
  forbidden paths, acceptance criteria, and executable validation commands.
- Every pass runs entirely on its own **isolated `pass/*` branch**, created
  from the plan's `base_branch` (typically `main`). The orchestrator never
  checks out, commits to, or pushes `main` directly.
- Each sub-pass produces exactly one **checkpoint commit** on the pass
  branch, using the deterministic message format
  `pass({sub_pass.id}): {sub_pass.title}`. This keeps every change small,
  attributable, and independently revertible.
- Before each checkpoint commit, the sub-pass must pass its **validation
  gate**: every command in its `validation` array must succeed, and a
  self-review of the diff must find no scope creep, regressions, or
  correctness/security issues.
- If a sub-pass's scope is ambiguous, requires leaving its allowed paths, or
  hits an unresolved safety/correctness problem, the orchestrator stops
  immediately and reports **`BLOCKED_CRITICAL`**, leaving the branch in its
  last good, fully committed state for a human to resolve.
- Once every sub-pass has passed its gate and a final full-diff validation
  succeeds, the orchestrator reports **`READY_FOR_HUMAN_REVIEW`** and stops.
- Merging is always **human-only**: the orchestrator never opens, approves,
  or merges a pull request. A human reviews the `pass/*` branch and decides
  whether and how to merge it into `base_branch`.

See `docs/pass-orchestrator.md` for the full policy write-up, and
`pass-plans/pass-plan.schema.json` / `pass-plans/example-docs-pass.json` for
the plan format and a worked example.
