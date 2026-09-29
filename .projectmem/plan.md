# NexusUnity — plan

> Editable **intent** file: ideas + plans — what we *mean to do*.
> This is NOT the event log. `events.jsonl` -> `summary.md` records what
> *happened*; this file records what we *intend*. The AI reads it at
> session start and edits it directly (like `PROJECT_MAP.md`): add ideas
> and plans, check items off, move done work down to Shipped. Plans are
> never logged as events.

## Ideas
- Dual backend now (Legacy HTTP/MCP + optional Unity Pipeline later); hybrid primary only after M4 gates.

## Active plans
- [x] M1 — Extract Capture V2 into transport-independent `Editor/Capture` behind `ICaptureGateway` and route `capture_game_view_screenshot` through it without changing the Legacy HTTP/MCP response schema.
- [x] M2 — Canonical commands + dual registration for `nexus.project_map`, `nexus.group_compile_errors`, `nexus.capture_game_view` only.
  - [x] One command descriptor + one handler per POC command.
  - [x] Legacy HTTP projection; Pipeline `[CliCommand]` wrappers optional.
  - [x] No hard `com.unity.pipeline` dependency.
  - [x] `capture_game_view_screenshot` HTTP schema unchanged.

- [x] M3 — Explicit Legacy and Unity Pipeline transport adapters; runtime setting stays `legacy` until M4.
  - [x] `legacy` / `pipeline` / `auto` setting, default `legacy`; `auto` prefers Legacy.
  - [x] Async Pipeline capability probe; never block init.
  - [x] Explicit pipeline may skip 8081 when another project owns it.

- [x] M4 — `auto` prefers Pipeline on eligible installs; Legacy remains fallback; user can force Legacy.
  - [x] Eligibility: Unity 6000, package, commands, session, healthy probe.
  - [x] Default requested mode `auto`. HTTP is not removed.

- [x] M5 — Publish Legacy HTTP sunset timeline. Do not remove HTTP. Do not mark fully deprecated while CLI/Pipeline are pre-release.
  - [x] Gate ledger in `get_server_status`.
  - [x] Docs timeline; Legacy remains fully supported.

- [x] Stabilization / acceptance pass. M4 ACCEPTED (EditMode 152/152, two-editor A/B, 20 pending-GPU reloads, persistent MCP wait = 3 Editor ticks, overlay orientation). Legacy removal BLOCKED. Internal merge only, not RC. Do not start a new architecture.

## Next
- Legacy removal only after Unity CLI 1.0 stable, non-experimental Pipeline, and one Nexus stable release. Do not start.

## Someday / maybe

## Shipped
_Move completed plans here so the top stays about the future._
