# NexusUnity — plan

> Editable **intent** file: ideas + plans — what we *mean to do*.
> This is NOT the event log. `events.jsonl` -> `summary.md` records what
> *happened*; this file records what we *intend*. The AI reads it at
> session start and edits it directly (like `PROJECT_MAP.md`): add ideas
> and plans, check items off, move done work down to Shipped. Plans are
> never logged as events.
>
> Strategic rationale and historical retirement table: `NexusFeature~/MASTER_PLAN_V3.md` (v3.0, §0-A and §19). Task execution spec: `NexusFeature~/TASKS_V3.md`.

## Ideas
- Pre-edit C# <-> Unity YAML impact query with **per-prefab / per-object unique serialized values**: before an agent modifies a `public` or `[SerializeField]` field (or `UnityEvent`-bound method), list every `.prefab`, `.unity`, `.asset`, and Prefab Variant referencing it along with the actual stored/overridden values vs C# default so refactors never silently wipe scene/prefab data (extends RFC #213).
- Expose Nexus canonical commands cleanly under `com.unity.pipeline 1.0.0` grouped style (`nexus <command>`) + `CommandEffect.ReadOnly`, with a short skill pack composing alongside `unity-cli`.

## Active plans
- [x] **V3-01 — Pipeline 1.0.0 CLI contract fix** ✅ DONE 2026-11-17: merged via PR #222 into `development` (`a26ed29`), CI Scope green. `encoding` replaces `format`, kebab-case flags, `nexus` group + `CommandEffect.ReadOnly` on Pipeline >=1.0, drift blind spots closed, stabilize scripts repaired, #0008 fixed. Harness now on Pipeline 1.0.0: EditMode 216/216. Evidence `Research~/evidence/V3-01/`.
- [x] **V3-02 — Deprecation ledger & Unity 6.x polish** ✅ DONE: merged via PR #223 into `development` (2026-11-17). Ledger drops `beta.10` prose, CS0618 fixed, Game View stays strict `unavailable`. 6000.4.3f1 batchmode compile of `development` b1aeb1e: 0 errors, 0 warnings (2026-11-19, `Research~/evidence/V3-02/compile-6000.4.3f1.txt`).
- [x] **V3-03 — Deprecate/freeze obsolete surfaces** ✅ DONE: merged via PR #224 (2026-11-17). `unity_dump_scene_graph` deprecated and capped at 200 nodes, `MCPCliInstaller` frozen, `architecture_lab` archived. Evidence `Research~/evidence/V3-03/`.
- [x] **V3-04 — Ecosystem re-verification** ✅ DONE 2026-11-19 via PR #230 (`rework3/V3-04` -> `development`, open): `Research~/evidence/V3-04/ecosystem_delta.md`; first numeric capture rows in `BENCHMARKS.md`. Nothing dropped. RESCOPE: V3-21 (vs IvanMurzak RQL), V3-22 (#0011 two-tick latency first), V3-31 (reuse `console status`/`script recompile_status`).
- [x] **V3-00 — Branch & gate hygiene** ✅ DONE 2026-11-16: merged via PR #214 into `development` (`39e9ab7`), CI Scope green; gates green by fixing code (pytest 52, EditMode 216/216); fixed #0005, #0006; closed #0001–#0004. `Research~/` + `NexusFeature~/` are local-only (gitignored). Evidence `Research~/evidence/V3-00/`.
- [x] **V3-10 — AssetImpact corpus + `unity vcs` 1.0.1 boundary** ✅ DONE 2026-11-20: merged via PR #231 into `development` (`6d054f3`), CI Scope green. Unity-written Force Text corpus in `Tests/Fixtures~/AssetImpact`, frozen `expected_findings.json` (sha256 `4675bba6…c71e7`). `vcs` 1.0.1 still has no field/UnityEvent/pre-edit query; REPLAN_IF not triggered. Evidence `Research~/evidence/V3-10/`.
- [x] **V3-11 — Layer A `nexus.asset_impact`** ✅ DONE 2026-11-20: merged via PR #232 into `development` (`0532885`), CI Scope green. Core scanner, public surface (HTTP, `find_references` symbol mode, Pipeline `nexus asset_impact`), Python bridge (`unity_search_manager` references/impact, `unity_lint_project scope=assets`). Precision/recall 1.0 on the frozen corpus, 95–281 ms on 400 assets, EditMode 231/231. Evidence `Research~/evidence/V3-11/layer_a_report.md`.
- [x] **V3-12 — Layer B Editor truth (`#213` Phase 2)** ✅ DONE 2026-11-20 on `rework3/V3-12` (local commit, not pushed): `nexus.asset_impact` `level=editor|auto` adds a read-only `editor` section (`AssetImpactEditorTruth.cs`); never opens/dirties scenes. Parity vs Layer A 32/32 (100%) on the materialized V3-10 corpus, 0 Layer A defects; +15 ms cold / +2 ms warm per 100 assets; EditMode 234/234 + pytest 72; 6000.4.3f1 compile 0 errors. Evidence `Research~/evidence/V3-12/layer_b_parity.md`.
- [x] **V3-13 — Live agent efficacy & perf benchmark (`#213`)** ✅ DONE 2026-11-20 on stacked `rework3/V3-13` (local, not pushed): perf ~0.2 ms/asset static, +0.08 ms/asset Layer B; A/B 8 tasks/arm, both 8/8 pass, 0 regressions, hinted adoption 8/8 vs 1/8 unhinted. Verdict: fix output shape (compact default, post-edit `level=editor`, FSA aliases in Layer A) before new `#213` features. Evidence `Research~/evidence/V3-13/impact_benchmark.md`.
- [x] **V3-13 follow-up — output shape** ✅ 2026-11-20 on `rework3/V3-13-output-shape`: `results=auto|summary|full` (summary by default above 50 entries; 3.8× smaller rename impact at 300 assets) and Layer A follows `[FormerlySerializedAs]` (`serialized_as`). EditMode 238/238.

## Next
- [ ] **V3-20 — Bounded `nexus_snapshot` (`L-03`)**: `depth`/`root`/`limit`/`max_tokens` hierarchy + inspector/import summary (`scene get_hierarchy` in Pipeline 1.0.0 is still unbounded).
- [ ] **V3-21 — Visual capture focus & Windows validation (`L-05`)**: keep JPEG compression, target crop, and Scene View `target`/`instance_id` framing; verify D3D11/D3D12/Vulkan orientation.
- [ ] **V3-30 — Safe-Mode-resilient `nexus_compile` / `nexus_diagnose` + `V-08` static-state check**: wrap `unity recompile` / `unity status` (`safeMode.compileErrors`) + file grouping/source context + `Editor.log` fallback; flag unreset statics for Unity 6.6+/CoreCLR.
- [ ] **V3-31 — Semantic UGUI + UI Toolkit automation (`V-04a/b`)**: name/text/clickability targeting above `Pipeline 1.0.0`'s coordinate-only `RuntimeInputCommand`.
- [ ] **V3-32 — Package footprint (`P0-15`) & composable `skills~/nexus/`**: optional `com.unity.inputsystem`, exclude `UnityMCP.Runtime` from release builds.

## Someday / maybe
- Revisit `architecture_lab` evaluations only after `V3-01..V3-30` land and with a `Pipeline 1.0.0`-calibrated protocol (never under the obsolete `0.7/beta.10` v2.6 spec).

## Retired / closed (2026-11-16, see MASTER_PLAN_V2.md §19)
- ~~`L-04` modal dialog detection~~ -> superseded by `unity command editor dialog` + exit code `18` (`AWAITING_INPUT`).
- ~~`S-01` / `P0-07` multi-client installers~~ -> superseded by `unity setup` + `unity skill sync` (8+ clients).
- ~~`L-02` general `NexusJobStore` & `nexus_perf` profiler~~ -> superseded by `Pipeline 1.0.0` `diagnostics profiler_*` (12 commands), `diagnostics audit`, MCP `job_*`, and 6.6+ Fast Enter Play Mode.
- ~~`C-01` generic context-retrieval MCP~~ -> dropped after FHCE 67-run evaluation (`0/19` voluntary calls, no token saving).
- ~~`C-02` raw YAML GUID reverse-index~~ -> replaced by `unity vcs affected/guid` for file-level git graphs; semantic symbol/value layer moved to `V3-10`.
- ~~Old `P0-09` / `P0-16` / `P0-17` / `Gate F` (`0.7/beta.10` sweep)~~ -> suspended/archived as obsolete baseline.
- ~~Retired S-07 claims~~ -> `Pipeline 1.0.0` now writes outside `Assets/` without `.meta`, auto-opens Game View, and draws URP/HDRP camera Overlay.

## Shipped
- [x] `M1–M4` / `P0-02` / `S-02` / `S-04` / `S-07` (on `rework2/S-02` -> ancestor of `lab/architecture-evolution-v1`): Capture V2 (`ICaptureGateway`), canonical envelope `{ok, data, error, meta}`, `nexus.project_map`, `nexus.group_compile_errors`, `nexus.capture_game_view` (JPEG 768 + Scene framing), `EditorReadiness` `compile_epoch`/`reload_epoch`, dual Pipeline `0.7`/`0.8` asmdef (`Unity.Pipeline` + `Unity.Pipeline.Attributes`).
