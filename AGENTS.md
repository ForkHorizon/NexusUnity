# Agent Instructions: NexusUnity

## Repository Scope

This directory is the canonical Nexus Unity package repository.

Use this directory for all Nexus Unity package work:

- Branches, commits, pushes, pull requests, and issue fixes.
- Package source, bridge source, tests, changelog, README, and documentation.
- GitHub issue/PR work for `ForkHorizon/NexusUnity`.

The parent Unity project is only the local test harness:

`/Users/daliys/Daliys/UnityProjects/UnityTestForNexus`

Use the parent project to run the Unity Editor, MCP server, scenes, `unity_lint_project`, and live package validation. Do not modify parent project files unless the user explicitly asks for harness work.

## Branching

- Base branch: `development`.
- Verify the real branch names before creating history.
- Do not auto-push.

## Required Package Updates

When package behavior or public API changes, keep these aligned:

- `CHANGELOG.md`
- `README.md`
- `DOCUMENTATION.MD`
- `API_REFERENCE.MD` when tool contracts or public API shapes change
- Relevant tests under `Tests~/` or `Editor/tests/`

## Validation

Fast package validation:

```bash
PYTHONDONTWRITEBYTECODE=1 scripts/prepush-validate.sh --static-only
```

For Unity-facing behavior, also use the running test harness through MCP tools such as `unity_wait`, `unity_lint_project`, and `unity_editor_controller`.

Clean generated Python caches before validation if needed; `__pycache__` or `*.pyc` files are not part of package changes.
