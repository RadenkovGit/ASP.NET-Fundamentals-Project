# Pass Orchestrator v1

Pass Orchestrator is a lightweight convention (not a new service or app) for running
large, multi-step Claude Code changes as a supervised sequence of small, gated
"sub-passes" on one dedicated branch — using the **existing** `@claude` GitHub Action
already configured in this repo (`.github/workflows/claude.yml`). It adds no new
authentication, no new workflow, and no third-party service.

This v1 is intentionally conservative: it improves Claude's local execution
discipline, checkpointing, and final signaling, but it is not yet a fully
automated two-agent gate. In particular, ChatGPT can currently audit GitHub state
read-only, but cannot reliably write approval markers, create issues, update
files, or merge PRs through the GitHub connector in this setup.

The optional v2 audit gate adds an explicit pause-and-approve protocol for plans
that set `audit.required: true`. It still does not pretend ChatGPT has GitHub
write access; approval is represented as a machine-readable GitHub comment ledger
entry written by a human, local Codex session, or another authenticated actor.

## How it's built

| Piece | Location | Purpose |
|---|---|---|
| Orchestration instructions | `.claude/skills/pass-orchestrator/SKILL.md` | The lifecycle contract and safety gates Claude follows when orchestrating a pass. This is a Claude Code [Agent Skill](https://code.claude.com/docs) — the current project-local mechanism for reusable instructions, invoked explicitly (e.g. `/pass-orchestrator ...`) rather than firing automatically, to avoid silent scope creep. |
| Pass-plan schema | `pass-plans/pass-plan.schema.json` | JSON Schema defining the machine-readable format for an "X Pass" and its ordered sub-passes. |
| Example pass plan | `pass-plans/example-docs-pass.json` | A concrete, harmless, docs-only 3-sub-pass plan used to test the orchestrator end-to-end. |
| Audit-gated example | `pass-plans/example-audit-gated-docs-pass.json` | A harmless docs-only plan for testing the optional independent audit pause between sub-passes. |
| Plan validator | `scripts/validate_pass_plan.py` | Dependency-free validation for required fields, pass/sub-pass id shape, branch safety, audit gate shape, path fields, and validation command shape. |
| Branch validator | `scripts/validate_pass_branch.py` | Checks a completed `pass/...` branch for one checkpoint commit per sub-pass, deterministic commit messages, and per-commit changed paths within scope. |
| CI validation | `.github/workflows/pass-orchestrator-validate.yml` | Read-only GitHub Action that validates pass plans on PRs and validates `pass/...` branch PRs against their matching pass plan. |
| This document | `docs/pass-orchestrator.md` | Human-facing policy explanation. |

The existing `.github/workflows/claude.yml` remains the execution entrypoint. It
uses the existing Claude OAuth secret and a narrow `--allowedTools` allowlist for
the git/read-only GitHub operations the orchestrator needs. No new secrets,
GitHub Apps, or third-party services are required.

## How a large pass is started

1. A human writes a pass plan JSON file under `pass-plans/` conforming to
   `pass-plans/pass-plan.schema.json`, and commits it (typically directly to
   `base_branch`, or via a normal small PR — the plan file itself is not part of
   the pass it describes).
2. The human opens an issue or PR comment, e.g.:
   > `@claude /pass-orchestrator start pass-plans/example-docs-pass.json`
3. The existing `claude.yml` workflow fires (unchanged), runs Claude Code, which
   loads the `pass-orchestrator` skill and begins execution per the contract in
   `SKILL.md`.
4. Claude creates `pass_branch` from `base_branch` (if it doesn't already exist)
   and begins sub-pass `.1`. The branch must use the `pass/...` namespace and
   must not be the same as `base_branch`.

## How the orchestrator consumes the pass plan

The skill reads the JSON pass plan and walks `sub_passes` strictly in order. For
each sub-pass it: understands the scoped objective → implements only within
`allowed_scope` → runs the listed `validation` → critically self-reviews the diff
→ fixes what it finds → re-validates → makes exactly one checkpoint commit. It
will not start sub-pass N+1 until sub-pass N has a passing checkpoint commit. Full
detail is in `.claude/skills/pass-orchestrator/SKILL.md`.

On resume, the orchestrator must first audit the existing branch history. Existing
checkpoint commits must match the plan in order and use the exact checkpoint
message format; unexpected, duplicate, missing, or out-of-order commits block the
run.

## Optional independent audit gate

Plans may include:

```json
{
  "audit": {
    "required": true,
    "mode": "github-comment-ledger",
    "approval_sources": ["chatgpt-relayed-by-human", "codex-local"],
    "approved_auditors": ["RadenkovGit"]
  }
}
```

When this is enabled, each sub-pass stops after its checkpoint commit is pushed.
Claude emits:

```text
AWAITING_AUDIT
```

This is an expected pause, not a failure. An external reviewer then audits the
exact checkpoint SHA and posts a machine-readable approval entry in the same
GitHub issue or PR thread:

```text
AUDIT_APPROVED
pass_id: <pass_id>
sub_pass_id: <sub_pass.id>
checkpoint_sha: <full checkpoint sha>
source: <source label from audit.approval_sources>
auditor: <name or handle>
summary: <one-line verdict>
```

An authorized reviewer can explicitly reject a checkpoint with the same fields:

```text
AUDIT_REJECTED
pass_id: <pass_id>
sub_pass_id: <sub_pass.id>
checkpoint_sha: <full checkpoint sha>
source: <source label from audit.approval_sources>
auditor: <name or handle>
summary: <one-line reason>
```

On resume, Claude must re-read the comment thread and continue only if the entry
matches the exact pass id, sub-pass id, checkpoint SHA, accepted source, and an
authorized GitHub comment author. The GitHub comment author's login must be
listed in `audit.approved_auditors`, the comment's `authorAssociation` must be
`OWNER`, `MEMBER`, or `COLLABORATOR`, and the entry's `auditor:` value must match
that GitHub login. A listed auditor who is not also a repository owner, member,
or collaborator cannot clear the gate. Missing, stale, rejected, or unauthorized
approval keeps the pass paused. A rejected or ambiguous audit should not be
converted into approval.

For the supervised-auto workflow, a local Codex/ChatGPT orchestrator can handle
`AWAITING_AUDIT` silently by reading the diff, posting the approval comment using
an authenticated local GitHub CLI session, and re-invoking Claude. The human
should still be notified only for `BLOCKED_CRITICAL` or final
`READY_FOR_HUMAN_REVIEW`, unless no configured auditor is available.

## Checkpoint commit policy

- Exactly one commit per completed sub-pass, never more, never bundled.
- Deterministic message format: `pass(<sub_pass.id>): <sub_pass.title>`
  (e.g. `pass(docs-example.1): Add CONTRIBUTING.md`).
- Commits are made only on `pass_branch`, never on `base_branch`.
- Because each sub-pass is isolated to one commit, any single sub-pass can be
  cleanly reverted independently of the others (see below).

## Automated validation

Pass Orchestrator v1.1 adds a lightweight validation layer:

- `scripts/validate_pass_plan.py` validates pass-plan files without external
  packages.
- `scripts/validate_pass_branch.py` validates completed `pass/...` branches
  against the selected pass plan.
- `.github/workflows/pass-orchestrator-validate.yml` runs these checks on PRs to
  `main` with read-only repository permissions.

For normal infrastructure PRs, CI validates the plan files only. For PRs whose
head branch starts with `pass/`, CI also finds the matching plan by `pass_branch`
and checks that the branch has exactly one correctly named checkpoint commit per
sub-pass, with each commit touching only that sub-pass's `allowed_paths` and no
`forbidden_paths` / `global_forbidden_paths`.

Path patterns use Python `fnmatch` semantics. In practice, `*` and `**` can match
across `/`, so use exact paths for single-file sub-passes and directory patterns
such as `StudentPlannerApp/**` for forbidden trees.

This does not replace human review. It is a hard, repeatable tripwire for the
most important shape and scope invariants.

## `BLOCKED_CRITICAL` semantics

Emitted (as an exact, standalone line — easy for external tooling, including
another LLM, to grep for) when the orchestrator hits something it must not resolve
on its own: ambiguous scope, a design decision with real product impact, or a
serious problem it couldn't fix within the declared scope. When this happens:

- The pass **stops** — no further sub-passes are attempted in that run.
- The branch is left in its last fully-committed, clean state (no partial work).
- A human-readable explanation follows the marker, naming the sub-pass and the
  specific decision/input needed.
- Once the human resolves the ambiguity (e.g. by editing the pass plan, or
  replying with a decision), the pass can be resumed with the same
  `/pass-orchestrator continue ...` invocation — the orchestrator detects
  already-completed sub-passes via existing checkpoint commits on `pass_branch`.

## `READY_FOR_HUMAN_REVIEW` semantics

Emitted (also as an exact, standalone line) only after **all** sub-passes have
passed their individual gates **and** a final full-pass validation has run
comparing `base_branch` to the final `pass_branch` HEAD (interaction effects
between sub-passes, no unrelated changes, `base_branch` provably untouched, all
`final_validation` commands green). This is the signal that the X Pass is
complete and ready for a human to look at — nothing more happens automatically
after this.

## How final human approval works

The orchestrator never opens a PR, never merges, and never force-pushes. Once
`READY_FOR_HUMAN_REVIEW` appears, a human:

1. Reviews `pass_branch` (diff, commit-by-commit or as a whole).
2. Optionally opens a normal PR from `pass_branch` into `base_branch` themselves.
3. Merges (or requests changes / asks for another pass) using their normal
   process. The orchestrator has no merge authority by design.

## How to revert one sub-pass

Because each sub-pass is exactly one commit:

```
git revert <checkpoint-commit-sha>
```

on `pass_branch`. This cleanly undoes just that sub-pass without touching the
others.

## How to revert the whole X Pass

`base_branch` (e.g. `main`) is never modified during a pass, so there is nothing
to revert there. To abandon the whole X Pass, simply don't merge `pass_branch`
(and delete it if desired). No history rewriting or force-push is ever needed.

## Why `main` remains protected by process

The orchestrator only ever checks out, commits to, and (if pushing) pushes
`pass_branch`. `base_branch`/`main` is read-only context used purely to create
`pass_branch` and to diff against for final validation. Combined with "never
force-push," "never merge," and "never auto-approve," this means the worst case
of a misbehaving pass is a messy `pass_branch` that a human can inspect, fix, or
discard.

This is a process guarantee, not a hard permission boundary in v1. The existing
GitHub Action token has repository write permissions, so branch protection and
human review remain important until a stricter technical gate exists.

## What v1 does not solve

The desired long-term workflow includes an independent ChatGPT review after each
Claude sub-pass before the next sub-pass begins. This v1 does not fully enforce
that. It currently enforces Claude implementation, Claude self-review, checkpoint
commits, and final readiness signaling. ChatGPT can inspect repository state and
provide an independent audit, but because GitHub write operations currently fail
with `403 Resource not accessible by integration`, ChatGPT cannot write an
`AUDIT_APPROVED` marker back to GitHub or act as an automatic gate.

Future versions may add a separate reviewer run, persisted machine-readable pass
state, a GitHub Action orchestrator that pauses between stages, or another
authenticated coordination channel. Until then, final merge remains manual.

The optional v2 audit gate is the first coordination channel: it creates a
machine-readable approval protocol without adding auto-merge authority. It is
still a supervised workflow, because the approval writer is an authenticated
actor outside the Claude implementer run.

## Limitations of v1

- No automated enforcement (e.g. branch protection or a bot) prevents the
  orchestrator from technically running commands outside `allowed_scope` — the
  safety boundary is currently the instructions in `SKILL.md`, not a hard
  technical sandbox. Human review before merge is still required.
- Schema validation of pass-plan JSON is done by Claude reading the schema at
  runtime and by the lightweight CI validators in this repository. The validators
  intentionally avoid third-party dependencies, so they check the safety-critical
  plan shape rather than every JSON Schema draft feature.
- Resuming after `BLOCKED_CRITICAL` relies on git history (checkpoint commits) on
  `pass_branch` to infer progress; it does not persist separate run-state.
- Designed for one pass in flight per branch; running multiple concurrent X
  Passes just means using multiple `pass_branch` values, each with its own plan
  file.
