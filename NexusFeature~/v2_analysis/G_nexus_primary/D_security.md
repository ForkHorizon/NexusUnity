# D — Security architecture: can Unity writes be delegated behind a Nexus façade?

Agent D, v2.5 corrective pass, 2026-10-03. Research only. No production code was written.

Labels:
- **MEASURED**: observed on this machine.
- **REPO**: read in source. Paths are relative to the Nexus repo root, or `PC/` = `Library/PackageCache/com.unity.pipeline@ab05e9cd7f74/` (Pipeline `0.7.0-exp.1`).
- **DOC**: vendor documentation, with URL.
- **HYPOTHESIS** / **UNKNOWN**: not verified.

---

## 0. Bottom line

1. **Nexus cannot be a security boundary for writes. It can only be a guardrail.** HYPOTHESIS, strongly supported. The Pipeline bearer token sits in a file the user can read (`Library/Pipeline/.unity-pipeline-port`). The agent typically has a shell. So any policy Nexus applies before forwarding can be bypassed with `unity command <write>` or `unity eval`, unless the *client* is configured to deny that path (§3.6). Delegation does not reduce what the agent *can* do. It can only change what the agent is *steered* to do, and what gets logged, previewed and confirmed.
2. **Pipeline 0.7 has no pre-dispatch hook, no central policy and no public in-process invocation API.** REPO, §1. Delegation is therefore out-of-process only, over loopback HTTP `/api/exec` or a warm `unity mcp` / `unity shell` child. Every guarantee Nexus wants must be enforced *before* forwarding. That runs into TOCTOU and resolver-mismatch problems (§3.2).
3. **The real security value a façade could add is approval granularity, not containment.** DOC + MEASURED:
   - `unity mcp` tools carry no MCP annotations (no `readOnlyHint` / `destructiveHint`; MEASURED).
   - Claude Code can mark one tool as needing a human on *every* call (`_meta["anthropic/requiresUserInteraction"]`, DOC).
   - A façade split into read / write / destructive tools could give a human gate that Unity-direct cannot give today.
   - A façade that collapses many writes into one manager tool name does the opposite: it destroys per-tool permission rules.
4. **Verdict per category** (matrix in §4):
   - Bounded object writes: *later, with conditions*.
   - Asset/file writes: *later, narrow subset only*.
   - Settings writes and destructive ops: *keep Unity-direct*, except a gated destructive tool after Gate F.
   - Build and package installs: *keep Unity-direct*.
   - **`eval` / `eval_file` / `run_script` / free-form `batch`: keep Unity-direct, never delegated, no exceptions.**

---

## 1. Unity Pipeline 0.7 auth and policy model (REPO unless marked)

| Property | Finding | Evidence |
|---|---|---|
| Transport | `HttpListener` bound to the wildcard `http://+:{port}/`. Loopback is enforced per request: a non-loopback remote gets 403. | `PC/Runtime/Common/BasePipelineServer.cs:341-343`, `:519-528` |
| Origin | Any `Origin` header gets 403. The one opt-in exception is `Origin: null` (sandboxed frame), via `AllowSandboxedBrowserClients`, which still needs the token. | `BasePipelineServer.cs:530-548`, `:175-189` |
| Token | 256-bit CSPRNG, base64. Kept in `SessionState`, so it survives domain reloads and is regenerated on Editor restart or `RotateToken()`. | `PC/Runtime/SecurityTokenManager.cs:45-79`, `:103-111`, `:135-139` |
| Token check | `Authorization: Bearer` on **every** route except CORS preflight, checked before routing, with a constant-time compare. | `BasePipelineServer.cs:573-577`, `:641-653`; `SecurityTokenManager.cs:88-98` |
| Token storage | `Library/Pipeline/.unity-pipeline-port`, field `evalToken`. Permissions are restricted to the current user (`chmod` / `icacls`) on creation. "Never written to a separate file". | `PC/Runtime/Models/InstanceDescriptor.cs:15,24,170-192`; `PC/Runtime/Common/FilePermissions.cs:23-42`; `SecurityTokenManager.cs:17-18` |
| Token scope | **One token, all 154 commands.** There are no scopes, roles or read-only tokens. | No scope concept anywhere in the `IsAuthorized` path (`:641-653`) |
| Pre-dispatch hook | **None.** The only extension point is `protected virtual OnCommandDone`, which runs *after* execution. `EditorPipelineServer` is `internal`, so it cannot be subclassed from Nexus. | `BasePipelineServer.cs:2177`, `:2162`; `PC/Editor/EditorPipelineServer.cs:18,249-252` |
| Central safety policy | **Not implemented.** Pipeline itself says "no safety-policy gate yet (CAT-2509 is not implemented)". `ProjectPaths` and `AuthoringUndoScope` are only a "minimal seed". The docs say "There is no central dispatcher; each command applies the same simple rules inline". | `PC/Runtime/Commands/RuntimeInputCommand.cs:26-29`; `PC/Editor/Authoring/ProjectPaths.cs:19`; `AuthoringUndoScope.cs:23`; `PC/Documentation~/safety-and-mutations.md:11-13` |
| In-process API | `CommandRegistry` is `internal static`. Command classes are `internal static` (`ComponentCommands`, `AssetCommands`, `BatchCommand`, `CodeEvalCommand`). `DispatchCommandOnMainThread` is `internal`. Public pieces: `RegisterCommand` (dynamic *registration*, not invocation), `ProjectPaths`, `AuthoringUndoScope`, `BasePipelineServer` (abstract). **Invoking an official command in-process requires reflection, which plan §4 forbids.** | `PC/Runtime/Common/CommandRegistry.cs:29,116`; `PC/Editor/Commands/GameObjects/ComponentCommands.cs:24`; `BatchCommand.cs:56`; `CodeEvalCommand.cs:14`; `BasePipelineServer.cs:2591` |
| Path sandbox | `ProjectPaths.Resolve` is lexical: it normalises and rejects `..`. There is **no symlink resolution** (`Path.GetFullPath` only, line 79). Paths are confined to an `AuthoringRoot` (default `Assets`) stored in EditorPrefs. | `ProjectPaths.cs:64-115` |
| Sandbox is agent-adjustable | `set_authoring_root` is an ordinary command, has no `confirm`, and is exposed in `unity mcp` (MEASURED). An agent can widen its own sandbox back to `Assets`. | `PC/Editor/Commands/Authoring/AuthoringConfigCommands.cs:19`; `F_facade_evidence/unity_mcp_tools_list.jsonl` |
| Object refs bypass the root | `ObjectResolver.TryResolve`: `globalId`, `guid`, `instanceId` and explicit `Assets/…` / `Packages/…` paths resolve **without** authoring-root confinement. So `set_component_properties` can edit any loadable object, including `Packages/` assets. `delete_asset` re-confines; `set_component_properties` does not. | `PC/Editor/Authoring/ObjectResolver.cs:35-46,48-89,90-122`; `AssetCommands.cs:425-450` vs `ComponentCommands.cs:127-161` |
| `confirm` / `dry_run` | Per-command and agent-set, like Nexus D-11. Required for: settings `set_*`, `package_add/remove`, `build`, `delete_asset`, `clear_baked_*`, `remove_*_binding`, and *overwrite* in `write_text_file`. Advisory ("accepted for parity") on bakes and materials. Absent on `delete_gameobject` (undoable). | `ProjectSettingsCommand.cs:88`; `PackageManagerCommand.cs:257,395`; `BuildCommand.cs:103`; `AssetCommands.cs:275-296`; `LightingBakeCommands.cs:58,137-141`; `TextFileCommands.cs:50-66` |
| File-write deny list | `write_text_file` writes any extension under the root. New files need no `confirm`, so `.cs`, `.asmdef` and `.rsp` are writable without one. | `TextFileCommands.cs:50-79` |
| Build output | `outputPath` may be absolute or `../`. It is not confined. | `BuildCommand.cs:99`, `:506-511` |
| Packages | `package_add` accepts registry ids, git URLs and `file:../…`; needs `confirm`. | `PackageManagerCommand.cs:257-260` |
| `batch` | Cap of 200 ops. Excludes `eval`, `eval_file`, `run_script`, `menu`, `build`, `recompile`, play controls, `package_*` and nested `batch`. **With `transactional=false` it runs non-Undo asset/file/settings writes** (per-op `revertible:false`). The exclusion lists are curated by hand ("V1 caveat"). | `BatchCommand.cs:59,97-111,113-135,184-189` |
| `eval` / `run_script` | Roslyn compile + execute on the main thread, timeout up to 24 h. `run_script` is self-described as "still arbitrary code execution — only the carrier changes". There is no switch to disable eval; the "host that disables the eval family" is client-side, by tag. | `PC/Runtime/Commands/CodeEvalCommand.cs:16,27,80`; `PC/Editor/Commands/Scripts/RunScriptCommand.cs:35,44` |
| Audit | Request/response transaction log at `Logs/pipeline.log`, **off by default** (`m_LogRequestsResponses = false`). Eval-usage telemetry is on by default (`Library/Pipeline/eval-usage.jsonl` exists, MEASURED). | `PC/Editor/PipelineTransactionLog.cs:13-23`; `EditorPipelineManager.cs:33,38` |
| Idempotency | None. The dispatcher cancels *queued* items on timeout, but an item already running still applies (same class of bug as Nexus D-2). | `PC/Runtime/Common/Dispatcher.cs:73-94`; no `request_id` anywhere in `PC/` |
| Server-side tool filter | `unity mcp` has no include/exclude/tag flags (MEASURED: `unity mcp --help`). Filtering exists only client-side. | §3.5 |

## 2. Nexus's own model (REPO unless marked)

| Invariant (§6.8) | Reality | Evidence |
|---|---|---|
| Loopback + Origin | Request URL must be loopback. An `Origin`, if present, must be a loopback http(s) origin, which is **looser than Pipeline**: a page served from any `localhost` port passes the Origin check, though it still needs the token. | `Editor/MCPServer.Http.cs:18-30` |
| Token | `X-Nexus-Unity-Token`, compared with `string.Equals` (not constant-time). Falls back to re-reading the file. `get_server_status` and `shutdown_server` are exempt (D-1). | `MCPServer.Http.cs:132,190-210`; `MCPServer.cs:36` |
| Token file | `Library/NexusUnityAuthToken.txt`, written with no permission restriction. **MEASURED: mode `-rw-r--r--` (0644, readable by other local users)**, whereas Pipeline restricts its file to the user. | `Editor/MCPServer.Identity.cs:112-122`; `ls -la` on 2026-10-03 |
| Path sandbox | `ValidatePath` resolves symlinks (`ResolveRealPath`) for both the target and the project root. That is **stronger than Pipeline**. `ValidateWritableAssetPath` forbids `Packages/` and `ProjectSettings/` and requires `Assets/`. | `Editor/MCPServerMethods.Utils.cs:179-201`, `:252-274` |
| `confirm:true` | Agent-set intent flag on `.cs` writes, `delete_asset` and PlayerPrefs delete-all (D-11, not a human gate). | `Utils.cs:287-291`; `MCPServerMethods.Asset.cs:238`; `MCPServerMethods.PlayerPrefs.cs:57` |
| "Type allowlists" | Actually a **deny list** of assembly and namespace prefixes (`System`, `Mono`, `UnityEditorInternal`, …). `UnityEditor.*` and user types are allowed. | `MCPServerMethods.TypeResolution.cs:15-75` |
| Batch cap 50 | `batch_execute` ≤ 50, no recursion. Arbitrary inner methods. | `MCPServerMethods.Core.cs:20,52-75` |
| `request_id` idempotency | **Plan only, not implemented** (no occurrence under `Editor/`). | grep; MASTER_PLAN §4, P0-04/05 |

**Consequence:** today Nexus and Unity enforce *different* sandboxes:
- Nexus is symlink-aware and protects `ProjectSettings` / `Packages`.
- Unity is lexical, its root is agent-mutable, and object refs are unconfined.

A façade that forwards to Unity inherits **the weaker of the two** unless Nexus re-implements the checks up front. That is permitted: validation is not "duplicate implementation of the primitive".

## 3. What a façade adds and removes

### 3.1 Where enforcement would live
There is no Pipeline hook, so a Nexus façade (S6) has exactly one enforcement point: **Nexus, before forwarding**.

Two placements are possible:

- **(a) L1 adapter** (Python bridge or shim) that reads `evalToken` from the port file and calls `/api/exec`, or drives a warm `unity mcp` child.
  - Enforcement happens in Python, outside Unity. It cannot see live object state without an extra read.
- **(b) In-Editor Core command** that calls back into Pipeline over loopback HTTP.
  - HYPOTHESIS, a structural risk: a Nexus handler on the main thread that waits synchronously for a `MainThreadRequired` Pipeline command will deadlock. Both share one main thread (`Dispatcher.cs:73-94` waits on a queue pumped from the Editor update loop).
  - This forces async, off-main-thread forwarding, which brings back the D-2 "timed out but still applied" class.
  - Placement (b) also re-introduces the reflection temptation (§1, in-process API), which plan §4 forbids.

**Recommendation:** if writes are ever delegated, use (a) with the warm-child transport, and keep (b) for reads only.

### 3.2 Pre-validation soundness (TOCTOU and resolver mismatch)
- **Paths.**
  - The resolvers disagree:
    - Nexus resolves relative to the project root; Unity resolves bare paths relative to `AuthoringRoot`, which is mutable through `set_authoring_root`.
    - Nexus follows symlinks; Unity does not.
  - So a path Nexus approved can resolve differently inside Unity.
  - **Condition C-P:** Nexus forwards only fully canonical, explicit `Assets/…` paths that it resolved itself (symlink-free, not under `Packages/` or `ProjectSettings/`, extension deny-list per §6.8). It refuses any path whose real path differs from its lexical path.
  - Residual window: a same-user process can swap a symlink between check and use. Out of scope, because that process already owns the token (D-1 reasoning).
- **Object refs.**
  - Unity re-resolves hierarchy paths with first-match semantics (`ObjectResolver.cs:136-146`, `:225`). An `instanceId` can point at a different object after a reload. Neither confines to the authoring root.
  - **Condition C-O:** Nexus resolves the reference itself, checks the target (scene object, not an asset under `Packages/`, component type on an allow-list), and forwards **only the `globalId`**. That is the stable identifier, resolved first by Unity (`:35-46`).
  - Nexus then **verifies the post-state**. `set_component_properties` already re-reads the component (`ComponentCommands.cs:159-160`); Nexus compares the target `globalId` in the result.
  - That is detect-after, not prevent-before. For Undo-able ops it is acceptable, because Nexus can `Undo` the group on mismatch.
- **Values.**
  - `set_component_properties` takes arbitrary `properties` (`JObject`) and object-ref values. An object-ref value can wire an asset from anywhere into a scene component.
  - **Condition C-V:** Nexus validates each value. Object-ref values pass through C-O. A deny-list of property paths (`m_Script`, and `m_GameObject` etc.) blocks re-pointing script or ownership fields. Payload is capped.

### 3.3 Auditability
- A façade can log every delegated write. The log entry holds: `request_id`, tool and action, canonical args after validation, the Unity command and its params, outcome, duration, and the Undo group name.
- That is strictly better than Unity-direct, whose transaction log is off by default (`EditorPipelineManager.cs:33`).
- The log is **not complete**: anything the agent does Unity-direct is absent from it. Never market it as a full audit trail (same honesty as R5).
- `request_id`:
  - Nexus can deduplicate agent→Nexus retries.
  - It cannot make Nexus→Unity calls idempotent, because Pipeline has no `request_id`.
  - **Condition C-I:** on a transport timeout, Nexus returns `outcome:"unknown"` and **never auto-retries a forwarded write**.

### 3.4 Undo grouping
- Each Unity command collapses its own Undo group (`AuthoringUndoScope.cs:35-56`). A Nexus composite that makes N forwarded calls leaves N Undo steps, plus a partial state if a later call fails.
- **Condition C-U:** a multi-step write composite is emitted as **one Nexus-constructed Unity `batch` with `transactional:true`**, which gives one Undo step plus rollback (`BatchCommand.cs:36-44`). The op list is built from Nexus's own closed op set, never agent-supplied `operations`.
- Non-Undo writes (asset, file, settings) cannot be in a transactional batch, so they cannot be part of an atomic composite.

### 3.5 Approval semantics and client permission granularity (DOC)
- **Claude Code** (https://code.claude.com/docs/en/permissions):
  - Allow rules match by tool name, or a glob after `mcp__<server>__`.
  - A parameter-level rule for an MCP tool is possible **only as a deny rule passed with `--disallowedTools`**, and it matches exact scalar values. A settings file *skips* any `mcp__` rule with parentheses.
  - So "allow `nexus_object` except `action=delete`" cannot be written in settings. It needs a PreToolUse hook, which can inspect the input and deny or ask.
  - Per-tool `_meta["anthropic/requiresUserInteraction"]: true` prompts on every call, even in `auto` and `bypassPermissions`, and allow rules cannot skip it (https://code.claude.com/docs/en/mcp). Exceptions: `dontAsk` mode denies the call instead, and an Agent SDK `canUseTool` callback may approve it.
- **Codex CLI** (https://learn.chatgpt.com/docs/extend/mcp?surface=cli):
  - Per-tool `approval_mode` (`auto | prompt | writes | approve`), by tool name only. No argument-level rules.
  - `writes` relies on the read-only annotation.
  - MEASURED: none of the 154 `unity mcp` tools carry annotations, so in Codex `writes` mode every Unity tool, reads included, is treated as a write.
- **Elicitation:**
  - A server can ask the user mid-call. Claude Code supports form and URL mode, and declares `elicitation:{form,url}` on protocol revision 2026-07-28.
  - It **can be auto-answered by an `Elicitation` hook**, so it is a strong but user-configurable gate.
  - The Nexus Python bridge speaks protocol `2024-11-05` (D-6), so elicitation is **not available today**.
  - Codex elicitation support: UNKNOWN.
- **Implications:**
  - **Single manager tool** (`nexus_object(action=set_property|delete|…)`, S6-router or S6-curated):
    - Every write collapses under one name. The user can only approve or deny the whole family, and Codex cannot tell read from write.
    - This weakens approval granularity relative to S4. **Reject for writes.**
  - **Split façade** (`nexus_read_*` with `readOnlyHint:true`; one `nexus_edit_objects` with `destructiveHint:false`; a separate `nexus_destructive` with `destructiveHint:true` + `anthropic/requiresUserInteraction:true`):
    - Granularity is equal to or better than S4.
    - Destructive ops get a real per-call human prompt in Claude Code, unlike `confirm:true`. This is the only place where S6 beats S4 on security.
    - **Condition C-A:** never mix read and write actions in one tool, and never mix destructive and non-destructive writes in one tool.

### 3.6 Prompt-injection attack surface (S4 vs S6)
Untrusted text reaches the agent through:
- console logs (including third-party package logs),
- UI text, GameObject names, serialized strings and asset contents,
- `read_text_file`.

How each arm changes exposure:
- **S4.** The injected instruction meets an agent that has `eval`, `run_script`, `write_text_file`, `package_add` (git URL), `build` (unconfined output) and `set_authoring_root` loaded or discoverable as first-class tools. One tool call from injection to code execution.
- **S6, with `eval` and the rest not exposed.**
  - The injection must also persuade the agent to leave the façade and use `unity …` through the shell. That is one more step, and blockable by client config.
  - The façade can also normalise reads so untrusted strings come back as clearly delimited data fields, truncated, never as instructions or hints. That is a modest defence in depth (HYPOTHESIS: effect size unmeasured).
- **But** S6 is a guardrail only if the client denies the bypass: `Bash(unity:*)`, the `unity` MCP server, `Read(Library/Pipeline/**)`, and generic `curl`. With an unrestricted shell, an injected agent can read the port file and call `/api/exec` directly. **S6 lowers the *probability* of injected writes. It does not change the *capability* ceiling.**
- **New risk that S6 adds:** Nexus becomes a *confused deputy* holding the Pipeline token.
  - Any Nexus-side bug in validation, a forwarded field or an unescaped composite becomes a Unity write under Nexus's authority.
  - The Nexus token file is world-readable (MEASURED 0644) and compared without constant time. If the bridge holds the Pipeline token, its own token hygiene must at least match Pipeline's.
  - **Condition C-T:** chmod 600 the Nexus token file and use a constant-time compare before *any* write delegation.

## 4. Matrix

Legend: **NOW** = feasible to delegate safely now · **LATER** = later, with the listed conditions · **DIRECT** = keep Unity-direct.

| # | Category (Unity examples) | Verdict | Required conditions / reason |
|---|---|---|---|
| 1 | **Bounded property/object writes with Undo** (`set_component_properties`, `set_transform`, `set_active`, `rename_gameobject`, `create_gameobject(s)`, `add_component`, `set_parent`, `set_serialized_field`) | **LATER** (not NOW) | Gate F passes. Then:<br>• C-O: Nexus resolves the ref and forwards only the `globalId`, scene objects only, type allow-list.<br>• C-V: property-path deny list, object-ref values confined, payload cap.<br>• C-U: multi-op goes through one Nexus-built transactional `batch`.<br>• C-I: `request_id` and no auto-retry.<br>• C-A: separate write tool with annotations.<br>• C-T before Nexus holds the Pipeline token.<br>• Post-state verification.<br>• Audit log.<br>• Transport (a): warm child, never in-Editor sync loopback.<br>• Contract tests pinned to Pipeline N/N-1, because the resolver and exclusion lists are curated "V1".<br>Why not NOW: no `request_id`, protocol 2024-11-05, the token file issue, and §6.8 says "write params are not projected to Pipeline". |
| 2 | **Asset/file writes** (`write_text_file`, `create_asset`, `create_folder`, `move/copy/rename_asset`, `create_script`, `import_asset`, `set_import_settings`) | **LATER, narrow subset only**: `create_folder`, `create_asset` (typed ScriptableObject allow-list), `move/rename` inside `Assets/`. **DIRECT** for `write_text_file`, `create_script` and `import_asset`. | Not undoable, so they cannot join a transactional composite.<br>Unity's sandbox is weaker than §6.8: no symlink resolution, no extension deny-list, agent-mutable root.<br>Delegating would make Nexus vouch for a sandbox it does not control.<br>If delegated, they need C-P plus the full §6.8 deny list (`.cs` needs `confirm`; block `*.rsp`, `.dll`, `.asmdef`/`.asmref` with precompiled refs, and `Packages/`).<br>`import_asset` copies an *external* file into the project, which is an exfiltration/ingress vector; keep it DIRECT.<br>Nexus already has its own sandboxed `write_file`, so delegation adds nothing. |
| 3 | **Settings / ProjectSettings writes** (`set_player_settings`, `set_*_settings`, `set_build_settings`, tags/layers, `set_authoring_root`) | **DIRECT** | §6.8: "ProjectSettings/Packages write-protected" is a Nexus invariant. Forwarding these would make Nexus the channel for exactly what it forbids.<br>Not undoable. They affect every later build.<br>`set_authoring_root` in particular must never be reachable through Nexus, because it widens Unity's sandbox.<br>Read-only `get_*_settings` may be delegated (read allow-list, already plan §4). |
| 4 | **Destructive ops** (`delete_asset`, `delete_gameobject`, `remove_component`, `clear_baked_lighting`/`navmesh`, `remove_*_binding`, overwrite cases) | **DIRECT now. LATER only as one dedicated gated tool.** | Allowed only if:<br>• it is a separate tool with `destructiveHint:true` + `anthropic/requiresUserInteraction:true` (a human prompt every call in Claude Code), plus elicitation once the bridge speaks 2026-07-28;<br>• `dry_run` preview first, with the preview shown in the prompt;<br>• no `confirm:true` passthrough from the agent: Nexus sets `confirm` only after the human gate;<br>• C-O/C-P and an audit record.<br>Undoable deletes (`delete_gameobject`) are lower risk than `delete_asset` and the bakes (not undoable, per Unity's own text).<br>This is the one place a façade can *add* a guarantee Unity-direct lacks. Without that gate, keep DIRECT. |
| 5 | **Build / package installs** (`build`, `switch_build_target`, `package_add/remove`) | **DIRECT** | `package_add` accepts git URLs and `file:` paths. That is a supply-chain and code-execution vector: package code runs at import (editor scripts, `[InitializeOnLoad]`).<br>`build` output is unconfined (`BuildCommand.cs:506-511`).<br>Both are long and asynchronous, so a failed or stuck build would leave the façade in its worst `outcome:"unknown"` state.<br>Unity already excludes them from `batch`.<br>Nexus may expose **read** status (`build_status`, `package_status`/`list`) only. |
| 6 | **`eval`, `eval_file`, `run_script`, `batch` (agent-authored), `menu`, `reload_file`** | **DIRECT — never delegated** | See §5. |

## 5. Explicit statement on `eval` / `run_script` / `batch`

- **`eval`, `eval_file` and `run_script` will never be routed through, wrapped by, or forwarded from Nexus**, in any arm (S5, S6-domain, S6-curated, S6-router), and not behind a gate either.
- Reasons:
  1. They are arbitrary code execution in the Editor process. Unity says so itself (`RunScriptCommand.cs:35`). No Nexus pre-validation of the C# text can be sound.
  2. Pipeline offers no hook to observe them before execution (§1). So a Nexus "eval gate" would be bypassable by `unity eval` and would mislead users into believing a gate exists (R5, D-11 honesty).
  3. G3 / CH2-6 already rejected an eval forwarder, and nothing in Pipeline 0.7 changes those premises.
  4. Under a façade with one manager tool, a forwarded eval would inherit the read tools' permission grant. That is the worst case for client approval granularity.
- **`batch`:**
  - An **agent-supplied** `operations` list is a composition language. It can include non-Undo asset/settings writes with `transactional:false` (`BatchCommand.cs:126-135`). Forwarding it bypasses every per-category verdict above. Keep it DIRECT.
  - The only allowed use is internal: a Nexus-constructed transactional batch from Nexus's own validated, closed op set (C-U).
- `menu` (arbitrary menu items, modal dialogs) and `reload_file` (hot-patch code) are classed with eval. They stay DIRECT.
- **G8 policy-layer idea:** Pipeline 0.7 has **no** pre-dispatch hook (answers the P0-10 question for 0.7.0-exp.1). A studio policy layer over `eval` is therefore impossible without reflection or an upstream change. Track CAT-2509 ("shared safety policy", referenced in 3 Pipeline files) as the upstream signal.

## 6. Conditions summary (prerequisites for any write delegation, category 1 first)

| ID | Condition | Status |
|---|---|---|
| C-T | Nexus token file chmod 600 (match Pipeline `FilePermissions`); constant-time compare; `shutdown_server` behind the token (D-1) | Not done (MEASURED 0644) |
| C-I | `request_id` cache + `outcome:"unknown"` + no auto-retry of forwarded writes | Planned (P0-04/05), not implemented |
| C-A | Read, write and destructive tools are separate; MCP annotations; `requiresUserInteraction` on destructive | Not designed yet |
| C-O | Resolve in Nexus → forward the `globalId` only → post-verify target identity | Design only |
| C-P | Canonical explicit `Assets/` paths, symlink-free, §6.8 deny list | Nexus has the pieces (`Utils.cs:179-274`) |
| C-V | Property-path deny list, object-ref values confined, size caps | Design only |
| C-U | Multi-op = one Nexus-built transactional `batch` | Design only |
| C-X | Transport = warm `unity mcp`/`shell` child or HTTP from the L1 adapter; never sync loopback from the Editor main thread | HYPOTHESIS (deadlock) to verify in the P0-16 spike |
| C-Q | Bridge on MCP ≥ 2025-06-18 (elicitation, `isError`, annotations) | D-6/D-7 open |
| C-D | Docs state that the façade is a guardrail, not containment; Unity-direct remains available to the agent | Policy |

## 7. Open items / UNKNOWN
- UNKNOWN: whether `unity mcp` (beta.10) forwards server-side tool annotations if Pipeline ever adds them (`CliCommand` has no such field in 0.7).
- UNKNOWN: Codex elicitation support; Cursor/Gemini per-call approval semantics for annotated tools.
- HYPOTHESIS to test in the spike: (b)-placement deadlock (C-X); whether `globalId` forwarding plus post-verify catches every re-resolution mismatch.
- Follow-up outside this research: Nexus `IsValidOrigin` accepts any loopback origin (looser than Pipeline). Recommend aligning with Pipeline (reject any `Origin`).
