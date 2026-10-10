# Competitor teardown: akiojin/unity-cli (2026-10-04)

Primary subject: [akiojin/unity-cli](https://github.com/akiojin/unity-cli). Inspected tree: shallow clone of `main` at `7f45d7aec2a866abda88ba3768bf4057c5f23053` (2026-10-03 02:12 +0900, merge of PR #464). Cargo crate and UPM package both still declare **0.18.1**. Latest GitHub Release is **v0.18.1**, published 2026-10-02 (`SHA256SUMS` plus per-RID CLI and C# LSP binaries). This is not Coplay, not Funplay, and not the official Unity CLI.

Nexus is not this product. Akiojin’s own comparison page measures **unity-cli 0.17.0 development build** against **official Unity CLI 1.0.0-beta.11** and **Pipeline 0.8.0-exp.1** on 2026-10-02. The local Nexus note that official CLI **1.0.0-beta.10** is installed is a different binary. Do not read Akiojin’s latency tables as Nexus measurements.

## Lineage verdict

**CONFIRMED: uLoopMCP / hatayama/unity-cli-loop is a different project, not a predecessor of akiojin/unity-cli.**

| | Akiojin | hatayama |
| --- | --- | --- |
| Author | Akio Jinsenji (`akiojin`, Cloud Creative Studios) | hatayama (`m_hatayama`) |
| Current repo | `akiojin/unity-cli` | `hatayama/unity-cli-loop` |
| Own predecessor | `akiojin/unity-mcp-server`, README banner **DEPRECATED**, successor named as `akiojin/unity-cli` | Same product, renamed. uLoopMCP became Unity CLI Loop at 1.0.0 (2026-03). GitHub `hatayama/uLoopMCP` redirects. UPM id is still `io.github.hatayama.uloopmcp` |
| Cross-mentions in the inspected trees | None for hatayama / uloop / uLoopMCP | None for akiojin |

They converged on the same industry move (Node MCP out, native CLI plus skills in) independently. Cover hatayama only in [Sibling, not lineage](#sibling-not-lineage-hatayamaunity-cli-loop).

Akiojin’s own predecessor is **DOCUMENTED** on the deprecated repo: Node.js MCP (`npx @akiojin/unity-mcp-server`), Unity 2020.3+, TCP bridge already on the Editor side. Latest release observed via search index: **v5.5.2** (2026-02-26). Not re-cloned. `docs/migration-notes.md` records the redesign motives: drop the Node runtime, ship one binary, drop MCP-as-the-protocol. Parts of that note are stale; see [Docs that disagree with source](#docs-that-disagree-with-source).

## How to read labels

- **CONFIRMED**: read in the 2026-10-03 `main` tree, or in a GitHub API response from this session.
- **DOCUMENTED CLAIM**: the project states it (README, docs, release notes) and this pass did not re-execute the Editor matrix or the benchmark.
- **INFERRED**: follows from the code, not spelled out as a guarantee.
- **UNKNOWN**: not in the inspected tree, or only in a stale doc.

Nexus facts below are the stated baseline (UPM `com.forkhorizon.nexus.unity` 1.7.0, `unity: 6000.0`, optional Pipeline plus legacy HTTP MCP, three canonical commands, one envelope, Overlay JPEG look, readiness epochs). They were not re-benchmarked. Architecture is unresolved. This note does not recommend a Nexus-primary router.

## Architecture

**CONFIRMED** three processes, from `docs/architecture.md` and the tree:

```text
Agent
  -> skills (on demand) and/or `unity-cli` / `unity-cli mcp`
  -> Rust CLI (src/)  --optional-->  C# LSP (lsp/, .NET 10, child process, stdio LSP)
  -> TCP 127.0.0.1:6400
  -> Unity Editor + UPM com.akiojin.unity-cli-bridge
```

| Piece | Role |
| --- | --- |
| `src/` Rust 2021, clap, tokio | CLI, tool catalog, local tools, MCP stdio adapter, `unityd` connection pool, LSP supervisor, skill linter, UnityCsReference cache |
| `UnityCliBridge/Packages/unity-cli-bridge/` | Editor TCP server, command handlers, lockfile, auth. 144 `.cs` files, 58 under `Tests/`. Namespace `UnityCliBridge` |
| `lsp/` | Bundled C# language server. Separate release artifacts `csharp-lsp-*` |

`unityd` (`src/daemon/unityd.rs`) keeps a TCP pool. A peer close after Editor restart or domain reload drops that socket and reconnects. **CONFIRMED** eval deliberately bypasses the daemon and uses a direct TCP call (`docs/editor-eval.md`). Local C# reads do not open the Editor.

Constitution v2.2.0 (`docs/constitution.md`, last revised **2026-03-13**) still says: CLI is primary, one UPM bridge, do not go back to an MCP-specific implementation, Rust only, no performance regression, TDD, LSP-centered C# edits that fail closed, `UNITY_CLI_*` only. The October tree violates the letter of “no MCP implementation” by shipping a thin adapter, and it violates “LSP-centered” as a default: `UNITY_CLI_LSP_MODE` defaults to **`off`**.

## Move away from a giant MCP surface

This is the useful counterexample, and it is only a partial one.

**CONFIRMED** the agent-primary path is no longer “put every Unity operation in `tools/list` and hope.” README flow is `Claude Code -> Skills (on demand) -> unity-cli -> TCP`. `unity-cli-usage` tells the agent to discover with `tool list --query <term> --compact` (and `--category`, `--limit`) and to call `tool schema <name>` for one payload. `raw <tool>` and `tool call <tool>` are documented as the **primary** invocation for tools that have no typed subcommand, not as a fallback (skill text and the v0.11.5 changelog).

**CONFIRMED** the catalog underneath is still giant. `src/tooling/tool_catalog.rs` `TOOL_NAMES` has **154** unique names on this tree: **30 local**, **124 remote**. `unity-cli mcp` (`src/mcp/mod.rs`) is a stdio JSON-RPC adapter over that same catalog. `tools/list` returns the whole published catalog and **rejects a pagination cursor**. It speaks protocol versions `2024-11-05`, `2025-03-26`, `2025-06-18`, `2025-11-25`. `tools/call` returns the CLI envelope as MCP text and sets `isError` on failure. Every 2 seconds, if the Editor or custom-tool set changes, it emits `notifications/tools/list_changed`.

So the redesign hid the catalog behind skills and keyword lookup. It did not delete it, and any MCP client that calls `tools/list` is back on a 150-tool surface. Project tools marked `[UnityCliBridge.Tools.UnityCliTool]` join the same list.

Published counts disagree: README says **101** Unity Tool APIs and **14** skills; architecture says **129** registered tools; comparison page (0.17.0) says **153** entries; Editor matrix says all-tools **121/121, 133 calls** (exclusions not counted as passes). The source count on this commit is 154. Treat README and architecture counts as stale.

## Native CLI

**CONFIRMED** one binary, `unity-cli`, clap tree in `src/cli.rs`. Typed groups include `setup`, `doctor`, `bridge`, `system`, `scene`, `instances`, `editor`, `test`, `tool`, `raw`, `batch`, `lsp` / `lspd`, `unityd`, `skills`, `reference`, `cli`, and `mcp`. Most bridge operations are not typed subcommands. Global flags: `--output text|json`, `--host`, `--port`, `--project-path`, `--timeout-ms`, `--dry-run`, `--verbose`.

Success and failure share one stdout envelope, **DOCUMENTED** in `unity-cli-usage`: `{success, command, data, errors:[{code,message}], warnings}`. Exit codes are part of the contract (0 success, 2 invalid argument, 3 unauthorized, 6 operation/timeout, 7 unreachable, 8 tests failed). A timeout does not prove the mutation did not run; the skill says to read the job or request id before retrying.

`--dry-run` skips mutating tool calls, including eval.

## Skill-based progressive disclosure

**CONFIRMED** canonical skills live in `.claude-plugin/plugins/unity-cli/skills/`. This tree has **23** `unity-*` skills, not the 14 named in the root README. `docs/skills.md` matches the 23. `dev-skills/gh-skills-sync` is excluded from the plugin.

Contract (Skill Contract v1, issue #160, enforced by `unity-cli skills lint` and `src/skills/rules.rs`):

- Description hard cap 1024, at least one trigger token in the first 250 characters, third person, a `Use when` clause, a `Do not use` / `Not for` clause, and a sibling named in the description.
- Body ≤ 500 lines. Required H2 order: Use When, Do Not Use When, Preferred Flow, Examples, References.
- References are one level deep. Files over 100 lines need a table of contents.
- No time-sensitive wording (`as of`, `currently`, …).
- `allowed-tools` is `Bash(unity-cli:*), Read, Grep, Glob`. Only `unity-csharp-edit` may add `Edit, Write`. Only `unity-cli-usage` may set `user-invocable: false`.
- Every skill carries `references/runtime-checklist.md`.

The CLI embeds those directories at build time. `unity-cli skills install|refresh|show` writes them for Claude Code, Cursor, Windsurf, and VS Code, with a `.unity-cli-skills.json` hash ledger. Refresh refuses locally edited owned files. Symlinks under the destination are refused. Claude Code and Codex plugins share one source tree via symlinks.

**INFERRED** progressive disclosure is the host’s skill router plus this contract, not a feature of the TCP protocol. The body and references stay out of context until a skill triggers. The MCP adapter does not do this; it publishes the catalog.

## TCP / bridge

**CONFIRMED** `src/unity/transport.rs`:

- Connect to `UNITY_CLI_HOST`:`UNITY_CLI_PORT`, default `127.0.0.1:6400`.
- Frame is a **4-byte big-endian length** plus UTF-8 JSON. Max frame **10 MiB**.
- Request shape: `{id, type: <tool name>, params, authToken?}`.
- Idle pooled sockets are probed with `MSG_PEEK` so a dead Editor is not sent a second copy of a request.
- There is a fallback that accepts unframed JSON if the header is not a plausible length. **INFERRED** that exists for tests and old debug peers, not as the production frame.

**CONFIRMED** auth (`UnityCliBridge/.../BridgeAuthentication.cs`, `EditorLockfile.cs`, `src/unity/auth.rs`, architecture doc):

- 32 random bytes, base64, created once per Editor domain (`static readonly`).
- Written into `~/.unity-cli/editors/<pid>.json` with POSIX **0600** (`fchmod` 384) before the secret is considered published.
- Compare is length-check plus XOR. A wrong token is never treated as the unauthenticated opt-out.
- CLI reads the token at connect time from the unique live lockfile for that host:port, or from `UNITY_CLI_AUTH_TOKEN_FILE`. Listings are not supposed to print the token.
- `UNITY_CLI_ALLOW_UNAUTHENTICATED=1` still allows a **missing** token. The CLI prints that this opt-out is deprecated and will be removed next minor. It is still present at 0.18.1. Mismatched tokens are always rejected.
- Non-loopback bind warns on the Editor console. The doc says TCP is plaintext and this is **not** isolation from other processes of the same OS user.

Domain reload: `MarkReloading()` keeps the lockfile and only patches `state` to `reloading` (old token remains in that file). After the domain comes back, static init creates a **new** token and `Publish` rewrites the file. **INFERRED** a client that cached the pre-reload token must re-read the lockfile; `token_for` does that on each connect. `unityd` drops a socket the Editor closed.

Multiple Editors are first-class: `instances list` / `set-active`, `--project-path`, and exit 6 `AMBIGUOUS_EDITOR`. `doctor` classifies `SAFE_MODE`, `BRIDGE_NOT_INSTALLED`, `PORT_IN_USE`, `EDITOR_NOT_RUNNING`, `SANDBOX_BLOCKED`, and surfaces `file(line,col): error CSxxxx` from `Editor.log`.

## Hot reload

**CONFIRMED** this is an optional preview, not a core dependency. `docs/hot-reload.md`:

- Tool pair `hot_reload_status` / `hot_reload` with actions `begin`, `apply`, `recover`.
- Backend is **Fast Script Reload 1.8.0**, git pin `51140b71…`, package `com.handzlikchris.fastscriptreload`. The bridge does not install it. Missing backend returns `HOT_RELOAD_PACKAGE_MISSING`.
- Supported hosts: **Mono Editor, Unity 2022.3+, x64 or Apple Silicon macOS**. Real method replacement is **DOCUMENTED** as verified on 6000.3.25f1 and 2022.3.62f3, native ARM64 and x64 under Rosetta. Windows and Linux are issue #386, not claimed.
- Scope is one source file, one non-generic non-partial class, existing synchronous block-bodied methods. Fields and declarations must stay identical. No auto-rollback. `recover` stops Play and asks for a clean compile / domain reload. Preview source is not saved.
- The matrix fixture expects `HOT_RELOAD_PACKAGE_MISSING` unless `--fsr-path` is passed. Do not read the 2026-09-30 PASS table as “hot reload worked on eight Editors.”

## Unity-independent code tooling

**CONFIRMED** 30 catalog entries never need the Editor. They split into:

- File/search: `read`, `search`, `list_packages`.
- LSP index and edits: `build_index`, `update_index`, `get_index_status`, `get_symbols`, `find_symbol`, `find_refs`, `rename_symbol`, `replace_symbol_body`, `insert_before_symbol`, `insert_after_symbol`, `remove_symbol`, `validate_text_edits`, `write_csharp_file`, `create_csharp_file`, `apply_csharp_edits`, `create_class`.
- Official source cache: `reference_fetch`, `reference_status`, `reference_search`, `reference_grep`, `reference_view`, `reference_clean`, `reference_find_symbol`, `reference_diff`, `reference_resolve_symbol_at`, `reference_embed_build`, `reference_embed_search`.

`local_lsp_write` returns an error if `UNITY_CLI_LSP_MODE=off` (`"{tool} requires LSP"`). Default mode is off, so symbol writes fail closed until the user sets `auto` or `required`. That matches the constitution’s “do not silently degrade” rule and contradicts any reading that LSP is always on.

`reference_*` shallow-clones [UnityCsReference](https://github.com/Unity-Technologies/UnityCsReference) into `~/.unity/cache/UnityCsReference/<version>/`. `reference_embed_build` / `embed-search` use **BGE-Small-EN via ONNX**. On Intel macOS the Homebrew formula is expected to supply `onnxruntime`, or `ORT_DYLIB_PATH`. Embeddings are optional and local.

After a local C# write, `run_csharp_post_write_pipeline` can update the index and, only if the caller sets `refresh` or `waitForCompile` (both default **false**), call remote `refresh_assets` and then poll compilation.

## Recording and input

**CONFIRMED** in `docs/tools.md` and `TOOL_NAMES`:

- Input System authoring: action maps, bindings, composites, control schemes, asset analysis.
- Play Mode simulation: `input_keyboard`, `input_gamepad`, `input_mouse`, `input_touch`, `create_input_sequence`, `get_current_input_state`. Holds use `holdSeconds`. The doc says device state is not proof that an InputAction callback fired; check the console or game evidence.
- `setup` / `bridge install`, when the Editor is closed and the project is legacy Input Manager only, sets `activeInputHandler` to Both so the Input System package import does not stick on a restart dialog.
- UI tools are separate: `find_ui_elements`, `get_ui_element_state`, `click_ui_element`, `set_ui_element_value`, `simulate_ui_input`.
- Video: `capture_video_start|status|stop`. Formats **mp4, webm, png_sequence**. Requires optional `com.unity.recorder` **4.0+**. Without it, `RECORDER_PACKAGE_MISSING`. Recorder pulls Timeline; unrelated commands no longer require either package.

## Build and bake jobs

**CONFIRMED** tools and the v0.13–v0.15 examples:

- `build_player` returns a `buildId`. Poll `get_build_status` until `succeeded`, `failed`, or `interrupted`. Only `succeeded` is a pass. The active build target and its module must already be installed. Output must be outside assets; ancestors must not be symlinks. macOS example uses `/private/tmp` because `/tmp` is a symlink.
- `start_scene_bake` targets `lighting`, `navmesh-legacy`, `navmesh-surface`, `occlusion`. Poll `get_scene_bake_status` by `jobId` for progress and verified saved artifacts. `navmesh-surface` needs AI Navigation and a `surfacePath`.
- Extended matrix (**DOCUMENTED**, issue #392, only some Editors): bake PASS on 2022.3.62f3 and 6000.3.25f1 (4 types). Player build PASS for macOS and **UNSUPPORTED** for Windows (`BUILD_MODULE_MISSING`). Not a claim of a successful Windows player build. 6000.0 / 6000.4 / 6000.5 / 6000.6 / 6000.7 rows are dashes.

Also present, not re-read handler-by-handler: Addressables (`addressables_analyze|build|manage`), Timeline (`get_timeline`, `manage_timeline`), animation clips/curves, VFX Graph (`vfx_describe_graph`, `vfx_list_library`, `vfx_apply`, `vfx_runtime`, `vfx_settings`, `vfx_bake_sdf`) with versioned `VFX_API_UNSUPPORTED` rather than a crash, profiler start/stop/metrics, `package_manager`, `registry_config`.

## Supported Unity versions

**CONFIRMED** `package.json`: `"unity": "2022.3"`. Dependencies: Newtonsoft 3.2.1, Test Framework 1.1.33, Input System 1.14.2, uGUI 1.0.0.

**DOCUMENTED CLAIM** `docs/editor-compatibility.md`, macOS Apple Silicon, 2026-09-30, baseline PASS including package compile, input, Timeline, VFX, C# eval, hot-reload **error contract**, PlayMode/domain-reload results, and the all-tools sweep:

| Editor | Runtime | VFX | All-tools |
| --- | --- | --- | --- |
| 2022.3.62f3 | Mono | 14.0.12 | 121/121, 133 calls |
| 6000.0.84f1 | Mono | 17.0.4 | same |
| 6000.3.25f1 | Mono | 17.3.0 | same |
| 6000.4.11f1 | Mono | 17.4.0 | same |
| 6000.5.3f1 | Mono | 17.5.0 | same |
| 6000.6.3f1 | Mono | 17.6.0 | same |
| 6000.7.0a2 | Mono | 17.7.0 | PASS after retry |
| 6000.7.0b2 | Mono | 17.7.0 | same |

6000.7 Editor is Mono. CoreCLR is a Player backend and is **out of matrix**. Windows hardware is deferred (issues #340 AC-6 and #386). 6000.7.0a2 failed twice for engine-side reasons before a clean retry; those failures are written down and not counted as passes. This pass did not re-run `scripts/e2e-matrix.sh`.

## Domain reload

**CONFIRMED / DOCUMENTED**, several different behaviors:

- PlayMode tests may keep domain reload **enabled**. The bridge persists the run and the client is told to reconnect and poll `get_test_status` instead of submitting a second run (`docs/tools.md`). There is a PlayMode fixture `DomainReloadResultTests`.
- 6000.7.0a2’s Test Framework clears its assembly cache when entering Play without domain reload. The bridge invalidates that cache on that alpha. If the hook is missing, it fails with `TEST_RUNNER_DOMAIN_RELOAD_REQUIRED` rather than reporting zero tests.
- Eval results and the eval compile cache are **per domain**. `get_eval_status` says “current Editor domain.” First call after reload is the slow one.
- Hot-reload `recover` wants a clean domain reload and does not restart Play by itself.
- Lockfile `state=reloading` plus a new auth token after the domain returns (above).
- Benchmarks that include Play/Stop wait until Bridge `isCompiling` / `isUpdating` clear. Those particular latency runs used **DisableDomainReload** and **PlayUnfocused**. They are not a domain-reload latency claim.

There is no single “readiness epoch” object. Readiness is split across `doctor`, `get_compilation_state`, `get_editor_state`, lockfile `state`, and per-job status.

## Jobs

**CONFIRMED** long work is start-plus-poll, not one blocking MCP call:

| Start | Poll | Terminal |
| --- | --- | --- |
| `run_tests` | `get_test_status` | exit 0 or 8 on the final poll |
| `build_player` | `get_build_status` | `succeeded` / `failed` / `interrupted` |
| `start_scene_bake` | `get_scene_bake_status` | `succeeded` / `failed` |
| `capture_video_start` | `capture_video_status` | `capture_video_stop` |
| `eval_csharp` (async id) | `get_eval_status` | `state` even when the transport succeeded |
| `hot_reload` begin | `hot_reload_status` | `appliedRevision` or `recover` |

`batch` exists as a CLI command (JSON or stdin). Not traced command-by-command in this pass. Eval inside a batch still uses direct TCP and is not auto-retried through `unityd`.

## Capture

**CONFIRMED** `capture_screenshot` modes include `game`, `scene`, `explorer`, `window`.

- `captureMode: game` with `includeUI: true` (default) wants the final frame, including UI Toolkit and Screen Space Overlay, and **does not steal focus**. Failures: `GAME_VIEW_NOT_FOCUSED`, `GAME_CAPTURE_UNAVAILABLE`, `GAME_CAPTURE_BUSY`, `GAME_CAPTURE_TIMEOUT` (5 seconds). `includeUI: false` is camera-only.
- Width and height resize the frame. Omitted size is native Game resolution. The official-CLI comparison used a 1280×720 PNG **with no base64 in the response**.
- If the Editor accepts the shot but does not answer before `--timeout-ms`, the CLI’s **default** is a full desktop capture via `screencapture` / GDI / grim|gnome-screenshot|spectacle|import|scrot|maim, saved as PNG, tagged `"fallback": "os"`. `osFallback: false` keeps the timeout error instead. v0.18.1 release notes fix a Windows verbatim-path bug in that fallback.
- `analyze_screenshot` is a separate tool. Its model and limits were not opened in this pass. **UNKNOWN** whether analysis is local or calls a vision API.

This is a full-frame PNG/video design. It is not an Overlay JPEG look, not a 768 px longest-edge default, and not an object crop.

## Compile

**CONFIRMED** there is no `unity_write_and_compile` equivalent as the default path.

- Remote: `refresh_assets`, `get_compilation_state`. `doctor` reads compile errors from `Editor.log`, including Safe Mode.
- Local writes refresh the Editor only when asked (`waitForCompile` defaults false).
- `eval_csharp` compiles a snippet in the Editor domain with a pinned Editor-only Roslyn bundle, C# 9, implicit `System` / `UnityEngine` / `UnityEditor`. It caches references and identical snippets, still executes every call, and refuses to start while compiling, refreshing, transitioning, or already evaluating. It does not enter Play or save scenes. Async results are unsupported. Docs quote warm identical-snippet p50 about **14–25 ms** frontmost; a new snippet pays a compile. That figure is a **DOCUMENTED CLAIM** in `docs/tools.md`, with the method in `docs/editor-eval.md`.
- VFX `vfx_apply` can recompile the graph per op or batch with `autoCompile: false` then one `compile` op.

## Context strategy

**CONFIRMED** pieces, not one context service:

1. Skills: short front-loaded descriptions, long bodies and references loaded only when the skill fires.
2. Discovery: `--query`, `--compact`, `--category`, `--limit`, then one `tool schema`. Plain `tool list` still prints every name.
3. Payloads that can shrink: VFX describe `include` / `includeSlots:false`; test XML and screenshots written to disk rather than always inlined (video and OS-fallback paths return paths). **UNKNOWN** whether every screenshot tool inlines base64. The comparison page says those benchmark shots did not.
4. Code context without the Editor: ripgrep-style `search` / `read`, optional LSP, optional UnityCsReference checkout, optional ONNX embeddings.
5. Eval serialization is bounded in depth and size. Property getters still run. Cycles fail explicitly. Unity objects return a descriptor (`kind`, `type`, `name`, `instanceId`), not the whole object.
6. MCP `tools/list` does the opposite: the full catalog, no cursor.

## Benchmark and acceptance philosophy

**DOCUMENTED CLAIM**, `docs/comparison.md` plus JSON under `docs/benchmarks/`, source commit `6ca7f454…`, unity-cli **0.17.0 development build** (auth on), official CLI **1.0.0-beta.11**, Pipeline **0.8.0-exp.1**, one Apple M5 Max, 128 GiB, macOS 26.5, Unity 6000.3.25f1, shared isolated project. Not re-run here. Not a 0.18.1 number. Not a Nexus number.

Method they actually publish:

- 100 samples after 3 warmups, alternating tool order, frontmost and background as separate runs.
- Process mode: one CLI spawn per sample, `unityd` already warm, eval on direct TCP. Objects deleted outside the timed span.
- Resident mode: 23 operations, unity-cli still spawns a short CLI against warm `unityd`, official side is one NDJSON shell. Daemon/shell startup excluded. Play/Stop include waiting until ready flags clear.
- Failures, wrong ids, timeouts, daemon restarts, and daemon-to-direct fallback **abort** the run. Focus glitches are discarded and remeasured.
- JSON keeps every sample, nearest-rank p50/p95, binary hashes, focus, Play settings, and host load at start and end.
- They print losses. Frontmost resident **Play until ready** was **552.16 ms vs 266.35 ms** (unity-cli slower). Hierarchy frontmost process p50 was **7.91 vs 20.40 ms**. Background process p50 rose to about **61–67 ms** for both. They explicitly refuse to treat an older staff note of ~550 ms official latency as the current official number.
- Play in that benchmark used DisableDomainReload and PlayUnfocused. Hierarchy payloads are not byte-identical. “Faster” is not claimed for every command.

Acceptance philosophy elsewhere: versioned Editor matrix, stable error codes per VFX package version, hot-reload absence is an expected error not a pass, alpha engine failures recorded separately, Windows gaps left open, skill install has a real Claude Code session check (`scripts/e2e-skills-install.py`) that does not claim a human looked at the scene. Specs live in GitHub issues labeled `gwt-spec`, not a local speckit tree (constitution forbids leftover speckit).

## Security

**CONFIRMED**

- Loopback default, plaintext TCP, same-user lockfile readers are in scope. Architecture says use a trusted network or a tunnel for non-loopback, and that the token is not a sandbox.
- 256-bit token, 0600, constant-time compare, tokens omitted from instance listings, wrong token never falls open.
- Unauthenticated opt-out still shipped, labeled temporary since at least the architecture page, still in 0.18.1.
- `eval_csharp` and statement mode run on the Editor main thread with Editor privileges. Docs: not a security sandbox; undo is the snippet’s problem. Timeout does not cancel execution.
- Desktop screenshot fallback can capture whatever is on screen, including other apps, if a modal blocks the Editor. Screen Recording permission changes what macOS returns.
- Skill install refuses symlink swaps and will not claim an existing skill directory that is not in its ledger.
- Installers check `SHA256SUMS` and leave the old binary in place on mismatch (`README`). Release assets include those sums. **DOCUMENTED CLAIM** for the installer’s behavior; the script body was not audited line by line.
- No CLI account, and the comparison page says the CLI dependencies do not phone home analytics. **DOCUMENTED CLAIM** from `docs/comparison.md` pointing at `Cargo.toml` and `src/`. Not a full secret-scan.
- MCP configure refuses to write a token into client settings, refuses symlinks, and refuses to overwrite an existing HTTP server named `unity-cli`.

## Installation

**DOCUMENTED** in README, consistent with release asset names:

- `curl | sh` / `irm | iex` into `~/.unity/tools/unity-cli/{rid}/`, symlink or PATH entry, checksum gate, background auto-update unless `UNITY_CLI_NO_AUTO_UPDATE=1`. Pin with `UNITY_CLI_VERSION=vX.Y.Z`.
- `brew install akiojin/tap/unity-cli`, `winget install akiojin.unity-cli`, `cargo install`, or GitHub Release binaries (linux/osx/win, x64 and arm64 where built).
- `unity-cli setup` adds the OpenUPM scoped registry and `com.akiojin.unity-cli-bridge` **pinned to the CLI version**, optionally launches the Hub Editor, waits, returns one JSON report. Manual alternatives: `openupm add` or git URL `?path=UnityCliBridge/Packages/unity-cli-bridge`.
- `unityd` and `lspd` refresh managed binaries on startup without a prompt.
- `unity-cli cli doctor|install` is binary maintenance, not the Unity `doctor`.

## Supported clients

**CONFIRMED** from CLI enums and `docs/skills.md` / `docs/mcp.md`:

| Path | Clients |
| --- | --- |
| Claude Code plugin marketplace | `unity-cli` plugin (skills; binary and bridge bootstrap on demand) |
| Codex plugin | `.codex-plugin` plus `.agents/skills` symlinks |
| `skills install` | `claude-code`, `cursor`, `windsurf`, `vscode` (VS Code lands in `.github/skills` or `~/.copilot/skills`) |
| `mcp configure` | `claude-code`, `cursor`, `vscode`, `windsurf`, `codex` |
| Not configured | Claude Desktop. Windsurf has no `--local`. Devin Desktop is named as out of scope. |

An Editor is not required to start the MCP adapter. The builtin catalog is available immediately; a later Editor is picked up per call and via `tools/list_changed`.

## Docs that disagree with source

| Doc | Says | Source on this commit |
| --- | --- | --- |
| `README.md` | 101 tool APIs, Skills (14) | 154 `TOOL_NAMES`, 23 plugin skills |
| `docs/architecture.md` | 129 tools; newline-looking diagram; “legacy UNITY_CLI_* aliases” that names the same variables | 154 tools; 4-byte length frame; migration table is similarly garbled |
| `docs/migration-notes.md` | MCP server mode removed; `package.json` deleted | `unity-cli mcp` exists; repo-root `package.json` exists (release tooling) |
| `docs/constitution.md` (2026-03-13) | Do not return to an MCP-specific implementation; LSP is the design center | Thin MCP adapter is current; LSP mode defaults off |
| `docs/comparison.md` | 153 tools, version 0.17.0 dev | Catalog is 154 at 0.18.1; benchmark is historical relative to this tree |
| Architecture auth section | Opt-out removed next minor | Flag still honored at 0.18.1 |

Use the Rust catalog, `docs/skills.md`, `docs/mcp.md`, `docs/hot-reload.md`, and `docs/editor-compatibility.md` over the root README counts.

## Sibling, not lineage: hatayama/unity-cli-loop

**CONFIRMED** shallow clone `b2b4d6b634b2157dbf6c28e9f6c9055fe7b71201` (2026-10-05 00:23 +0900, which is still 2026-10-04 15:23 UTC). Package `Packages/src/package.json`: **`io.github.hatayama.uloopmcp` 3.11.4**, Unity **2022.3**. GitHub `/releases/latest` on this date returned **`uloop-project-runner-v3.6.2`**, published 2026-10-04. Those numbers are different series on purpose (`docs/adr/0002-dispatcher-runner-split.md`, `docs/version-series-realignment.md`). **UNKNOWN** what `uloop -v` prints without running it.

V3 (`Packages/src/Documentation~/whats-new-v3.md`) **removed MCP**. A search of `*.cs`, `*.go`, and `*.md` found no MCP server type; the removal is documented, not reimplemented. A third-party `rougebleu/uloop-mcp` exists specifically because v3 dropped MCP. Not inspected beyond search.

**CONFIRMED** shape:

```text
uloop (global Go dispatcher)
  -> per-project uloop-project-runner (version from .uloop/project-runner-pin.json)
  -> Unix domain socket (macOS/Linux) or named pipe (Windows)
  -> Editor package
```

No TCP port. Protocol generation is an **exact integer equality** between `cli/common/clicontract/contract.json` and `CliConstants.REQUIRED_CLI_PROTOCOL_VERSION`, not a min-range (`docs/protocol-version.md`). The runner is pinned by the package so several projects on one machine do not share one CLI generation. ADR 0002 rejects copying official Unity’s single global binary for that reason.

**CONFIRMED** `cli/common/tools/default-tools.json` lists **22** tools: `compile`, `get-logs`, `run-tests`, `clear-console`, `focus-window`, `get-hierarchy`, `find-game-objects`, `screenshot`, `execute-dynamic-code`, `control-play-mode`, pause-point/watch (5), `simulate-mouse-ui`, `simulate-mouse-input`, `simulate-keyboard`, `replay-input`, `set-game-view-size`, `hot-reload`, `record-video`. About **23** `SKILL.md` files under `Packages/src` (one per command, plus `uloop-hello-world`). README design text: do not chase tool count; `execute-dynamic-code` covers almost everything; a new dedicated tool is usually the wrong answer; prefer a skill or a snippet. Skill bodies are described as loaded at startup (“token cost close to zero” is their claim for adding a skill, **DOCUMENTED**, not measured here).

Where this sibling is sharper than Akiojin, as a label not a recommendation:

- MCP is actually gone, and the builtin catalog is ~22 commands, not 154.
- `compile` refreshes, compiles, and waits for domain reload unless `--no-wait-for-domain-reload`.
- `get-hierarchy` writes JSON under `.uloop/outputs/` and returns a path.
- `get-logs` is tail-like with `MaxCount`.
- Screenshots are PNG of a named EditorWindow, or Game View `rendering` mode, optional raycast grid for later clicks. `record-video` is MP4, WebM on Linux.
- Hot reload is in-house: external Roslyn worker, publicized refs, `Assembly.Load`, Harmony transpiler. It patches **the files already on disk**, can add methods/fields visible to edited code in the same assembly group, and supports async/iterators. A later `compile` is how the change becomes real. ADR 0009 (2026-09-26): designed for Play entry **without** domain reload; with domain reload, changes active at Play entry are dropped and reported, not silently lost. Not Fast Script Reload.
- `pause-point` arms a line without editing source and returns captured locals/parameters/`this` as strings. Requires Code Optimization Debug. This has **no Akiojin equivalent** in `TOOL_NAMES`.
- `execute-dynamic-code` allows `await`, runs with full Editor permissions, and can be disabled in Tool Settings. Same class of risk as Akiojin eval, but it is the **intended** general API, which is why the dedicated list can stay small.
- PlayMode tests **force domain reload off** unless `--respect-enter-play-mode-settings`. Akiojin’s documented test path is the opposite: survive an enabled domain reload.
- Installer checks sigstore attestations (**DOCUMENTED** in what’s-new). Linux `focus-window` is unsupported.

Hatayama is the cleaner “small CLI plus skills” counterexample. Akiojin is the one that still carries a full Editor IDE surface (VFX, Timeline, Addressables, bake, player build, LSP, reference embeddings) and then puts skills in front of it.

## What Nexus is objectively better at

Compared with Akiojin as of this tree, using the Nexus baseline already in the package, not a new benchmark:

- **A real small primary contract.** `nexus.project_map`, `nexus.group_compile_errors`, and `nexus.capture_game_view` share one handler each and one envelope (`ok`, `data`, `error`, `meta`), with a CLI drift lock on those three. Akiojin’s skills are a router in front of **154** tools, and `unity-cli mcp` republishes that catalog with pagination refused. Nexus still has a large legacy raw/MCP list beside the three; the advantage is the canonical trio, not the absence of legacy tools.
- **Bounded look.** Canonical capture defaults to JPEG, 768 px longest edge, object crop, and Scene View framing (`nexus_look` alias). Akiojin’s game capture is a full frame (PNG in the official comparison), refuses to run unless the Game View is focused, and can fall back to a desktop screenshot. Better for agent tokens and for “did this object look right”; worse if the task is a full-resolution recording.
- **Compile is part of the write.** README: script writes need `confirm: true`, and readiness stays not-ready until a compile that started after the write settles. Akiojin local writes do not refresh or wait unless the caller opts in, and the default is off.
- **Readiness as one epoch story** (sprint baseline, not re-measured in this file). Akiojin splits that across doctor, compilation state, lockfile `state`, and job polls. Useful, but not one probe an agent can trust after Play or domain reload.
- **Official Pipeline as an optional primary**, with legacy HTTP kept until Unity CLI 1.0 is stable (README). Akiojin can run **beside** Pipeline (they did, ports 7800 and 6471) but does not speak Pipeline’s command set. Nexus can follow Unity’s CLI when that stack is the one installed. Floor is Unity **6000.0** in `package.json`, so this advantage does not exist on 2022.3.
- **Does not depend on a third-party hot-reload git pin** for ordinary edits. Ordinary Nexus edits are compile-and-reload, which is slower and is also what actually lands in the project.

## What Nexus is objectively worse at

- **2022.3 through 6000.7 as a tested bridge matrix.** Akiojin’s package floor is 2022.3 and the macOS matrix is published, including alphas, with failures written down. Nexus declares Unity 6000.0. Akiojin’s Windows gap is real; “worse on Windows” would not be fair.
- **Editor-free C# intelligence.** LSP symbol/rename/structured edit, plus a versioned UnityCsReference cache and optional ONNX embeddings, with an explicit error when LSP mode is off. Nexus canonical code help is `group_compile_errors` after Unity compiles.
- **Play Mode input breadth and jobs.** Keyboard, mouse, gamepad, touch, sequences, UI Toolkit/uGUI tools, Recorder video (mp4/webm/png sequence), player build status, and four bake targets are catalog tools with poll ids. Nexus README covers UI Toolkit window automation, `click_object_in_game`, and Test Runner polling, not this set.
- **Domain-reload-durable test runs** as a documented bridge behavior, and a doctor taxonomy (`SAFE_MODE`, port conflict, sandbox) that names the next action.
- **Skill contract tooling**: lint, sibling boundaries, hash-ledger install, one source tree for Claude Code and Codex. Nexus has no equivalent skill package in this comparison.
- **Measured, loss-including latency notes against official Pipeline**, even though they are one machine, one commit, and DisableDomainReload. Nexus has not published an equivalent table in this teardown.
- **Per-Editor 0600 token and constant-time compare**, with the token kept out of listings. Whether Nexus matches that today was **not** re-audited here. Do not treat it as a known Nexus bug in this file.

Hatayama, not Akiojin, is ahead of both on pause-points, in-house hot reload that can add members, hierarchy-as-a-file, and a 22-command catalog with MCP actually deleted. That gap is sibling context. It is not an Akiojin feature to copy by mistake.

## What Nexus could copy cheaply

Cheap means a contract or a CLI habit, not a new transport and not a Nexus-primary router.

- Skill shape only: front-loaded description, Use when / Do not use, one sibling, body cap, references one level down, and a lint. Point skills at the **three canonical commands** plus a short legacy escape, not at 154 raw names.
- Keyword discovery for whatever legacy list remains: `tool list --query --compact --limit` so agents stop pasting the full catalog. Akiojin still fails this on the MCP path; copy the CLI habit, not `tools/list` without a cursor.
- Doctor codes that say what to do next (`SAFE_MODE` means read these compile errors; do not assume the Editor is closed).
- Start-plus-poll with a stable id, and the rule that a timeout is not permission to resubmit, for tests and any other long call. Nexus Test Runner already polls; the copy is the explicit “do not duplicate” instruction.
- Dry-run on mutating canonical calls.
- Fail closed when a capability is off (Akiojin LSP mode, missing Recorder, missing Fast Script Reload, missing Windows build module) instead of a partial success.
- Version skew in the ping payload (`versionCheck` when CLI and bridge differ). Nexus already drift-locks the three commands; the same idea for Pipeline package versus bridge is the cheap part.
- Write screenshots and large hierarchies to a path by default when the caller did not ask for pixels. Hatayama’s hierarchy path is the cheaper pattern. Akiojin’s desktop fallback is not.

## What would require architecture change

Do not schedule these as Nexus-primary work. They are listed so a later decision can see the cost.

- Replacing legacy HTTP MCP with a resident native binary and a pooled socket (`unityd` or hatayama’s dispatcher plus per-project runner). That is a second runtime beside Pipeline and legacy HTTP.
- An LSP sidecar and a UnityCsReference/ONNX index. New process, new updater, new failure mode. Constitution-style “LSP or fail” fights a default-off switch; Nexus would have to pick one.
- In-Editor hot reload (Fast Script Reload pin, or hatayama’s Harmony/Roslyn worker) and pause-points. Both are domain-reload architecture, not a tool wrapper. Hatayama’s ADR says the domain-reload-on Play path is already in maintenance mode.
- Unix-socket or named-pipe IPC with an equality protocol version and a package-pinned runner. Solves multi-project CLI skew. It is a distribution system, not a command rename.
- A 100-tool Editor surface (VFX, Timeline, Addressables, bake, player build) with skills as the only progressive disclosure. That is Akiojin’s current architecture, including the MCP adapter that undoes the disclosure.
- Treating `eval` / `execute-dynamic-code` as the general API so the dedicated list can stay small. That is hatayama’s philosophy. It moves safety and schema control into an unsandboxed compiler.

## What should NOT be copied

- The 154-name catalog, or an MCP adapter that `tools/list`s it with pagination rejected. That is the giant surface this teardown was asked to challenge. Skills in front of it are not a fix once a client loads MCP.
- Hatayama’s “one dynamic C# tool replaces dedicated tools” as the Nexus primary API. It keeps the list small by giving the agent the Editor process.
- Akiojin’s hot reload as a default dependency: optional, macOS-only verified, third-party git pin, method-body preview that does not save, no rollback.
- The desktop screenshot fallback. A hung modal becomes a capture of every other window, and it is on by default.
- The unauthenticated opt-out that the docs still call temporary at 0.18.1.
- Stale public counts. README 101/14, architecture 129, comparison 153, source 154. A drift lock that does not cover the README is how agents learn the wrong surface.
- Their envelope field names. Nexus already has `ok` / `data` / `error` / `meta`. A second envelope dialect is not progressive disclosure.
- Benchmark claims that ignore their own caveats: one M5 Max, DisableDomainReload, different payloads, official Play-until-ready faster, 0.17.0 not 0.18.1, not Nexus.
- `curl | sh` without the checksum half. The checksum and “leave the old binary” behavior are the part worth keeping if a native install is ever built. The pipe is not.
- Coexistence theater: loading Bridge and Pipeline in one Editor proves they can both listen. It does not mean Nexus should grow an Akiojin-shaped TCP server next to Pipeline.

## Evidence paths

Clones, not part of the package:

- `/tmp/agent-e-teardown/akiojin-unity-cli` at `7f45d7ae`
- `/tmp/agent-e-teardown/hatayama-unity-cli-loop` at `b2b4d6b6`

Akiojin files that decide the claims: `src/tooling/tool_catalog.rs`, `src/unity/transport.rs`, `src/unity/auth.rs`, `src/mcp/mod.rs`, `src/cli.rs`, `src/daemon/unityd.rs`, `src/tooling/local_tools.rs`, `UnityCliBridge/Packages/unity-cli-bridge/package.json`, `Editor/Core/BridgeAuthentication.cs`, `Editor/Core/EditorLockfile.cs`, `docs/skills.md`, `docs/mcp.md`, `docs/hot-reload.md`, `docs/editor-compatibility.md`, `docs/editor-eval.md`, `docs/comparison.md`, `docs/tools.md`, `README.md`.

Hatayama files: `Packages/src/package.json`, `Packages/src/Documentation~/whats-new-v3.md`, `Packages/src/Documentation~/tools.md`, `cli/common/tools/default-tools.json`, `docs/adr/0002-dispatcher-runner-split.md`, `docs/adr/0009-hot-reload-targets-play-mode-without-domain-reload.md`, `docs/hot-reload.md`, `docs/protocol-version.md`, `README.md`.
