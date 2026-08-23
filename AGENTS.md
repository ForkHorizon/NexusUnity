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

<!-- SOMA_MEMORY_TOOLS_START -->
## Memory Tools

Default mode: light. Do not spend tokens on memory tools for small, obvious, single-file tasks.

- projectmem: use for bugs, regressions, multi-step changes, repeated attempts, or architecture decisions. For small self-contained edits, skip full memory startup and use targeted history checks only when useful.
- `.projectmem/config.toml`, `PROJECT_MAP.md`, and `plan.md` are shared project configuration. The append-only event log, issue files, generated summary/structure, watcher state, and generated `CLAUDE.md` bridge are local and ignored.
- This package uses the tracked `.githooks` directory. When `pjm` is installed, its `pre-commit`, `post-commit`, and `post-merge` wrappers provide warnings and local auto-capture; do not run `pjm hooks install`, which assumes `.git/hooks` exists.
- Verify that the projectmem MCP server is bound to this package before using write tools. If it is bound elsewhere, use the local `pjm` CLI from this package root instead.
<!-- SOMA_MEMORY_TOOLS_END -->
