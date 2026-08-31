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

## How it's built

| Piece | Location | Purpose |
|---|---|---|
| Orchestration instructions | `.claude/skills/pass-orchestrator/SKILL.md` | The lifecycle contract and safety gates Claude follows when orchestrating a pass. This is a Claude Code [Agent Skill](https://code.claude.com/docs) — the current project-local mechanism for reusable instructions, invoked explicitly (e.g. `/pass-orchestrator ...`) rather than firing automatically, to avoid silent scope creep. |
| Pass-plan schema | `pass-plans/pass-plan.schema.json` | JSON Schema defining the machine-readable format for an "X Pass" and its ordered sub-passes. |
| Example pass plan | `pass-plans/example-docs-pass.json` | A concrete, harmless, docs-only 3-sub-pass plan used to test the orchestrator end-to-end. |
| This document | `docs/pass-orchestrator.md` | Human-facing policy explanation. |

No changes were made to `.github/workflows/claude.yml` and no new secrets or GitHub
App configuration are required — the orchestrator is just a prompt/skill plus a data
format that the existing `claude-code-action` executes when someone `@claude`-mentions
it in an issue/PR comment.

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

## Checkpoint commit policy

- Exactly one commit per completed sub-pass, never more, never bundled.
- Deterministic message format: `pass(<sub_pass.id>): <sub_pass.title>`
  (e.g. `pass(docs-example.1): Add CONTRIBUTING.md`).
- Commits are made only on `pass_branch`, never on `base_branch`.
- Because each sub-pass is isolated to one commit, any single sub-pass can be
  cleanly reverted independently of the others (see below).

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

## Limitations of v1

- No automated enforcement (e.g. branch protection or a bot) prevents the
  orchestrator from technically running commands outside `allowed_scope` — the
  safety boundary is currently the instructions in `SKILL.md`, not a hard
  technical sandbox. Human review before merge is still required.
- Schema validation of pass-plan JSON is done by Claude reading the schema at
  runtime, not by a CI-enforced validator (no new dependency was introduced for
  this in v1).
- Resuming after `BLOCKED_CRITICAL` relies on git history (checkpoint commits) on
  `pass_branch` to infer progress; it does not persist separate run-state.
- Designed for one pass in flight per branch; running multiple concurrent X
  Passes just means using multiple `pass_branch` values, each with its own plan
  file.
