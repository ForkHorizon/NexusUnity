# Unity CLI discovery model — 2026-10-04

Forensic note for the Nexus intelligence sprint. Scope is the official Unity CLI and `com.unity.pipeline` discovery surface as published on 2026-10-04. This pass did not start an Editor, did not HTTP port 7800, did not run `unity command`, `unity mcp`, `unity shell`, or `unity list`, and did not pass `--project-path` at the harness. The local binary was not executed (`--version` / `--help` can start the resident auth broker). Its version was read from the Mach-O image.

Label key: **CONFIRMED** (primary source states it directly), **DOCUMENTED** (official docs or skill text), **MEASURED** (counted here from a file or page, not from a live Editor), **INFERRED**, **UNKNOWN**. Snapshots under `NexusFeature~/v2_analysis/F_facade_evidence/` are **HISTORICAL**.

## Versions

| Piece | What is current on 2026-10-04 | What is installed here |
| --- | --- | --- |
| Unity CLI | **1.0.0-beta.12**. Docs heading 30 September 2026. Forum announcement 1 October 2026. No beta.13 (or later) heading in the release notes, and the beta.12 thread’s later posts (through 2 October) do not announce another CLI. | **1.0.0-beta.10** embedded in `/Users/daliys/.unity/bin/unity` (mtime 2026-09-21). `unity.previous` mtime 2026-09-15. |
| Pipeline | **0.8.0-exp.1**, forum 28 September 2026. No 0.8.0-exp.2 / 0.9 announcement found. Unity staff estimate “out of experimental in approximately a month” (29 September) is not a GA date. | **0.7.0-exp.1** in `/Users/daliys/Daliys/UnityProjects/UnityTestForNexus/Packages/manifest.json`. |

- **MEASURED:** the only `1.0.0-beta.*` string in the installed binary is `1.0.0-beta.10`.
- **CONFIRMED:** Hub 3.22.1 (1 October 2026) bundles CLI 1.0.0-beta.12. A standalone install does not move just because the Hub did. Update path remains `unity self-update` (not run here).
- **DOCUMENTED:** there is still no stable 1.0.0 channel. Installers take the latest beta until a stable release exists.
- **UNKNOWN:** whether the CDN has a build newer than the published notes. The three guessed manifest URLs under `public-cdn.cloud.unity3d.com/hub/prod/cli/` returned 404. `unity self-update --check` was not run.

beta.12 versus installed beta.10, discovery-relevant only:

- beta.11 (22 September) added `unity recompile` (file and line, exit 0 / 6 / 7), `unity docs`, `.unitypackage` import/export, `unity watch` was already in beta.10, and `unity job wait` no longer reports success while a matching `*_status` command is still running.
- beta.12 (30 September / announced 1 October) is the discovery change: bare `unity command` lists tags. Also `unity commands --grep`, `unity skill disable|enable|list`, `unity status --until-ready`, `unity projects create --with-pipeline`, `notifications` on JSON when a newer CLI exists, and named-command retry while the Editor is briefly busy.

## Two catalogs, not one

**DOCUMENTED** in the current `unity-cli` skill (`integration-advanced.md`, fetched 2026-10-04 from `Unity-Technologies/skills` `main`):

1. **CLI binary tree** — `unity commands` (plural). Subcommands, arguments, and flags of the `unity` binary. No Editor. `--format json` is the machine-readable tree. beta.12 adds `unity commands --grep <regex>` over that tree plus the plugin catalog. This is not the Pipeline tool list. The docs reference still says `unity --help` is the authoritative list for the installed binary, including commands the web page has not caught up with.
2. **Pipeline / Editor catalog** — `unity command` (singular), `unity list`, and `unity mcp`. These talk to a running Editor (or a Player via `--runtime` / `--runtime-path`). The Editor defines names, parameters, and errors, so a CLI update is not required for a new `[CliCommand]`.

`akiojin/unity-cli` (Rust, crates.io 0.18.1 on 2 October 2026, “101 Unity Tool APIs”) is a third-party TCP tool. It is not this CLI.

## Current discovery model (beta.12 + Pipeline 0.8)

**CONFIRMED** in the beta.12 forum heads-up (1 October 2026):

> `unity command` with no arguments now lists the Editor’s command tags (about fifty, each with its command count) instead of 160-odd commands. Use `unity command --tag <tag>` to drill in. If a script reads the full catalog from `unity command --format json`, add `--detail full`. The tag index needs `com.unity.pipeline` 0.8.0-exp.1. Older packages still return the full listing.

That is the official replacement for the flat default dump. It matches the release notes and the skill’s listing section.

**DOCUMENTED** drill-down (skill flag table):

| Invocation | What it returns |
| --- | --- |
| `unity command` | Tag index. `--tags` is the default when no command name and no `--detail`. About fifty rows, each `tag` + `count`. `--format json` is a `tags` array, not the old full schema dump. |
| `unity command --tags` | Same index, explicit. Composes with `--tag` (tags inside a subtree) and `--query`. |
| `unity command --tag <tag>` | Commands carrying that tag. Tags are hierarchical (`assets`, `assets/import`). |
| `unity command --query <term>` | Substring on name, description, or tag. |
| `unity command --detail compact` | Compact rows. |
| `unity command --detail full` | The previous full catalog, in one response. |
| `unity command --group_by flat\|package\|tag` | Grouping. Underscore is required. Default `flat`. |
| `unity command --sort name\|package --order asc\|desc --offset N --limit N` | Sort and page. |
| `unity list` | Every registered tool: name, description, group, and parameter schema. Discovery only; it does not run a command. The skill does not say this became a tag index. |
| `unity command <name> …` | Runs that command. Listing flags are not listing flags once a name is present: they are forwarded as parameters. |

Traps in the same skill:

- `--tags` and `--detail` cannot be combined.
- A bare `--query` on a named command forwards boolean `true`.
- One sentence later in the same file still says the authoritative catalog is `unity command --format json` with every command’s full parameter schema. That contradicts the flag table and the beta.12 heads-up. **INFERRED:** the flag table plus the heads-up are the operational model; the “authoritative catalog” sentence was not fully updated. Not remeasured live.

Server side, Pipeline 0.8 **CONFIRMED**: `GET /api/commands?detail=tags` returns one row per tag with its command count. Combining it with `group_by` or `sort=package` is refused, and that refusal is specified to work only with the latest CLI.

**DOCUMENTED** but pre-change (17 September beginner guide): bare `unity command` was the full blueprint and `unity list` was the short table, “hundreds of built-in commands.” That guide is older than beta.12. “Hundreds” is prose, not a count.

Query flags themselves are older than the default flip. **DOCUMENTED** in the 12 August 2026 (beta.4) notes: `--query`, `--tag`, `--group_by`, `--sort`, `--order`, `--offset`, `--limit`, `--detail` were added then. beta.12 changed the no-argument default and added `--tags`.

## What this machine still does

**INFERRED** from the heads-up plus the installed versions, not from a live `unity command` (forbidden against the open harness):

- Installed CLI is beta.10, which predates the default flip.
- Installed Pipeline is 0.7.0-exp.1. The heads-up says older packages still return the full listing even to a new CLI.
- So the flat default dump is still what this Editor pair does. The tag index is a property of beta.12 + Pipeline 0.8, not of the running harness.

## Command counts

| Figure | Status |
| --- | --- |
| 154 tools in `F_facade_evidence/unity_list.json` (`data.count` and `data.tools`) and 154 MCP tools in `unity_mcp_tools_list.jsonl` | **HISTORICAL.** Plan F20 dates that measurement 2026-10-02 against Pipeline 0.7 / CLI beta.10. Recounted from the files on 2026-10-04. Not a live catalog. |
| “160-odd commands” and “about fifty tags” | **DOCUMENTED** approximations in the beta.12 announcement and release notes. Not an exact integer. |
| 156 unique command anchors in the published 0.8.0-exp.1 manual index | **MEASURED** from the fetched manual page (built-in names only, including runtime commands through `cleanup_codereload`). Not a live `tools/list`. Custom `[CliCommand]`s are extra. Difference versus “160-odd” is unexplained (**UNKNOWN** whether a few commands are undocumented or the “160” includes aliases). |
| Plan F15 “~162” on Pipeline 0.7 | September research figure. Same order of magnitude as the official “160-odd.” Not remeasured. |

Counts are of Pipeline commands (flat tools), not of CLI subcommands. Tags are a grouping of that same catalog, not a smaller product surface. The tag row’s `count` is how many `unity command --tag <tag>` will list.

## Schemas

- **DOCUMENTED:** `unity list --format json` is the schema dump (name, description, group, parameters). `--detail full` is the full command catalog. `--detail compact` and the tag index are the cheaper views. No official statement that output schemas exist; the skill and the 0.8 manual document parameters and a return type name (`SceneHierarchy`, `CaptureResult`, …), not a JSON Schema for results.
- **HISTORICAL:** the 154-tool snapshot’s tools have `name`, `description`, `group`, `parameters`. `get_scene_hierarchy` parameters were only `path`. `console` parameters were `tail` (default 100), `level` (default `log`), `since` (default -1), `since_session`. `capture_game_view` already had `source`, default `camera`, described as Play Mode only for `screen`.
- **DOCUMENTED 0.8:** input parameters are still authored with `[CliArg]`. beta.8 stopped the CLI from fetching a separate argument-name catalog (`UNITY_NO_PARAM_VALIDATION` does nothing). The Pipeline package binds arguments.

## Skills

**DOCUMENTED:**

- `unity-cli` is embedded in the CLI binary. `unity skill show` prints it without writing files or using the network (beta.9). `unity skill install <client>` writes it in the client’s native layout. Clients named in the current skill: `claude-code`, `claude-desktop`, `grok`, `cursor`, `windsurf`, `vscode`, `cline`, `codex`. Codex installs a real skill directory (`~/.agents/skills/unity-cli`); older CLIs merged the whole skill into `AGENTS.md`, which every Codex session paid for. `unity skill refresh` migrates that.
- A second skill, `unity-pipeline`, ships inside `com.unity.pipeline` under `Library/PackageCache`, which clients do not scan. `unity skill install <client> --local` copies it next to `unity-cli`. It is not installed user-globally. It versions with the project’s package.
- beta.12: every installed skill’s frontmatter is loaded in every session of the client that found it, whether or not the skill is used. Cost scales with skills installed. `unity skill disable|enable` renames `SKILL.md` to `SKILL.md.disabled`. `unity skill list` shows state. `refresh` does not re-enable a disabled skill.
- `unity setup claude` installs the Claude Code plugin (`Unity-Technologies/unity-agent-plugin`) which carries the same skills. The skill text says not to also `unity skill install claude-code`, or Claude Code loads two copies. `unity mcp configure claude-code` skips a second `unity mcp` registration if that plugin already provides one.
- Public skill repo `Unity-Technologies/skills` `main`, GitHub contents API on this date: 34 skill directories, including `unity-cli` and `unity-package-management`. The rest are domain skills (UI, tilemaps, audio, IAP, LevelPlay, localization, URP, navigation, multiplayer, and so on), not alternate discovery protocols. `unity-cli` was republished 1 October 2026 (`#106` / `#105`). The `unity-package-management` skill states the CLI does not manage UPM packages; that work is `UnityEditor.PackageManager.Client` via the Editor. Pipeline itself does ship `package_list|search|add|remove|resolve|status` in the 0.8 manual, so that skill sentence is about the CLI binary’s package commands, not about Pipeline.
- `--caller` and `--skill` are analytics labels. The public skill says an agent running the skill has no reason to set them by hand and they are inert when analytics are off. The agent-plugin copy of `SKILL.md` tells agents to pass `--caller plugin --skill <name>` on every `unity command`. Those two official texts disagree. Neither changes command behavior.

**DOCUMENTED** context-cost claim, not a token count: skill descriptions are paid per install, which is why disable exists. No official token figure for the skill text or for the tag index.

## MCP

**DOCUMENTED** (`integration-advanced.md`, release notes, and https://docs.unity.com/unity-cli/replace-mcp-server-unity-cli):

- `com.unity.ai.assistant`’s in-Editor MCP server is deprecated. `unity mcp` in the CLI replaces it. Third-party MCP packages are out of that deprecation.
- `unity mcp` is an stdio MCP server inside the `unity` binary. It exposes the connected Editor’s commands as tools. It starts even when no Editor is running. beta.10: the server declares tool-list change notifications and notifies the client when the catalog changes, so a session that connected before the Editor started is not stuck with zero tools. The skill names this `tools/list_changed`.
- `--instance host:port` was removed. The CLI discovers Editors itself because the per-instance bearer token is not carried by a bare host and port. Target with cwd or `--project-path`.
- `unity mcp configure <client>` registers it. `--server` defaults to `editor`. `--server issue-tracker` (also `unity bug mcp`, beta.11/12) is a different server for the Issue Tracker, not the Editor command catalog.
- Official guidance for shell-capable agents is still `unity command` / `unity eval` rather than MCP. MCP is for clients that cannot run a shell or do not compose command lines reliably.
- Codex configure relaxes Codex’s sandbox network policy so both `unity mcp` and a direct `unity command` can reach localhost.

**UNKNOWN:** any MCP `resources/list` or `prompts/list` for the Editor server. Nothing in the beta.10–12 notes or the skill describes resources or prompts. The documented MCP surface is tools plus `tools/list_changed`.

**INFERRED:** the beta.12 tag default is specified for `unity command`, not for `unity mcp` or `unity list`. Nothing published says `tools/list` became a tag index. A client that loads MCP tools can still receive one tool per Pipeline command. Not remeasured. The 154-tool MCP snapshot remains the last measurement, and it is 0.7-era.

## CLI to Pipeline

**DOCUMENTED:**

- `unity pipeline install` writes `com.unity.pipeline` into `Packages/manifest.json` from the UPM registry. `upgrade` moves an already-installed project only when the registry is newer and prints every skipped version’s summary (beta.12). `projects create --with-pipeline` adds it at creation (beta.12); failure to add the package still exits 0 with a warning.
- An Editor that is already open does not load a newly added package until it refreshes (typically when its window is focused). Until then `unity status` reports `STATUS_PIPELINE_LOAD_PENDING`, not “no Editor.”
- Safe Mode: the package does not load, so `unity command`, `unity list`, `unity status`, and `unity mcp` cannot connect. Recovery is fix compile errors and restart. `unity pipeline list --format json` exposes `data.summary.instancesInSafeMode` and `data.instances[].safeMode.detected`.
- Transport is local HTTP. 0.8 binds Editor and Runtime servers to `127.0.0.1` only (previously all interfaces, still token + loopback Origin). Dial the IP, not `localhost`. One bearer token; this note did not re-read the 0.8 security model beyond that heads-up.
- 0.8 breaking change: `[CliCommand]` / `[CliArg]` moved to `Unity.Pipeline.Attributes`. URP on 6000.7 hit compile errors against that move (forum, 28–29 September); Unity said the URP team was publishing a fix. Not verified in this harness.
- Player commands are listed on the Editor server in 0.8 with the `runtime` tag. `batch` still refuses them.
- `unity command <name>` on beta.12 waits inside `--timeout` while the Editor is briefly down (after `editor_play`, during reload) and resends only a command the Editor rejected without running. Failure code `EDITOR_NOT_READY` (exit 6).
- Warm path, skill text: a resident Editor answers `command` / `list` / `eval` in roughly 200–600 ms with no recompile and no domain reload. That figure is **DOCUMENTED**, not measured in this pass. The plan’s ~100 ms warm MCP and ~1.2–2.2 s cold `unity command` stay **HISTORICAL** (F22).
- Cold one-shot: `unity run <project> --command <name>` boots a batch Editor, runs one command, exits. A bare `unity run` without `--command` is batchmode and quits; it is not a resident server. A resident headless Editor is the Editor binary with `-batchmode` and without `-quit`. The skill says that process serves commands but is not listed by `unity status`.
- `unity status --until-ready` (beta.12, default timeout 300 s) blocks until state `ready`. While playing, JSON may include `frameCount` and `playerLoopTicking` when the package publishes them.

## Shell, jobs, sandbox

- **DOCUMENTED:** `unity shell` is one warm process; commands omit the `unity` prefix. `unity shell --protocol ndjson` is one JSON request per line and one JSON result per line, serial, for agents. Stdin in that mode is the shell’s, so secret-from-stdin flags do not see the caller’s pipe (beta.12 security fix). Trusted input only.
- **DOCUMENTED** since beta.4 (12 August 2026), and in the current skill as ordinary production use: `unity command <name> --detach` and `unity eval --detach` return a job id; `unity job status|wait|cancel`. Requires a Pipeline package with job support (present since 0.5, `POST /api/exec` with `"job": true`). `--result-only` cannot be combined with `--detach`. The plan’s “`--detach` / `unity job` is a hypothesis until P0-10” is stale.
- **DOCUMENTED** sandbox: beta.10 makes `unity status` say when a Pipeline discovery file exists but cannot be read (separate account). beta.10 also adds a sandbox hint on connection refusal for `unity command`, `unity job`, and `unity list`. beta.11: `unity status` and the Pipeline connection error recognize an OpenAI Codex sandbox on Windows, as they already did on macOS. The current skill still says the CLI does not yet distinguish a sandbox from a missing Editor with its own structured code, and that the human message is still the generic one — “interim guidance,” to be retired when a distinct code exists. **INFERRED:** the notes added a hint, not a separate machine-readable code the skill would accept as authoritative.
- **UNKNOWN:** whether beta.11 or beta.12 stops a sandboxed `unity command` from deleting `Library/Pipeline/.unity-pipeline-port`. That deletion is the plan’s F27 measurement (2026-10-03) on beta.10-era behavior. It is not mentioned in the beta.11 or beta.12 notes read here. Not retested.

## Capture, recompile, jobs, hierarchy filters

Official 0.8.0-exp.1 manual (https://docs.unity3d.com/Packages/com.unity.pipeline@0.8/manual/):

- **`get_scene_hierarchy` — CONFIRMED, still unbounded by depth/root/limit.** Only parameter is `path` (open scene; omit for the active scene). Each node carries `instanceId` and `hierarchyPath`. No `depth`, `root`, or `limit` in that reference. The 0.7 and 0.8 release posts do not add those parameters. `find_gameobjects` is a separate query (name / tag / component / path), not a bounded tree walk. F22’s claim that hierarchy has no depth/subtree/limit still matches the latest published manual.
- **`capture_game_view` — filters did land.** 0.8 default `source=screen` (Edit Mode works; overlay UI only exists in Play Mode). `source=camera` is the old off-screen camera path and never sees overlay canvases. `save_path` makes the result path-only (no inline base64) unless `include_inline_image=true`. `max_resolution` caps the inline image’s longest edge and does nothing for a path-only save. `width`/`height` default 1280×720, cap 4096. **HISTORICAL** 0.7-era snapshot: `source` default was `camera`, and `screen` was described as Play Mode only. The 0.8 heads-up matches that change. `capture_scene_view` has the same path-only / `max_resolution` contract. `capture_editor_element` and `capture_runtime_element` are selector captures, Unity 6000.7+ only.
- **`recompile`:** still the async Pipeline command; poll `recompile_status`. CLI `unity recompile` (beta.11) is the wrapper that lists compiler errors with file and line and exits 0, 6 (errors), or 7 (no Editor / dropped request). `--strict` fails on warnings. beta.12 does not add a depth-style filter to recompile output.
- **Jobs:** see above. beta.11 fixed `unity job wait` reporting success while the Editor’s matching status command (for example `build_status`) was still running. 0.8: the nine `*_status` commands return real JSON, not a JSON string.
- **Console:** 0.7 replaced `get_console_logs` with `console` plus `console_status`, added counts, `groundTruth`, `logType`, `since_session`, and a cursor `reset` when a `since` cursor is from another session. **HISTORICAL** parameters include `tail` default 100. 0.8 notes do not add a new console cap. The 0.8 console parameter table was not re-fetched; **UNKNOWN** whether defaults changed inside 0.8 without a release-note line.

## Context-cost claims

| Claim | Label |
| --- | --- |
| Plan: 154 MCP schemas ≈20.8k compact / ≈24.6k spaced tokens (cl100k), bare names ≈0.45k | **HISTORICAL** (F20, 2026-10-02). Not recomputed. |
| Plan: Codex code-mode dumped 25.7–29.3k tokens of tool listing over 154 Unity + 14 Nexus tools | **HISTORICAL** (F24). |
| Official: about fifty tag rows “fit on one screen”; the count on the row is the size of the next listing | **DOCUMENTED.** No token measurement. |
| Official: skill descriptions are loaded for every installed skill, every session | **DOCUMENTED.** No token measurement. |
| Official: `unity templates list --format json --brief` exists because full JSON can be hundreds of KB | **DOCUMENTED**, templates only, not the Pipeline catalog. |
| Warm Editor round-trip 200–600 ms | **DOCUMENTED** in the skill. Not measured here. |
| This pass | No new token **MEASUREMENT**. |

## Tool-discovery strategy, as published

For a shell-capable agent on the latest CLI and Pipeline 0.8, the skill’s order is:

1. Do not dump the catalog first.
2. `unity command` (tag index) or `unity command --query <term>`.
3. `unity command --tag <tag>` for one subtree.
4. `unity list` or `unity command --detail full` only when the parameter schema is required.
5. `unity commands --grep` when the question is about the CLI binary, not the Editor.
6. Domain skills (the 34 in the public repo, plus `unity-pipeline` when mirrored) tell the agent which commands to call. They are workflow guides, not a second tool index.
7. MCP remains the flat tool server for clients that cannot do that dance. `tools/list_changed` covers Editor connect/disconnect. No published progressive `tools/list`.

On this harness (CLI beta.10, Pipeline 0.7.0-exp.1) step 2 still returns the full listing, per the beta.12 heads-up about older packages.

## Stale Nexus plan assumptions

`MASTER_PLAN_V2.md` (written 2026-10-04 from September research) versus this pass:

| Plan claim | Now |
| --- | --- |
| F1: CLI is 1.0.0-beta.11 (2026-09-22) | Stale. Published CLI is beta.12 (30 September / 1 October). Installed binary is still beta.10. |
| Discovery of ~154 commands is what agents pay on `unity command` | Stale for beta.12 + Pipeline 0.8: the default is a tag index of about fifty rows, and the full dump is `--detail full`. Still true for Pipeline ≤0.7, including this harness, and still the documented behavior of `unity list` and, as far as the notes go, of `unity mcp`. |
| `unity command --tag` is a plan idea | It has been a real listing flag since beta.4 (12 August). beta.12 made it the second step of the default flow. |
| F3: `--detach` / `unity job` is only a forum hypothesis until P0-10 | Stale. In the official notes since beta.4 and in the current skill. |
| F15: beta.9–11, ~162 commands | CLI moved one beta past that. Command-count order of magnitude matches “160-odd” / 156 manual anchors. Not a live recount. |
| F20: 154 MCP tools, `listChanged: true` | The notification part matches later official text (`tools/list_changed`). The 154 is **HISTORICAL** for 0.7. No official 0.8 MCP recount. |
| F22: `get_scene_hierarchy` has no depth/root/limit | Still true in the 0.8 manual. |
| F22: capture is an unbounded screenshot with no useful bound | Partly stale. 0.8 `capture_game_view` defaults to `source=screen`, can return a path instead of base64, and has `max_resolution` for the inline image. |
| F27: sandboxed `unity command` cannot see the Editor and deletes the port file | The “cannot see” half was partly addressed in beta.10/11 (hint text, unreadable discovery file, Codex-on-Windows). The port-file deletion is **UNKNOWN** after beta.10; not in the later notes; not retested. |
| Beginner-guide model (17 September): bare `unity command` is the full blueprint | Stale after beta.12. |
| Competing with Unity’s own skill on generic Editor driving | The public repo now has 34 skills plus the package’s `unity-pipeline` skill, and beta.12 treats installed-skill frontmatter as a per-session cost. That is more official skill surface than a single `unity-cli` file. Not a product conclusion. |

## Does official progressive disclosure remove the need for a Nexus-primary façade?

No architecture recommendation. The evidence only supports this:

Progressive disclosure is real, and it is specifically the **shell listing** of Pipeline commands. On CLI 1.0.0-beta.12 with Pipeline 0.8.0-exp.1, the default `unity command` is a tag index of about fifty counts, not a 154- or 160-command schema dump. `--tag`, `--query`, `--limit`, and `--detail compact` are the official way to avoid the full catalog. That undercuts the assumption that every Unity CLI discovery turn must pay for ~154 flat commands.

It does not, on published evidence, do three other things the façade discussion also depends on:

1. It does not change `unity list` or, as documented, `unity mcp` `tools/list`. Those are still described as the full tool set with parameter schemas and as one MCP tool per Editor command. Clients that ingest MCP tools can still pay the flat catalog. The tag index is opt-in for the shell, and it is unavailable on Pipeline 0.7 (this harness).
2. It does not bound `get_scene_hierarchy`. The 0.8 manual still takes only a scene `path`. Tag discovery does not shrink an unbounded tree read. Capture did gain path-only output and `max_resolution`; hierarchy did not gain depth, root, or limit.
3. It does not come with a measured token cost. “Fits on one screen” and “skills cost per install” are official qualitative claims. The 20k-token schema figures and the Codex listing dumps remain September / 2 October measurements against the flat 0.7 catalog.

So official progressive disclosure removes the *necessity of the default shell dump* on the newest CLI+Pipeline pair. It does not, by itself, show that a flat MCP catalog or unbounded hierarchy reads have gone away, and it is not in effect on the installed beta.10 + Pipeline 0.7.0-exp.1 pair. Whether that is enough to drop a Nexus-primary façade is outside this evidence.

## Sources

- Release notes (page last updated about 2 October 2026; newest heading 30 September, 1.0.0-beta.12): https://docs.unity.com/en-us/unity-cli/release-notes
- CLI reference (`command` vs `commands` vs `list` vs `mcp` vs `job` vs `skill`): https://docs.unity.com/en-US/unity-cli/unity-cli-reference
- MCP replacement: https://docs.unity.com/unity-cli/replace-mcp-server-unity-cli
- beta.12 announcement, 1 October 2026, including the “160-odd” / “about fifty tags” / Pipeline 0.8 requirement: https://discussions.unity.com/t/unity-cli-1-0-0-beta-12-is-rolling-out/1738085
- beta.11, 22 September 2026: https://discussions.unity.com/t/unity-cli-1-0-0-beta-11-is-rolling-out/1737353
- beta.10, 16 September 2026: https://discussions.unity.com/t/unity-cli-1-0-0-beta-10-is-rolling-out/1736729
- Hub 3.22.1 bundles CLI 1.0.0-beta.12 (1 October 2026): https://discussions.unity.com/t/now-available-unity-hub-3-22-0/1737927
- Pipeline 0.8.0-exp.1, 28 September 2026 (`/api/commands?detail=tags`, loopback-only, `capture_game_view` default `source=screen`, attributes assembly move): https://discussions.unity.com/t/unity-pipeline-package-0-8-0-exp-1-is-available-now/1737832
- Pipeline 0.7.0-exp.1, 14 September 2026 (console truth, `wait`, runtime registration): https://discussions.unity.com/t/unity-pipeline-package-0-7-0-exp-1-is-available-now/1736536
- Pipeline 0.8 manual index, scene commands, capture commands, editor lifecycle: https://docs.unity3d.com/Packages/com.unity.pipeline@0.8/manual/index.html , https://docs.unity3d.com/Packages/com.unity.pipeline@0.8/manual/commands/scenes.html , https://docs.unity3d.com/Packages/com.unity.pipeline@0.8/manual/commands/capture.html , https://docs.unity3d.com/Packages/com.unity.pipeline@0.8/manual/commands/editor-lifecycle-and-observability.html
- Current skill (tag default, `unity list` schema dump, MCP, shell, sandbox interim text): https://github.com/Unity-Technologies/skills/blob/main/skills/unity-cli/references/integration-advanced.md and https://github.com/Unity-Technologies/skills/blob/main/skills/unity-cli/SKILL.md
- Skill directory listing (34 names), GitHub contents API `Unity-Technologies/skills` `skills/` on `main`
- Beginner guide, 17 September 2026, pre-change listing model: https://unity.com/resources/a-beginners-guide-to-unity-cli-and-the-pipeline-package
- **HISTORICAL** catalogs: `NexusFeature~/v2_analysis/F_facade_evidence/unity_list.json`, `unity_mcp_tools_list.jsonl`
- Installed CLI: strings in `/Users/daliys/.unity/bin/unity` (`1.0.0-beta.10`), mtime 2026-09-21
- Installed Pipeline: `Packages/manifest.json` pin `com.unity.pipeline` `0.7.0-exp.1` in the harness project
