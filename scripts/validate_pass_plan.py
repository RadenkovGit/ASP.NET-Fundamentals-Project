#!/usr/bin/env python3
"""Validate Pass Orchestrator plan files without third-party dependencies."""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path
from typing import Any


TOP_LEVEL_KEYS = {
    "schema_version",
    "pass_id",
    "pass_title",
    "pass_branch",
    "base_branch",
    "goal",
    "global_constraints",
    "global_forbidden_paths",
    "sub_passes",
    "final_validation",
}

SUB_PASS_KEYS = {
    "id",
    "title",
    "objective",
    "allowed_scope",
    "allowed_paths",
    "forbidden_scope",
    "forbidden_paths",
    "acceptance_criteria",
    "validation",
    "visual_checks",
}

FINAL_VALIDATION_KEYS = {"requirements", "commands"}


def fail(errors: list[str], path: str, message: str) -> None:
    errors.append(f"{path}: {message}")


def is_non_empty_string(value: Any) -> bool:
    return isinstance(value, str) and bool(value.strip())


def require_string(errors: list[str], doc: dict[str, Any], key: str) -> None:
    if not is_non_empty_string(doc.get(key)):
        fail(errors, key, "must be a non-empty string")


def require_string_list(errors: list[str], doc: dict[str, Any], key: str) -> None:
    value = doc.get(key)
    if not isinstance(value, list) or not value:
        fail(errors, key, "must be a non-empty array")
        return
    for index, item in enumerate(value):
        if not is_non_empty_string(item):
            fail(errors, f"{key}[{index}]", "must be a non-empty string")


def validate_plan(plan: dict[str, Any], source: Path) -> list[str]:
    errors: list[str] = []

    missing = TOP_LEVEL_KEYS - set(plan)
    extra = set(plan) - TOP_LEVEL_KEYS
    for key in sorted(missing):
        fail(errors, str(source), f"missing required top-level key '{key}'")
    for key in sorted(extra):
        fail(errors, str(source), f"unknown top-level key '{key}'")

    if missing:
        return errors

    for key in ["pass_id", "pass_title", "pass_branch", "base_branch", "goal"]:
        require_string(errors, plan, key)
    for key in ["global_constraints", "global_forbidden_paths"]:
        require_string_list(errors, plan, key)

    pass_id = plan.get("pass_id")
    pass_branch = plan.get("pass_branch")
    base_branch = plan.get("base_branch")

    if plan.get("schema_version") != "1.0":
        fail(errors, "schema_version", "must be exactly '1.0'")
    if is_non_empty_string(pass_id) and not re.fullmatch(r"[a-z0-9][a-z0-9-]*", pass_id):
        fail(errors, "pass_id", "must be kebab-case")
    if is_non_empty_string(pass_branch):
        if not pass_branch.startswith("pass/"):
            fail(errors, "pass_branch", "must start with 'pass/'")
        if pass_branch in {"main", "master"}:
            fail(errors, "pass_branch", "must not be main/master")
    if is_non_empty_string(base_branch) and base_branch.startswith("pass/"):
        fail(errors, "base_branch", "must not be a pass branch")
    if pass_branch == base_branch:
        fail(errors, "pass_branch", "must differ from base_branch")

    sub_passes = plan.get("sub_passes")
    if not isinstance(sub_passes, list) or not sub_passes:
        fail(errors, "sub_passes", "must be a non-empty array")
    else:
        seen_ids: set[str] = set()
        for index, sub_pass in enumerate(sub_passes, start=1):
            path = f"sub_passes[{index - 1}]"
            if not isinstance(sub_pass, dict):
                fail(errors, path, "must be an object")
                continue
            missing_sub = {
                "id",
                "title",
                "objective",
                "allowed_scope",
                "allowed_paths",
                "forbidden_scope",
                "forbidden_paths",
                "acceptance_criteria",
                "validation",
            } - set(sub_pass)
            extra_sub = set(sub_pass) - SUB_PASS_KEYS
            for key in sorted(missing_sub):
                fail(errors, path, f"missing required key '{key}'")
            for key in sorted(extra_sub):
                fail(errors, path, f"unknown key '{key}'")
            if missing_sub:
                continue

            sub_id = sub_pass.get("id")
            expected_id = f"{pass_id}.{index}"
            if sub_id != expected_id:
                fail(errors, f"{path}.id", f"must be '{expected_id}'")
            if sub_id in seen_ids:
                fail(errors, f"{path}.id", "duplicates an earlier sub-pass id")
            seen_ids.add(str(sub_id))

            for key in ["id", "title", "objective"]:
                require_string(errors, sub_pass, f"{path}.{key}")
            title = sub_pass.get("title")
            if is_non_empty_string(title) and not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9 ._:/()#-]*", title):
                fail(errors, f"{path}.title", "contains characters outside the schema's commit-safe title pattern")
            for key in [
                "allowed_scope",
                "allowed_paths",
                "forbidden_scope",
                "forbidden_paths",
                "acceptance_criteria",
                "validation",
            ]:
                require_string_list(errors, sub_pass, f"{path}.{key}")

            for command_index, command in enumerate(sub_pass.get("validation", [])):
                if isinstance(command, str) and " should " in command.lower():
                    fail(
                        errors,
                        f"{path}.validation[{command_index}]",
                        "looks like prose; use an executable command or prefix with 'manual:'",
                    )

            allowed_paths = set(sub_pass.get("allowed_paths", []))
            forbidden_paths = set(sub_pass.get("forbidden_paths", []))
            global_forbidden_paths = set(plan.get("global_forbidden_paths", []))
            overlap = allowed_paths & (forbidden_paths | global_forbidden_paths)
            if overlap:
                fail(
                    errors,
                    f"{path}.allowed_paths",
                    "overlaps forbidden paths exactly: " + ", ".join(sorted(overlap)),
                )

    final_validation = plan.get("final_validation")
    if not isinstance(final_validation, dict):
        fail(errors, "final_validation", "must be an object")
    else:
        missing_final = FINAL_VALIDATION_KEYS - set(final_validation)
        extra_final = set(final_validation) - FINAL_VALIDATION_KEYS
        for key in sorted(missing_final):
            fail(errors, "final_validation", f"missing required key '{key}'")
        for key in sorted(extra_final):
            fail(errors, "final_validation", f"unknown key '{key}'")
        for key in FINAL_VALIDATION_KEYS:
            require_string_list(errors, final_validation, f"final_validation.{key}")

    return errors


def main() -> int:
    parser = argparse.ArgumentParser(description="Validate Pass Orchestrator plan files.")
    parser.add_argument("plans", nargs="+", type=Path)
    args = parser.parse_args()

    all_errors: list[str] = []
    for plan_path in args.plans:
        try:
            with plan_path.open("r", encoding="utf-8") as handle:
                plan = json.load(handle)
        except Exception as exc:
            all_errors.append(f"{plan_path}: invalid JSON: {exc}")
            continue
        if not isinstance(plan, dict):
            all_errors.append(f"{plan_path}: root must be an object")
            continue
        all_errors.extend(validate_plan(plan, plan_path))

    if all_errors:
        print("Pass plan validation failed:", file=sys.stderr)
        for error in all_errors:
            print(f"- {error}", file=sys.stderr)
        return 1

    print(f"Validated {len(args.plans)} pass plan(s).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
