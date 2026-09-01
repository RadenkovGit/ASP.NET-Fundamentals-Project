#!/usr/bin/env python3
"""Validate a completed Pass Orchestrator branch against its pass plan."""

from __future__ import annotations

import argparse
import fnmatch
import json
import subprocess
import sys
from pathlib import Path
from typing import Any


def run_git(args: list[str]) -> str:
    result = subprocess.run(
        ["git", *args],
        check=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
    )
    return result.stdout.strip()


def load_plan(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as handle:
        plan = json.load(handle)
    if not isinstance(plan, dict):
        raise ValueError("plan root must be an object")
    return plan


def matches_any(path: str, patterns: list[str]) -> bool:
    normalized = path.replace("\\", "/")
    for pattern in patterns:
        normalized_pattern = pattern.replace("\\", "/")
        if fnmatch.fnmatchcase(normalized, normalized_pattern):
            return True
    return False


def changed_paths_for_commit(commit: str) -> list[str]:
    output = run_git(["diff-tree", "--no-commit-id", "--name-only", "-r", commit])
    if not output:
        return []
    return [line.strip().replace("\\", "/") for line in output.splitlines() if line.strip()]


def commits_between(base_ref: str, head_ref: str) -> list[tuple[str, str]]:
    output = run_git(["log", "--reverse", "--format=%H%x00%s", f"{base_ref}..{head_ref}"])
    if not output:
        return []
    commits: list[tuple[str, str]] = []
    for line in output.splitlines():
        sha, subject = line.split("\x00", 1)
        commits.append((sha, subject))
    return commits


def main() -> int:
    parser = argparse.ArgumentParser(description="Validate a pass branch against a pass plan.")
    parser.add_argument("--plan", required=True, type=Path)
    parser.add_argument("--base-ref", required=True)
    parser.add_argument("--head-ref", required=True)
    args = parser.parse_args()

    plan = load_plan(args.plan)
    expected_branch = plan["pass_branch"]
    if args.head_ref not in {expected_branch, f"origin/{expected_branch}"}:
        print(
            f"Head ref '{args.head_ref}' does not match plan pass_branch '{expected_branch}'.",
            file=sys.stderr,
        )
        return 1

    errors: list[str] = []
    sub_passes = plan["sub_passes"]
    commits = commits_between(args.base_ref, args.head_ref)
    if len(commits) != len(sub_passes):
        errors.append(
            f"Expected {len(sub_passes)} checkpoint commits, found {len(commits)} "
            f"between {args.base_ref} and {args.head_ref}."
        )

    global_forbidden = plan.get("global_forbidden_paths", [])
    for index, sub_pass in enumerate(sub_passes):
        if index >= len(commits):
            break
        commit_sha, subject = commits[index]
        expected_subject = f"pass({sub_pass['id']}): {sub_pass['title']}"
        if subject != expected_subject:
            errors.append(
                f"Commit {commit_sha[:7]} subject mismatch: expected "
                f"'{expected_subject}', got '{subject}'."
            )

        paths = changed_paths_for_commit(commit_sha)
        if not paths:
            errors.append(f"Commit {commit_sha[:7]} changes no files.")
        allowed_paths = sub_pass["allowed_paths"]
        forbidden_paths = sub_pass["forbidden_paths"]
        for path in paths:
            if not matches_any(path, allowed_paths):
                errors.append(
                    f"Commit {commit_sha[:7]} changes '{path}', which is outside "
                    f"allowed_paths for {sub_pass['id']}."
                )
            if matches_any(path, forbidden_paths):
                errors.append(
                    f"Commit {commit_sha[:7]} changes '{path}', which matches "
                    f"forbidden_paths for {sub_pass['id']}."
                )
            if matches_any(path, global_forbidden):
                errors.append(
                    f"Commit {commit_sha[:7]} changes '{path}', which matches "
                    "global_forbidden_paths."
                )

    if errors:
        print("Pass branch validation failed:", file=sys.stderr)
        for error in errors:
            print(f"- {error}", file=sys.stderr)
        return 1

    print(
        f"Validated {len(commits)} checkpoint commit(s) on {args.head_ref} "
        f"against {args.plan}."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
