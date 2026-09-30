# Agent A — What to BUILD on top of Unity CLI/Pipeline

Angle: product engineer designing composite tools. Speed, task success, tokens.
Evidence base: repo source under `Editor/` (read-only), `NexusFeature~/*`, GitHub `Unity-Technologies/skills` (fetched), web search. Claims I could not verify are marked **[UNVERIFIED]**.

---

## 0. Executive position (and where I differ from the lead reviewer)

1. **Agree with "Unity does the hands, Nexus does the eyes and brain"** for commodity CRUD, but with one hard correction: **input is not a hand Unity has given us.** Nothing in the official `unity-cli` skill or its references mentions keyboard/mouse simulation, UI clicking, or profiler commands (fetched SKILL.md + `references/` listing, see §5). Input simulation, UI raycast and overlay-correct capture must stay Nexus-owned *and be improved* (keyboard + UGUI EventSystem path are missing today).
2. **The adoption failure is a surface problem, not a capability problem.** In 3 of 4 real sessions the agent never called Nexus; in the 4th it called `refresh_assets` + `wait` (19 s). Agents with a shell pick shell. Therefore every composite below must be callable as `unity command nexus_<x>` (a `[CliCommand]` projection), from `unity shell --protocol ndjson`, *and* as an MCP tool. MCP-only is the losing distribution.
3. **The flagship should not be a chatty loop the agent drives; it should be a declarative scenario the Editor executes in one call** (`nexus_verify` with a small step DSL). The agent writes intent once; Nexus runs play → input → wait → capture → assert inside Unity with event-driven waits and returns a verdict + evidence handles. That is what gives the 5–20× call reduction.
4. **Cheapest speed win is not transport; it's our own bridge.** `Editor/nexus_bridge/routes_editor.py::_wait_for_compilation` has fixed sleeps (0.5 s poll, then unconditional `sleep(2.0)`, then 1 s poll) and waits **up to 20 s** for `initialize` to fail when no domain reload happens — which is exactly the 19 s "compilation wait" in AI report 2.md. `_route_wait` polls at 1 s. Fixing these is worth more than any Pipeline migration.

---

## 1. Inventory: 121 raw methods in three buckets (+ transport)

Source: all `_methods["…"]`/`_asyncMethods["…"]` registrations in `Editor/MCPServerMethods*.cs` + `NexusLegacyCommandProjection` (project_map, group_compile_errors, capture_game_view). Bridge exposes 14 managers (`schemas_*.py`), and `route_tool` silently falls through to raw methods for unknown names, but they are not listed in `tools/list`, so agents never see them.

| Bucket | Count | Meaning |
|---|---|---|
| **(a) Duplicates Pipeline built-in → delegate in pipeline mode** | **~64** | Keep legacy implementation for non-Pipeline users, but do not advertise in pipeline/auto mode |
| **(b) Unique Nexus value → keep/strengthen** | **~40** | Intelligence, diffs, visual, input, UI Toolkit, lint |
| **(c) Rebuild as composite over primitives** | **~10** | Waits, write+compile, tests-wait, batch, bulk create |
| Transport/infra (Pipeline owns) | 7 | initialize, attach_existing_session, list_tools, ping_main_thread, shutdown_server, wait_for_ready, get_server_status |

By domain (counts approximate; boundary calls noted):

| Domain | (a) delegate | (b) keep/strengthen | (c) composite |
|---|---|---|---|
| Scene (7) | open/save/create_scene, create/destroy/get_game_object, instantiate_prefab | — | — |
| Hierarchy (10) | get_children, duplicate_object, set_active/enabled, set_sibling_index, remove_component, read_file (shell is better) | — | create_hierarchy, write_file, write_files_batch → `nexus_apply_code` |
| Component (7) | add_component, inspect_component, component_values, update_component, set_transform, set_parent | get_component_schema | — |
| Asset/Prefab (15) | move/copy/delete_asset, create_folder, list_assets, import_asset, refresh_asset_database, create_material, create_prefab | get_dependencies, explore_asset, get/apply/revert_prefab_overrides, edit_prefab_asset | — |
| Editor (18) | play/pause toggles, undo/redo, execute_menu_item, get_editor_state, get_project_info, tags/layers, list_scenes, set_selection, set_property, prefab stage open/close, focus_scene_view | lint_project, step_frame | run_tests + get_test_results → `nexus_test` |
| Search (7) | find_objects, find_by_path, get_root_game_objects, get_active_game_object, get_object_path, ping_object | find_references | — |
| ScriptableObject (8) | create/read/update/duplicate SO | list_fields_for_type, patch_scriptable_object, diff_scriptable_objects, diff_…_against_defaults | — |
| Reflection/Serialization (4) | invoke_method (→ `eval`), inspect_object | symbol_index, enforce_forced_defaults | — |
| PlayerPrefs (4) | all 4 (→ `eval`, rarely needed) | — | — |
| Sync (4) | — | — | is/wait_for_editor_idle, is/wait_for_asset_import_idle → event-driven `nexus_wait` |
| Snapshot/Context/Delta/Timeline (7) | dump_scene_graph (≈ get_scene_hierarchy) | compact_scene_snapshot, get_scene_dependencies, get_selected_object_full_context, show_unresolved_missing_references, scene_delta, get_editor_timeline | — |
| Visual/Semantic (4+3 cmds) | — | capture_game_view(_screenshot), capture_inspector_screenshot, semantic_find, generate_mermaid_diagram, project_map, group_compile_errors | — |
| Input (3) | — | simulate_mouse, simulate_touch, click_object_in_game (weak: `Camera.main` + world→screen only, Input System only, no keyboard, no UGUI raycast) | fold into `nexus_verify` |
| UI Toolkit editor automation (8) | — | ui_list_windows, ui_get_hierarchy, ui_query_elements, ui_click, ui_input_text, ui_get/set_window_rect, ui_capture_window_snapshot | — |
| Core misc | clear_logs, read_logs, create_primitive, attach_script | read_logs_since_cursor, tool usage stats | batch_execute |

Implication: **~64 methods (53%) are pure maintenance cost in pipeline mode.** Do not delete them (legacy users), but freeze them: no new features, no docs weight, hidden from the curated surface when Pipeline is detected. Engineering time goes to the ~40 (b) and the composites below.

Quality debt found in (b)/(c) while reading source:
- `GroupCompileErrorsCommand` scrapes console text for `"error CS"` and splits on `(` — misses warnings-as-errors, analyzer IDs, errors pushed out of the log window, and loses line/col. Should subscribe to `CompilationPipeline.assemblyCompilationFinished` (gives `CompilerMessage[]` with file/line/column/type) and persist the last result via `SessionState` across the reload.
- `run_tests` uses reflection on `TestRunnerApi` then the bridge polls for a new XML file every 1 s. Should use `TestRunnerApi.RegisterCallbacks(ICallbacks)` (RunFinished/TestFinished) and persist results; in pipeline mode delegate to Unity's `run_tests`/`test_status`.
- `click_object_in_game` fails for UI, for objects without a renderer centre on screen, and for projects using legacy Input Manager.

---

## 2. Composite tool designs (12)

Conventions for all: one Editor-side job, one main-thread entry, event-driven completion, compact result by default (`detail: "summary"|"normal"|"full"`), heavy payloads returned as **handles** (`cap://…`, `log://…`, `snap://…`) retrievable via one `nexus_get(handle, range)`. Each is exposed as `[CliCommand]` + MCP tool. Token numbers are rough, assuming ~300–800 tokens per raw call round-trip (call + JSON result) and ~1.5k tokens per 1600×900 JPEG image.

### 2.1 `nexus_apply_code` — write → compile → grouped diagnostics (compile-fix loop)
- **Combines:** file writes (sandboxed) or "files already edited by agent's own editor" mode; `AssetDatabase.Refresh`; `CompilationPipeline.compilationStarted/assemblyCompilationFinished`; `AssemblyReloadEvents`; Nexus grouping + dedupe + "likely root cause first"; optional `symbol_index` lookup for the unknown identifier (CS0246/CS1061 → suggest the actual type/member names).
- **In:** `{"files":[{"path","content"}]?, "touch_only":false, "timeout_s":120, "detail":"summary"}`
- **Out:** `{"status":"compiled|errors|no_change|timeout","ms":7900,"reload":true,"errors":[{"file":"Assets/X.cs","line":42,"col":7,"id":"CS1061","msg":"…","hint":"did you mean 'Health.Current'?"}],"more":0,"warnings_count":12}`
- **Replaces:** write ×N + refresh + wait (3–10 polls) + read_logs(200) + manual filtering = 6–15 calls, and the 200-line log dump (~4–8k tokens) → **1 call, ~200–600 tokens**.
- **Speed:** no fixed sleeps; completion signalled by the compile event (persist "job id → result" in `SessionState`/`Library/` file so the reply survives the domain reload; the client re-polls once after reconnection). `no_change` returns in <50 ms when nothing needs compiling (kills the 19–20 s false wait).
- Pipeline mode: call Unity `recompile` + `recompile_status`, keep Nexus grouping/hints.

### 2.2 `nexus_verify` — declarative QA scenario (flagship)
- **Combines:** Enter Play Mode (with `EnterPlayModeOptions` when safe), scene load, Nexus input (Input System `QueueStateEvent` + keyboard + UGUI `EventSystem.RaycastAll`/`ExecuteEvents` + legacy fallback), frame-accurate waits (`step_frame`, `WaitForFrames`, predicate on component property), Capture V2, console cursor, asserts, exit + restore.
- **In:**
```json
{"scene":"Assets/Scenes/Main.unity","steps":[
 {"wait":{"frames":30}},
 {"click":{"ui":"Canvas/StartButton"}},
 {"wait":{"until":"GameManager.State == 'Playing'","timeout_s":5}},
 {"key":{"press":"Space","hold_ms":100}},
 {"assert":{"prop":"Player/Health.current",">":0}},
 {"assert":{"no_errors":true}},
 {"capture":{"as":"after_jump","max_dim":960}}],
 "on_fail":"capture"}
```
- **Out:** `{"pass":false,"failed_step":4,"reason":"Player/Health.current = 0","errors":[{"msg":"NullReferenceException …","at":"PlayerController.cs:88","count":3}],"captures":["cap://after_jump"],"frames":212,"ms":4100}`
- **Replaces:** play + wait + find + click + wait + read property + read_logs + screenshot + stop = 10–20 calls and 1–3 images → **1 call + optional 1 image fetch**. Token saving 5–15×; wall time saving mostly from zero agent think-time between steps (each agent turn is seconds, not ms).
- **Speed:** runs entirely in `EditorApplication.update`/player loop; predicates checked per frame (no 1 s polling); captures only on assert failure or explicit step; the whole scenario is a persisted job so a reload mid-run returns a structured `interrupted_by_reload`, not a timeout.
- Scenarios saved as `Assets/NexusScenarios/*.json` become **regression tests** (runnable later via `nexus_verify --file` and from CI through `unity command`). This is the moat: a replayable agent QA artefact, not a one-off.

### 2.3 `nexus_context` — task-scoped context pack
- **Combines:** `project_map` (currently thin: build scenes, colour space, RP) + `symbol_index` + asset `get_dependencies` + `find_references` + `compact_scene_snapshot` + open scene/selection + last compile/test status + package list.
- **In:** `{"query":"player jump","budget_tokens":1500,"include":["scripts","scenes","prefabs"]}`
- **Out:** ranked list of files/types/objects with 1-line role each, entry points, and handles for deep reads; plus `"project_fingerprint"` for cache validation.
- **Replaces:** agent's 10–30 `rg`/`sed`/list calls at session start (what the 4 AI sessions actually did). The value proposition must be *better than rg*: include Unity-only knowledge rg cannot see — which scripts are actually attached in which scenes/prefabs, serialized references, which scenes are in the build, missing references.
- **Speed:** cached index in `Library/Nexus/index.*`, invalidated by `AssetPostprocessor.OnPostprocessAllAssets` + compile finished; budget-aware truncation.

### 2.4 `nexus_ui_check` — UI verification
- **Combines:** Capture V2 (overlay-correct), UGUI hierarchy walk (`Canvas`, `RectTransform.GetWorldCorners`, `Graphic.raycastTarget`, `CanvasGroup` alpha/interactable), UI Toolkit (`panel.Pick`, `worldBound`), TMP overflow checks (`TMP_Text.isTextOverflowing`), multi-resolution via GameView size switching.
- **In:** `{"resolutions":["1920x1080","1080x1920","2340x1080"],"checks":["offscreen","overlap","overflow","unclickable"],"capture":"on_issue"}`
- **Out:** `{"issues":[{"res":"1080x1920","kind":"overflow","path":"Canvas/Shop/Title","detail":"text overflows by 38px"}],"captures":["cap://1080x1920"]}`
- **Replaces:** per-resolution screenshot + agent visual inspection (3 images ≈ 4.5k tokens + unreliable VLM judgement) → structured findings, images only for failures.

### 2.5 `nexus_scene_diff` — what changed
- **Combines:** `ObjectChangeEvents` buffer (`scene_delta`) + scene YAML git diff resolved to object paths (fileID → hierarchy path) + prefab override diffs.
- **In:** `{"since":"generation:1234"|"git:HEAD"|"snapshot:snap://a","scope":"active_scene"}`
- **Out:** `{"added":["Enemies/Goblin (3)"],"removed":[],"modified":[{"path":"Player","component":"Rigidbody","props":{"mass":[1,2.5]}}],"generation":1301}`
- **Replaces:** raw YAML diffs the agents read in AI report 3 (thousands of tokens of fileIDs). High value for review and for "did my mutation do what I meant".

### 2.6 `nexus_mutate` — transactional batch with verify
- **Combines:** Unity primitives (create/set/add component — via Pipeline or legacy), single `Undo` group (`Undo.IncrementCurrentGroup`/`CollapseUndoOperations`), post-conditions, delta out.
- **In:** `{"ops":[…],"assert_after":[…],"rollback_on_fail":true}` → **Out:** delta (as 2.5) + failures.
- **Replaces:** N mutation calls + N re-reads. Rollback makes agents braver and faster.

### 2.7 `nexus_test` — tests with verdict
- **Combines:** Unity `run_tests`/`test_status` (pipeline) or `TestRunnerApi` callbacks (legacy); groups failures by assertion message and top user-code frame; maps to source lines.
- **Out:** `{"passed":120,"failed":2,"skipped":0,"ms":9400,"failures":[{"test":"…","msg":"Expected 3 but was 2","at":"InventoryTests.cs:55"}]}`
- Explicitly reports `"no_tests_found"` (benchmark C10 "passed" with 0 tests).

### 2.8 `nexus_perf` — perf triage
- **Combines:** `Unity.Profiling.ProfilerRecorder` (Main Thread time, GC Allocated In Frame, Draw Calls/Batches/SetPass, Triangles, System Used Memory), `FrameTimingManager`, optional Editor-side `ProfilerDriver` + `HierarchyFrameDataView` to extract top-N self-time markers for the worst frames.
- **In:** `{"frames":300,"during":"scenario:Assets/NexusScenarios/boss.json","top":10}`
- **Out:** `{"p50_ms":8.1,"p95_ms":21.4,"gc_kb_per_frame_p95":38,"spikes":[{"frame":187,"ms":41,"top":[["PlayerController.Update",12.3],["GC.Collect",9.8]]}],"draw_calls_p95":412}`
- Competitor comparison: Ivan's `profiler-capture-frame` reads `Time` fields only (tessl registry page), so a real marker-level triage is differentiating. Whether Pipeline exposes deep profiler data: **[UNVERIFIED]**.

### 2.9 `nexus_diagnose` — "why is it broken right now"
- **Combines:** editor state, compile status, console errors grouped with dedupe + counts (cursor-based), missing references, lint (NexusQualityGate), last test failures, editor timeline tail.
- **Out:** ≤15 lines prioritized. The first call an agent should make after "it doesn't work". Replaces 5–7 calls.

### 2.10 `nexus_look` — targeted visual
- **Combines:** Capture V2 + optional crop to object/UI bounds (`WorldToScreenPoint` of renderer bounds / `RectTransform` corners) + downscale + JPEG q70 + optional grid/label overlay of clickable UI (numbered boxes = "set-of-marks") so the model can say `click #7`.
- Replaces full-frame PNG (~1.5–3k tokens) with a crop at ~300–600 tokens; set-of-marks makes click targeting reliable without coordinates guessing.

### 2.11 `nexus_player_check` — runtime-build live check
- **Combines:** Unity `build` + `build_status`, Pipeline runtime connection into development players (Unity advertises runtime connection/hot reload; `[CliCommand(RuntimeOnly=…)]` exists per `integration-advanced.md`), plus a small Nexus runtime assembly (in `Runtime/`) that registers `nexus_verify` step handlers and capture (`ScreenCapture.CaptureScreenshotIntoRenderTexture` + `AsyncGPUReadback`) inside the player.
- Same scenario JSON as 2.2 runs against Editor or player → "works in editor, broken in build" becomes one call. Mechanics of player-side command registration in Pipeline: **[UNVERIFIED — needs a spike]**.

### 2.12 `nexus_get` / `nexus_expand` — handle resolver
- Every composite returns handles; `nexus_get("log://job42", {"range":[0,20]})`, `nexus_get("cap://after_jump",{"crop":"Player","max_dim":512})`. Keeps first responses tiny and makes detail pay-per-use.

---

## 3. Platform-level speed engineering

1. **Wake-up.** Nexus queues to `MCPServer.Enqueue` (`MCPServer.Logs.cs:142`) and drains on `EditorApplication.update` (`MCPServer.cs` `HandleMainThreadQueue`); nothing wakes the loop, so unfocused Editors show ~100 ms quantization. Options, in order:
   - Pipeline mode: rely on Pipeline's own dispatch and use `set_autotick` during an active job (per shared context; exact semantics **[UNVERIFIED]**).
   - Legacy: while any Nexus job/connection is active, switch Editor "Interaction Mode" to *No Throttling* (Preferences › General; stored in EditorPrefs — key names **[UNVERIFIED]**, verify on 6000.x) and restore after an idle timeout ("burst mode"). Measure: target p50 < 10 ms for read calls with Editor unfocused.
   - Windows: post a no-op message to the Editor main window from the listener thread to break the message wait; macOS: existing `AppNapBypass` plus a CFRunLoop wake **[UNVERIFIED effect]**.
   - Do NOT call `EditorApplication.QueuePlayerLoopUpdate` from the listener thread (main-thread API; it is currently only called from main-thread code in `Capture/`, which is correct).
2. **Warm process.** Recommend agents use `unity shell --protocol ndjson` (documented "boots the CLI once and runs many commands in the same warm process") or persistent `unity mcp` rather than cold `unity command` per call; a cold CLI spawn costs process start + discovery each time. Our skill should say so explicitly and measure the delta.
3. **Long-running job model.** One contract for compile, tests, verify, build, perf: `{"job":"j42","state":"running","progress":0.4}`, completion via long-poll (`nexus_job_wait j42 --timeout 60`) that returns the instant the job ends. Persist job state in `SessionState` + `Library/Nexus/jobs/` so it survives domain reload. Replaces bridge polling loops (`time.sleep(1.0)`).
4. **Event-driven waits.** `compilationStarted/Finished`, `AssemblyReloadEvents.after`, `EditorApplication.playModeStateChanged`, `AssetDatabase` import completion via `AssetPostprocessor`, `TestRunnerApi` callbacks. No fixed sleeps anywhere in the bridge.
5. **Avoid domain reloads.** `EditorSettings.enterPlayModeOptionsEnabled = true` with `EnterPlayModeOptions.DisableDomainReload | DisableSceneReload` for `nexus_verify` runs *only if* the project passes a static-state lint (Nexus can detect `static` mutable fields without `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` reset — a genuinely useful lint rule). Play entry drops from seconds to sub-second. Also batch code edits into one `nexus_apply_code` so one reload covers N files; `AssetDatabase.StartAssetEditing/StopAssetEditing` around multi-file writes.
6. **Caching with invalidation.** Project map / symbol index / reference graph cached in `Library/Nexus/`, keyed by (asset GUID, import hash via `AssetDatabase.GetAssetDependencyHash`), invalidated by postprocessor + compile events; responses carry `fingerprint` so the agent can skip re-reading unchanged context.
7. **Single main-thread hop per composite** and batch reads (one `FindObjectsByType` pass, not per-object calls).
8. **Capture**: keep 1600×900 JPEG profile; add crop-before-encode (encode fewer pixels = less main-thread encode time, which is still on main thread per plan §1.2).

---

## 4. Token engineering

- **Default result shape = verdict + counts + top-k + handles.** Never return full lists by default; `"more": N` + handle.
- **Detail levels** on every composite: `summary` (≤300 tokens), `normal`, `full`.
- **Stable short references**: hierarchy paths or short ids (`#g12`) valid per session generation instead of repeated instance IDs + full component dumps.
- **Deltas**: `since_generation` / console cursor / fingerprint on all read tools — the second read of anything should cost near zero.
- **Dedupe logs**: group identical messages with counts and first user-code frame; strip Unity internal frames.
- **Images**: JPEG q60–75, max_dim 768–1024 by default, crop to target, set-of-marks overlay for UI; only capture on failure in verify; return image once, then refer by handle.
- **Tool-surface size strategy:**
  - Curated MCP surface of **~10–12 composites** (the list above) + `nexus_get` + an escape hatch `nexus_call(method, params)` for the 121 raw methods, discoverable through `nexus_list(query)` — i.e., deferred loading done server-side. Schema budget target < 4k tokens total (Coplay ~29k per benchmark notes).
  - Profiles (`core`, `qa`, `ui`, `perf`) already exist conceptually in `NexusCommandDescriptor.Profiles`; wire them to the MCP surface.
  - **Skills first**: ship `nexus-unity` skill(s) that teach *when* (after C# edits → `nexus_apply_code`; before claiming done → `nexus_verify`; "it's broken" → `nexus_diagnose`) with CLI invocations. Skills cost zero schema tokens until triggered — this directly addresses the "agents never called Nexus" evidence. Install via our installer and, if possible, alongside `unity skill install`.

---

## 5. Verification of Pipeline capabilities (sources)

| Capability | Finding | Status |
|---|---|---|
| Keyboard/mouse input simulation in Play Mode | Official `unity-cli` SKILL.md contains no input/UI-click content; references folder has 9 files (auth, build-run-test, collaboration, config-hub, diagnostics, editors-install, integration-advanced, projects-templates, version-control) — none about input. Web hits for "input simulation" are third-party (hatayama unity-cli-loop, akiojin unity-cli). | **Not found in official material** — treat as absent; [UNVERIFIED] for `unity command` live list |
| UI automation | Same as above; Unity separately ships `com.unity.ui.test-framework` (6.3) that simulates clicks/keyboard for **UI Toolkit** panels — a candidate primitive for Nexus's UI Toolkit automation. | Pipeline: not found. UITK test framework: exists |
| Capture | `integration-advanced.md`: "`unity mcp` … Supports `capture_game_view` and `capture_scene_view` with fallback to OS screenshots when unresponsive." Overlay-canvas inclusion not stated; Nexus's own tests say official camera capture misses Screen Space Overlay. | Capture exists; overlay-correctness **[UNVERIFIED/negative per Nexus tests]** |
| Profiler | Not in official skill docs; shared context lists "profiler" in built-ins. | **[UNVERIFIED]** |
| Warm agent protocol | `unity shell --protocol ndjson` "boots the CLI once and runs many commands in the same warm process"; `unity command --query/--tag/--detail compact/--limit`; `[CliCommand]` has `MainThreadRequired` (default true) and `RuntimeOnly`. | Verified (GitHub) |
| Pipeline 0.7.0-exp.1 | Release focus: console command dependable across reloads/restarts/play transitions; wait command; runtime registration. | Verified via search snippet (forum page blocked) |

Sources:
- https://github.com/Unity-Technologies/skills (skills list, 34 dirs incl. `unity-cli`, `ui-ugui`, `ui-uitk`, `project-auditor-fixes`)
- https://raw.githubusercontent.com/Unity-Technologies/skills/main/skills/unity-cli/SKILL.md
- https://raw.githubusercontent.com/Unity-Technologies/skills/main/skills/unity-cli/references/integration-advanced.md
- https://raw.githubusercontent.com/Unity-Technologies/skills/main/skills/unity-cli/CHANGELOG.md
- https://discussions.unity.com/t/unity-pipeline-package-0-7-0-exp-1-is-available-now/1736536 (search snippet only)
- https://docs.unity3d.com/Packages/com.unity.ui.test-framework@6.3/manual/simulate/simulate-ui-interaction-landing.html
- https://tessl.io/registry/skills/github/IvanMurzak/Unity-MCP/profiler-capture-frame (Ivan profiler = Time fields snapshot)
- https://www.sourcepulse.org/projects/27120693 (hatayama unity-cli-loop, third-party input sim)

---

## 6. Recommended order (two-person team)

1. **Week 1 — stop the bleeding:** remove fixed sleeps/20 s false wait in `routes_editor.py`; event-driven compile wait; `CompilationPipeline`-based compile diagnostics; "burst mode" wake-up; measure unfocused p50.
2. **Weeks 2–3:** `nexus_apply_code`, `nexus_diagnose`, `nexus_get` handles, curated surface + escape hatch; `nexus-unity` skill with CLI examples. Re-run the 4 real-session style tasks and count Nexus calls (adoption is the KPI, not ms).
3. **Weeks 3–6:** `nexus_verify` v1 (Editor, UGUI + Input System + keyboard, asserts, capture-on-fail, scenario files), `nexus_look` with crop + set-of-marks.
4. **Then:** `nexus_ui_check`, `nexus_scene_diff`, `nexus_perf`, `nexus_player_check` (after a Pipeline runtime spike).
5. **Benchmarks must score task success** (assert-based), not "no JSON-RPC error".
