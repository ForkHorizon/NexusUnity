# Changelog - Nexus Unity

All notable public changes to Nexus Unity are documented here.

## [Unreleased]

### Changed
- Relicensed Nexus Unity from `GPL-3.0-only` to the `MIT` license to remove copyleft friction for commercial Unity studios. All prior contributors consented to the relicense.

### Fixed
- `add_component` now returns a clear `GameObject not found` error for stale instance IDs instead of throwing a raw Unity null reference.
- `batch_execute` now caps batches at 50 requests and rejects nested batch execution.
- `create_primitive` now validates parent, transform, and material inputs before creating the GameObject, and Vector3 inputs accept `[x, y, z]` arrays as well as `{x, y, z}` objects.
- `get_game_object` now returns transform state and a compact component list so agents can verify basic write operations with the cheap read-back call.
- Script writes now keep readiness probes in a busy/importing state while the scheduled asset refresh is pending, avoiding premature follow-up write calls during Unity domain reload.
- Script-writing RPC methods now require `confirm: true` before creating or overwriting `.cs` files and triggering Unity compilation.
- MCP bridge static resources can now be read through `resources/read` after discovery through `resources/list`.
- Play mode transitions now keep readiness probes busy so agents do not issue follow-up writes while Unity is still entering or exiting Play Mode.
- `delete_player_pref` now rejects missing or empty keys, and only clears all PlayerPrefs when called with `key: "all"` and `confirm: true`.
- Invalid `NEXUS_UNITY_TIMEOUT_SECONDS` values now fall back to the default bridge timeout instead of crashing the Python bridge at import time.
- Python bridge type helpers no longer require Python 3.11-only typing features.
- Path security tests now exercise JSON-RPC traversal rejection for representative file and asset tools.
- `get_test_results` now reads messages only from scoped NUnit result nodes and falls back to the test result instead of unrelated nested messages.
- `unity_scene_manager` schema aliases now preserve action-specific required parameters.
- Fast-path health JSON-RPC methods now use cached editor state instead of direct Unity API reads on the listener thread.

## [1.4.2] - 2026-06-13

### Added
- Added Python unit coverage for MCP bridge schema shape and type expectations.

### Changed
- Expanded the `unity_hierarchy_manager` MCP schema into per-action shapes with clearer required parameters and aliases.
- Refined Python MCP bridge routing with shared JSON-RPC payload types and more structured handler code.
- Added a repository mailmap entry for `air17` so local Git contributor reports resolve historical `air17@github.com` commits to the GitHub account's canonical no-reply identity.

### Fixed
- Removed direct Unity Project Auditor package dependencies and made Unity Project Auditor execution optional, so clean installs avoid duplicate immutable `.meta` GUID warnings and `Could not find any registered modules` Console spam.
- Fixed bridge-side compilation wait timeout handling after asset refresh and tightened test-result polling payload handling.

## [1.4.1] - 2026-06-12

### Fixed
- Added the missing folder `.meta` files for `docs/`, `docs/assets/`, and `Editor/tests/`. These folders were published without their folder-level metas, so a fresh package install logged "no meta file ... will be ignored" warnings and skipped the folders.
- Hardened the pre-push `.meta` pairing check to validate the git-tracked tree instead of the on-disk working directory (Unity auto-generates metas locally, which masked the missing committed metas) and to require a tracked `.meta` for every imported folder, not just files.

## [1.4.0] - 2026-06-11

### Added
- Claude Code integration card that registers `nexus-unity` in a project-root `.mcp.json`, using the `claude` CLI at project scope when available and falling back to writing the file directly. This is separate from the existing Claude Desktop card.
- Dedicated Python bridge unit tests for routing, compilation/test-result polling helpers, transport config, and JSON-RPC payload construction.
- `Python unit tests` GitHub Actions job that runs the bridge unit test suite after static validation.
- Maintainer-dispatched external PR replay workflow that can copy a reviewed fork PR to a trusted `trusted/pr-N` branch for CI reruns.

### Changed
- Python MCP bridge internals now split logging and HTTP transport helpers into `nexus_bridge/_logging.py` and `nexus_bridge/_transport.py`, with stronger type hints, module documentation, and reusable transport configuration.
- Bridge CLI mode now uses `argparse`, validates `key=value` arguments, and parses JSON-looking values before routing direct tool calls.
- MCP bridge tool schemas now include clearer per-action descriptions and static resource metadata for `resources/list`.
- `Validate package` now warns for external fork pull requests and continues the CI pipeline instead of blocking before checkout; maintainers can still use the trusted-branch replay workflow when they want an internal CI branch.
- Local static/pre-push validation now runs the Python bridge unit test suite after compiling the bridge.
- The bridge log level is configurable with `NEXUS_UNITY_LOG_LEVEL`.

### Fixed
- Claude Desktop auto setup on macOS now writes `~/Library/Application Support/Claude/claude_desktop_config.json` instead of the XDG `~/.config/Claude` path, which the macOS app does not read (`SpecialFolder.ApplicationData` resolves to `~/.config` under Mono).
- CLI client detection (Codex, Gemini, Antigravity, Claude Code) now resolves executables with `where` on Windows instead of always failing, which previously left Gemini and Antigravity `Auto Setup` disabled on Windows even when installed.
- Generated MCP configs resolve the Python interpreter as `python3`, then `python`, then the Windows `py` launcher, instead of hardcoding `python3`, so the bridge launches on Windows hosts without a `python3` alias.
- Invalid `NEXUS_UNITY_PORT` values are ignored cleanly so the bridge can fall back to a positional port argument or the default port.

## [1.3.0] - 2026-06-05

### Added
- Integration cards now detect stale project-root MCP bridge deployments and client configs that point at a different Unity project.

### Changed
- The Server tab bridge summary now shows the deployed bridge version and flags package/deployed bridge mismatches.
- Integration setup success messages now explicitly tell users to restart the affected MCP client session after redeploying the bridge.

### Fixed
- Unity 6.3 editor compilation now works again by guarding Unity 6.4-only `EntityId` hierarchy event and serialized object-reference APIs behind 6.4+ fallbacks.

## [1.2.0] - 2026-05-31

### Added
- GitHub funding metadata now configures the repository Sponsor button for `Daliys`.
- MCP manager aliases for common raw-style action names such as `list_scenes`, `create_scene`, `create_gameobject`, `rename`, and tool-usage diagnostics.
- Scene-building manager support for primitive name, parent, transform, material path, object rename, transform updates, and `create_hierarchy` passthrough.
- Material creation support for visible base color and emission color fields.
- Local-Ollama pull request checklist review through `NexusQualityGate --checklist-ai`, with one AI verdict per checklist item and explicit next-action output for failures.

### Changed
- `Validate package` now runs entirely on the maintainer self-hosted Mac runner with the `nexus-unity-ci` label instead of GitHub-hosted runners.
- CI now uses workflow-level `concurrency.queue: max` so trusted runs queue on the local runner instead of being canceled or running in parallel.
- Public contribution docs now describe the trusted-branch replay policy for external fork pull requests before full local CI can execute candidate code.
- Unity package validation now performs a local package smoke import with Unity `6000.4.3f1` instead of the previous optional GitHub-hosted EditMode test job.
- Unity package smoke now generates a dedicated EditMode smoke test assembly in the temporary project so package resolution and editor bridge compilation are verified during CI.
- Manager invalid-action errors now include valid action names to make AI recovery deterministic.
- `create_scene` can create or reopen a scene directly at a provided asset path.
- `set_transform` now updates rotation and scale as well as position.
- The required `Documentation quality AI` CI job now validates both XML documentation quality and pull request checklist evidence while preserving the existing required status-check name.

### Fixed
- `write_file` and `write_files_batch` now create missing parent directories after path validation.

## [1.1.2] - 2026-05-23

### Fixed
- Fixed clean Unity Package Manager installs by moving package-internal Unity test sources into `Tests~/` so user projects do not compile NUnit-dependent test files during normal package import.
- Added the required `com.unity.inputsystem` package dependency for input simulation tools that compile against `UnityEngine.InputSystem`.

### Changed
- Packaging docs now treat all Unity-ignored `~` folders as no-`.meta` areas, including `tools~/` and `Tests~/`.
- Optional agent tooling smoke now validates live UI/tool routing directly instead of depending on package-internal Unity test sources.
- README release notes now direct users from `v1.1.0` and `v1.1.1` to the superseding `#v1.1.2` install pin.

## [1.1.1] - 2026-05-23

### Fixed
- Fixed Unity Package Manager installs by adding an auto-referenced `UnityMCP.Runtime` assembly definition for runtime APIs used by editor code.
- Removed Unity `.meta` files from the ignored `tools~/` validation tooling folder so immutable PackageCache imports no longer report orphan `tools~.meta` assets.

### Changed
- Package validation now ignores Unity-ignored `~` folders for `.meta` pairing and fails if `.meta` files under those folders are tracked again.
- README release notes now tell `v1.1.0` users to update pinned installs to `#v1.1.1` and clear the stale PackageCache entry if Unity keeps old import errors.

## [1.1.0] - 2026-05-23

### Added
- Roslyn-based `NexusQualityGate` for contributor CI, covering XML documentation quality, file size limits, method size warnings/failures, and local test coverage.
- Required self-hosted Ollama documentation review workflow using the `nexus-doc-ai` runner label and serialized GitHub Actions concurrency.
- GitHub issue templates, pull request template, static validation workflow, and code of conduct for public community maintenance.
- README badges and reproducible install URL pinned to `v1.1.0`.
- A single `Window > Nexus Unity` menu entry that opens the main Nexus Unity window.
- Tracked optional fast pre-push hook and installer for contributor local validation.
- Development versioning policy: keep unreleased development on the latest public package version and record user-visible work under `[Unreleased]` until release preparation.
- Public API stress-audit documentation covering raw `list_tools` validation, MCP bridge catalog validation, disposable mutation namespaces, and cleanup expectations.
- Contributor branch policy documentation: public pull requests target `development`; `main` is release-only.
- Pull request target policy workflow for enforcing contributor PRs to `development` and release PRs to `main`.
- UI Toolkit regression tests for the Nexus Unity editor windows.
- MCP integration config generator for Codex TOML, JSON-style MCP clients, VS Code-compatible `servers` config, Cursor, Windsurf, Claude Desktop, and generic copy/paste setup.
- Editor tests for integration config generation and responsive integration hub controls.
- Raw agent diagnostics: `get_test_results`, `get_tool_usage_stats`, `ui_get_window_rect`, and `ui_set_window_rect`.
- Bridge actions for `unity_editor_controller` test polling/server state/asset refresh and `unity_ui_automation` window rect/layout QA.
- Window snapshot diagnostics through raw `ui_capture_window_snapshot` and `unity_ui_automation` `capture_window_snapshot`.
- Scoped tool usage reset via raw `reset_tool_usage_stats`.
- Optional `scripts/agent-tooling-smoke.py` for focused local agent tooling verification.
- Console logging settings for Nexus Unity service messages, available in the main `Settings` tab and `Edit > Project Settings > Nexus Unity`.

### Changed
- Public XML documentation now describes the Unity Editor, filesystem, process, serialization, and UI side effects that the AI review gate expects on editor-facing APIs.
- The AI documentation review now uses Ollama JSON mode and a pragmatic rubric cache version so stale or over-strict model verdicts are not reused after rubric updates.
- AI documentation validation now passes a short Ollama keep-alive and explicitly unloads the local model after each review to avoid leaving large models resident between CI jobs.
- Static package validation now runs the deterministic Nexus quality gate and its local test harness.
- Contributor docs now define useful XML documentation expectations and local commands for deterministic and AI-backed documentation review.
- Consolidated API verification, project audit, test window, and Codex link test actions into the main Nexus Unity window instead of exposing separate Unity submenu entries.
- The package validation workflow now reuses the local static validator and includes an optional Unity EditMode test job when a Unity license secret is configured.
- The local pre-push hook now runs a sub-minute quick gate by default; full Python integration validation is opt-in with `scripts/prepush-validate.sh --integration`.
- Validation now runs on both `main` and `development` and documents that direct pushes to protected branches are blocked for everyone.
- Rebuilt Nexus Unity editor windows with compact UI Toolkit layouts, responsive wrapping, minimum usable bounds, and named automation elements.
- Reworked the main Nexus Unity window around user-facing `Server`, `Integrations`, and `Resources` tabs; test/internal actions now live under collapsed `Advanced / Diagnostics`.
- Integration setup is now card-based with status, auto setup, copy config, and config-location actions instead of a row of ambiguous CLI buttons.
- Wide editor layouts now keep server and bridge blocks stacked while stretching their internal action rows cleanly.
- Nexus Unity internal logs now default to important Console messages only, with `All` and `Custom` modes for verbose diagnostics.
- The quick pre-push validator now supports `NEXUS_UNITY_HOOK_LIVE=auto|required|off`, retries transient live-smoke failures, and prints validation timing.
- `unity_editor_controller` can run tests and wait for results through bridge-side polling instead of blocking the Unity main thread.
- `unity_ui_automation` now forwards `deep` hierarchy reads and `class_name` queries through the MCP bridge.
- The Python MCP bridge now supports `NEXUS_UNITY_URL`, `NEXUS_UNITY_PORT`, and `NEXUS_UNITY_TIMEOUT_SECONDS`, and avoids writing Python bytecode caches.
- Raw API schemas now document `update_component`'s preferred `properties` payload, keep its legacy `json_data` payload compatible, and expose `invoke_method.arguments` as a valid array schema.
- `create_material` now accepts an optional explicit asset `path`, allowing stress tests and automation to keep generated materials isolated.
- Public docs now identify `unity_write_and_compile` as the supported code-edit macro and treat old `unity_apply_code_change` wording as stale.

### Fixed
- `click_object_in_game` now honors `instance_id` as documented instead of only accepting hierarchy paths.
- Existing Codex and Claude Desktop config files are backed up before Nexus Unity updates their MCP server entries.

### Removed
- Removed orphan `Runtime/Tests.meta` file from the public package.
- Removed the default `runtime_trace.txt` diagnostic write from runtime log capture.

## [1.0.0] - 2026-05-18

### Added
- First public open source release for `com.forkhorizon.nexus.unity`.
- Raw HTTP JSON-RPC API with 111 Unity Editor tools discoverable through `list_tools`.
- Consolidated MCP bridge API with 14 `unity_` manager and macro tools for AI clients.
- Modular Python bridge package under `nexus_bridge/`.
- `unity_write_and_compile` macro for write, wait, and compiler-feedback workflows.
- Editor tests for path security, port behavior, manager routing, raw registry consistency, and bridge contract consistency.
- Release-readiness docs: `SECURITY.md`, `CONTRIBUTING.md`, and `RELEASE.md`.
- `GPL-3.0-only` license.

### Changed
- Public repository target is `https://github.com/ForkHorizon/NexusUnity.git`.
- Public documentation now treats Nexus Unity as a standalone open source package.
- The Unity installer deploys both `nexus_unity_bridge.py` and the required `nexus_bridge/` module to the project root.
- Package version, server version, bridge version, and public docs now use `1.0.0`.
- Package metadata now includes public repository, documentation, changelog, and license URLs.

### Removed
- Removed generated Graphify cache files from the package repo.
- Removed tracked `.jules/`, `.DS_Store`, Python bytecode, and local validation artifacts from public package contents.
- Experimental native bridge artifacts are not included in the public release.

## Pre-public History

Earlier internal builds used `2.x` and `3.x` version numbers while the package was being validated. Public semantic versioning starts at `1.0.0`.
