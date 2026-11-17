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
- [ ] **V3-02 — Deprecation ledger & Unity 6.x polish** (committed on `rework3/V3-02`, 2026-11-17; PR not opened, not pushed): ledger drops `beta.10` prose, `unity_cli_stable` = `not_probed_in_editor (1.0.x GA released upstream)`, any SemVer `-` suffix is pre-release (#0009), CS0618 fixed (0 warnings on 6000.3.16f1), Game View stays strict `unavailable` (documented). EditMode 217/217, pytest 62, prepush static + quality gate 0 errors. Not verified on 6000.4+. Evidence `Research~/evidence/V3-02/`.
- [x] **V3-00 — Branch & gate hygiene** ✅ DONE 2026-11-16: merged via PR #214 into `development` (`39e9ab7`), CI Scope green; gates green by fixing code (pytest 52, EditMode 216/216); fixed #0005, #0006; closed #0001–#0004. `Research~/` + `NexusFeature~/` are local-only (gitignored). Evidence `Research~/evidence/V3-00/`.
- [ ] **V3-10 — RFC #213 MVP + Unique Serialized Value Inspector**: static Layer A (`find_references` symbol mode, `analyze_change_impact`, `validate_asset_integrity`) extracting per-prefab/scene/variant unique serialized field values, `UnityEvent` string bindings, `Missing Script` / dangling GUIDs, reusing `unity vcs` for git-diff filtering without duplicating `vcs diff`/`vcs guid`.

## Next
- [ ] **V3-20 — Bounded `nexus_snapshot` (`L-03`)**: `depth`/`root`/`limit`/`max_tokens` hierarchy + inspector/import summary (`scene get_hierarchy` in Pipeline 1.0.0 is still unbounded).
- [ ] **V3-21 — Visual capture focus & Windows validation (`L-05`)**: keep JPEG compression, target crop, and Scene View `target`/`instance_id` framing; verify D3D11/D3D12/Vulkan orientation.
- [ ] **V3-11 — RFC #213 Phase 2 (Layer B Editor truth)**: `SerializedObject` / `PrefabUtility.GetPropertyModifications` / `AnimationUtility` read-only validation for complex prefab variants and `SerializeReference`.
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
