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

## Graphify

- Use `graphify-out/` as optional navigation for multi-file questions, impact/call-chain analysis, and unfamiliar architecture; skip obvious one-file tasks.
- Treat graph results as navigation clues and verify important findings in source.
- After code changes, refresh with `graphify update .` only when Graphify was used and indexed relationships may be stale. Routine refreshes are local/AST-based; do not run semantic labeling unless requested.

<!-- SOMA_MEMORY_TOOLS_START -->
## Memory Tools

Default mode: light. Do not spend tokens on memory tools for small, obvious, single-file tasks.

- projectmem: use for bugs, regressions, multi-step changes, repeated attempts, or architecture decisions. For small self-contained edits, skip full memory startup and use targeted history checks only when useful.
- `.projectmem/config.toml`, `PROJECT_MAP.md`, and `plan.md` are shared project configuration. The append-only event log, issue files, generated summary/structure, watcher state, and generated `CLAUDE.md` bridge are local and ignored.
- This package uses the tracked `.githooks` directory. When `pjm` is installed, its `pre-commit`, `post-commit`, and `post-merge` wrappers provide warnings and local auto-capture; do not run `pjm hooks install`, which assumes `.git/hooks` exists. Run `bash scripts/install-git-hooks.sh` once per clone: with the global CI Scope hook it links these hooks into `.git/hooks` (so the CI Scope commit check runs first), otherwise it sets `core.hooksPath=.githooks`. Never set a repo-local `core.hooksPath` by hand: it turns the CI Scope commit check off.
- Verify that the projectmem MCP server is bound to this package before using write tools. If it is bound elsewhere, use the local `pjm` CLI from this package root instead.
<!-- SOMA_MEMORY_TOOLS_END -->

<!-- >>> projectmem bridge >>> -->
## projectmem (MANDATORY)

This project uses projectmem for persistent memory + workflow rules.

This project is registered with projectmem as **nexusunity**.
Pass `project="nexusunity"` on any projectmem tool call.

SESSION START — call these three MCP tools, in this order, BEFORE
answering ANY question about this project:

  1. `get_instructions()` — loads the project's mandatory workflow
     rules. Without this you will not know how to log work
     correctly, when to use `add_note` vs `add_decision`, or how
     the event log is structured.
  2. `get_summary()` — loads project content. Do NOT answer from
     conversation history or by re-reading package.json / README /
     source files.
  3. `get_project_map()` — loads structural layout when relevant.

BEFORE modifying ANY file:
  - Call `precheck_file(file_path)` — check failure history first.

DURING work — use MCP write tools, NEVER edit `.projectmem/`
files directly via filesystem write:
  - On a bug discovery → `log_issue(summary, location)`.
  - After each fix attempt → `record_attempt(summary, outcome)`.
  - After confirmation → `record_fix(summary)`.
  - On a design choice → `add_decision(summary)`.
  - On a gotcha / setup detail → `add_note(summary)`.
  - When a new decision REPLACES an older one → pass
    `supersedes="<old event id>"` to `add_decision`. The log stays
    append-only; the old decision is tagged retired and drops out of
    summary.md, so the summary never shows two answers to the same
    question. Get ids from `get_summary()` or `search_events(query)`.

Editing `.projectmem/summary.md` or `.projectmem/PROJECT_MAP.md`
directly bypasses event logging and breaks audit replay. The
summary file regenerates from `events.jsonl` automatically — write
via the MCP tools and the summary will follow.

Do not re-scan source files when MCP tools can give you the same
answer in a few hundred tokens instead of thousands. This is not optional.
<!-- <<< projectmem bridge <<< -->
