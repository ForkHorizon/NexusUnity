# R_clients — How much MCP tool-schema context do clients actually cost (as of 2026-10-02)

Labels: CONFIRMED = official docs / source code; REPORTED = forum / blog / issue / third party; UNKNOWN = not found.

---

## 1. Claude Code

- Tool search is **on by default**. MCP tools are deferred and found when needed through the `ToolSearch` tool. **CONFIRMED**: https://code.claude.com/docs/en/mcp (section "Scale with MCP tool search" / "Configure tool search")
- Only names are sent up front. The docs say "Only tool names and server instructions load at session start". Full schemas load only when ToolSearch returns them. "Claude Code doesn't impose a fixed per-server tool cap; the practical limit is your context window budget." **CONFIRMED** (same URL)
  - Observed in this session: the deferred list given to the model is just the bare tool names (e.g. `mcp__nexus-unity__unity_asset_manager`). There are no descriptions. **OBSERVED** (first-hand, not documented)
  - Exact token cost per deferred tool: **UNKNOWN**, as no official figure was found. Because only a name is sent, it should be roughly the token length of `mcp__<server>__<tool>` (an inference, not a measurement).
- `ENABLE_TOOL_SEARCH` values. **CONFIRMED** (same URL):
  - unset: all MCP tools are deferred. It falls back to loading them up front in three cases: Agent Platform (Vertex) models older than Claude 4.5, a non-first-party `ANTHROPIC_BASE_URL`, or Microsoft Foundry deployments hosted on Azure.
  - `true`: always defer.
  - `auto`: load up front while the deferrable definitions total less than 10% of the context window. Defer all of them once they reach 10%.
  - `auto:N`: the same, with a custom percentage.
  - `false`: load everything up front.
  - `CLAUDE_CODE_DISABLE_EXPERIMENTAL_BETAS` forces tool search off.
- Models: tool search needs `tool_reference` support, which means Sonnet 4.5, Haiku 4.5, Opus 4.5 and later. **CONFIRMED** (same URL)
- Ways to opt out of deferral:
  - Per server: `alwaysLoad: true`.
  - Per tool: `_meta["anthropic/alwaysLoad"] = true`.
  - Server `instructions` "help Claude understand when to search for your tools".
  - Tool descriptions and server instructions are cut off at 2,048 chars by default. `CLAUDE_CODE_MAX_MCP_DESCRIPTION_LENGTH` changes the limit.
  - **CONFIRMED** (same URL)
- `list_changed`:
  - Interactive sessions refetch tools, prompts and resources. `-p` mode and the Agent SDK refetch the tool list only.
  - A failed refresh keeps the previous tools. Before v2.1.214, a failed refresh emptied the list.
  - When a server finishes connecting mid-turn, its tool names are listed to Claude on the next request in the same turn.
  - **CONFIRMED** (same URL, "Dynamic tool updates")
- Tool-output limit: a warning appears above 10k tokens, and the default maximum is 25k (`MAX_MCP_OUTPUT_TOKENS`). **CONFIRMED** (same URL)

## 2. OpenAI Codex CLI

- Per-server config options. **CONFIRMED**: https://learn.chatgpt.com/docs/extend/mcp?surface=cli (redirect target of developers.openai.com/codex/mcp)
  - `enabled_tools` is an allow list. `disabled_tools` is a deny list, applied after `enabled_tools`.
  - Also available: `enabled`, `required`, `startup_timeout_sec` (default 10), `tool_timeout_sec` (default 60), per-tool `approval_mode` and per-tool `output_token_limit`.
- Deferred loading and tool search exist in the source. **CONFIRMED** (source):
  - The tool is named `tool_search` and returns 8 results by default. Source: https://github.com/openai/codex/blob/main/codex-rs/tools/src/tool_discovery.rs
  - `apply_mcp_tool_exposure_policy` gives MCP tools DEFERRED exposure when `search_tool_enabled`, which means `model_info.supports_search_tool` and the provider supports `namespace_tools`. The per-server `omit_tools_from` setting can opt a server out. Source: https://github.com/openai/codex/blob/main/codex-rs/core/src/tools/spec_plan.rs
  - The search is BM25 over name, description and parameter metadata. **REPORTED**: https://codex.danielvaughan.com/2026/06/25/codex-cli-mcp-tool-search-default-livemcpbench-retrieval-bottleneck-scalable-discovery/
  - That blog also says tool search became the default in v0.142.2 (2026-06-25). **REPORTED**, same URL.
  - What stays in context per deferred tool (names only vs. namespace descriptions): **UNKNOWN**. The OpenAI API docs say a deferred MCP server stays visible to the model through its label and description (see section 5).
- `notifications/tools/list_changed`: **not supported**. **REPORTED**:
  - Open feature request: https://github.com/openai/codex/issues/10105
  - Bug saying list_changed doesn't invalidate the deferred tool cache: https://github.com/openai/codex/issues/33266
  - Related bugs: https://github.com/openai/codex/issues/37417 and https://github.com/openai/codex/issues/35583
- Hard tool-count limit in the Codex CLI: **UNKNOWN**. None was found in the docs.

## 3. Gemini CLI

- `includeTools` is an allow list. `excludeTools` is a deny list and wins over `includeTools`. **CONFIRMED**: https://geminicli.com/docs/tools/mcp-server/
- Schemas are cleaned before use: `$schema` and `additionalProperties` are stripped. **CONFIRMED** (same URL)
- Lazy or deferred loading: none in the docs, and discovery happens when the server connects. **CONFIRMED (absence in docs)**.
  - A feature request for a tool search tool was closed as stale. **REPORTED**: https://github.com/google-gemini/gemini-cli/issues/17322
  - A commenter says the Gemini API itself lacks `defer_loading`/`tool_search` (googleapis/js-genai#1416). **REPORTED**: https://github.com/google-gemini/gemini-cli/issues/21823
- `tools/list_changed` **is handled**. It calls `refreshTools()`, even if the server didn't declare `listChanged`. **CONFIRMED** (source): https://github.com/google-gemini/gemini-cli/blob/main/packages/core/src/tools/mcp-client.ts
- Limits:
  - Users hit a Gemini API error: "At most 512 function declarations can be specified". **REPORTED**: https://github.com/google-gemini/gemini-cli/issues/25440
  - A "100-tool limit" was discussed, but it applies mainly to Antigravity, and Antigravity warns at more than 50 tools. **REPORTED**: https://github.com/google-gemini/gemini-cli/issues/21823
  - Hard cap in the Gemini CLI itself: **UNKNOWN**.

## 4. Cursor

- Dynamic context discovery for MCP (blog post dated 2026-01-06):
  - Tool descriptions are synced to a folder. The agent gets "a small bit of static context, including names of the tools".
  - In an A/B test it cut total agent tokens by 46.9% on runs that called MCP tools.
  - **CONFIRMED (official blog)**: https://cursor.com/blog/dynamic-context-discovery
- 40-tool limit:
  - It existed historically. **REPORTED**: https://forum.cursor.com/t/tool-limit-reached-on-mcp/75680 and https://forum.cursor.com/t/about-limitation-of-the-number-of-mcp-tools/107844
  - A user says there is no warning with 80+ tools on recent versions (≥2.3). **REPORTED, no staff confirmation**: https://forum.cursor.com/t/regarding-the-quantity-limit-of-mcp-tools/153432
  - The current official limit is **UNKNOWN**. The Cursor MCP docs page says nothing about it: https://cursor.com/docs/context/mcp
- `list_changed` is ignored. This is **REPORTED**:
  - Cursor CLI, threads from May 2026 with no staff reply: https://forum.cursor.com/t/161459
  - IDE: https://forum.cursor.com/t/mcps-tool-list-changed-not-picked-up/132363 and https://forum.cursor.com/t/mcp-client-update-tools/77294 (the workaround there is to touch the config file).

## 5. API-level tool search and accuracy numbers

**Anthropic API.** **CONFIRMED**: https://platform.claude.com/docs/en/agents-and-tools/tool-use/tool-search-tool
- Two variants: `tool_search_tool_regex_20251119` and `tool_search_tool_bm25_20251119`.
- `defer_loading: true` on a tool keeps it out of context until it is found.
- Each search returns 5 tools by default (`limit` can be 1–10,000). A request can hold at most 10,000 deferred tools.
- At least one tool must not be deferred.
- Deferring keeps the prompt cache intact.
- For MCP servers, `defer_loading` is set through `mcp_toolset.default_config`.
- The docs quote:
  - "Claude's ability to pick the right tool degrades once you exceed 30–50 available tools."
  - A 5-server setup takes about 55k tokens, and tool search typically cuts that by more than 85%.
  - Use tool search at 10 or more tools or more than 10k tokens of definitions.
  - Keep the 3–5 most-used tools non-deferred.

**Anthropic engineering post "Advanced tool use".** **CONFIRMED (official)**: https://www.anthropic.com/engineering/advanced-tool-use
- Example costs: GitHub 35 tools ≈ 26K tokens, Slack 11 tools ≈ 21K, and 58 tools ≈ 55K in total. Anthropic has seen 134K internally, and Jira alone is about 17K.
- Context use falls from about 77K to about 8.7K tokens (85% less).
- MCP-eval accuracy improves with tool search:
  - Opus 4: from 49% to 74%.
  - Opus 4.5: from 79.5% to 88.1%.

**Anthropic post "Code execution with MCP" (2025-11-04).**
- Token use fell from 150,000 to 2,000 (98.7%).
- It suggests a `search_tools` tool with a detail-level parameter: name only, name plus description, or the full schema.
- **CONFIRMED (official)**: https://www.anthropic.com/engineering/code-execution-with-mcp

**OpenAI API.** **CONFIRMED**:
- The `tool_search` tool with `defer_loading` and namespaces is supported only on gpt-5.4 and later. Docs: https://developers.openai.com/api/docs/guides/tools-tool-search
- On namespaces the docs say "our models have primarily been trained to search those surfaces" and recommend fewer than 10 functions per namespace (same URL).
- A deferred MCP server stays visible to the model through its label and description: https://developers.openai.com/api/docs/guides/tools-connectors-mcp
- `allowed_tools` restricts which tools of an MCP server are imported (same URL).
- From the function-calling guide (https://developers.openai.com/api/docs/guides/function-calling):
  - "Aim for fewer than 20 functions available at the start of a turn … soft suggestion. Use tool search to defer…"
  - `tool_choice.allowed_tools` restricts tools while keeping the prompt cache.
- Claim of a "128-tool API limit" across OpenAI, Anthropic and Google: **REPORTED only** (danielvaughan blog above). Not verified.

**Research papers:**
- RAG-MCP: tool-selection accuracy is 43.13% with retrieval vs. 13.62% with the full toolset. Prompt tokens drop by more than 50%. **CONFIRMED (paper)**: https://arxiv.org/abs/2505.03275
- "Less is More" (DATE 2025):
  - Narrowing the tools given to the model cuts execution time by up to 70% and power by up to 40%, and improves success rates. **CONFIRMED (paper)**: https://arxiv.org/abs/2411.15399
  - Example: Llama3.1-8B picks the right tool with 19 tools but fails with 46. **REPORTED** via a search summary and not checked against the PDF.
- "How Many Tools Should an LLM Agent See?" (arXiv 2605.24660). **CONFIRMED (paper)**: https://arxiv.org/abs/2605.24660
  - BFCL (370 tools): showing about 7 tools on average gives 90.3% coverage, vs. 90.8% when showing 50.
  - ToolBench (3,251 tools): a fixed top-5 gives 64.7% vs. 61.9%.
  - Downstream accuracy: 93.1% vs. 87.1%.
- OSWorld-MCP (158 tools): even the best models call tools at only a 36.3% rate. **CONFIRMED (paper)**: https://arxiv.org/abs/2510.24563
- Third-party figures: about 300–600 tokens per tool schema, and BFCL calendar accuracy falling from 43% to 2% when tools go from 4 to 51. **REPORTED, unverified**: https://getunblocked.com/blog/mcp-tool-overload/ and https://tianpan.co/blog/2026-04-09-tool-selection-problem-agent-tool-routing-at-scale

## 6. Unity `unity mcp` / `unity command` / skill

- Unity recommends shell commands over MCP. **CONFIRMED**: https://docs.unity.com/en-us/unity-cli/replace-mcp-server-unity-cli
  - Quote: "The `unity command` and `unity eval` commands drive the Editor directly, without MCP in between. They're faster and use fewer tokens than MCP. Use these commands if your agent can run shell commands."
  - MCP mode is kept for agents that "can't run arbitrary shell commands" or for "models that don't compose command lines reliably".
- `unity mcp` "exposes the commands of a connected Unity Editor as MCP tools". **CONFIRMED**:
  - Source: 0.1.0-beta.8 release notes, https://docs.unity.com/en-us/unity-cli/release-notes
  - Source: https://github.com/Unity-Technologies/skills/blob/main/skills/unity-cli/references/integration-advanced.md
  - Tool names such as `capture_game_view` and `capture_scene_view` appear as separate tools, which points to **one MCP tool per command**. That reading is an inference from the wording; no explicit statement was found.
  - The server declares `tools/list_changed` and notifies the client when the catalog changes, for example when the Editor starts or a recompile adds `[CliCommand]`s. **CONFIRMED** (same two sources)
- Filtering:
  - `--tools`, tool groups, or token-cost guidance for `unity mcp`: **UNKNOWN**, none was found.
  - `unity list` prints each tool's group, and `unity command` listings accept `--query`, `--tag`, `--group_by`, `--sort` and `--order`. **CONFIRMED** (release notes)
- Discovery is on demand through the shell. **CONFIRMED**: https://github.com/Unity-Technologies/skills/blob/main/skills/unity-cli/SKILL.md
  - The skill tells the agent to run bare `unity command` to "list the commands the Editor exposes".
  - It says "Command names are defined by the Editor, so run `unity command` (or `unity list`) to see the exact set — don't assume a name."
  - `unity list` shows name, description, group and parameter schema. `unity commands --grep <pattern>` / `--format json` covers the CLI's own commands.

## 7. Consolidated ("manager") tools vs. atomic tools

- **Anthropic, "Writing effective tools for agents"**: **CONFIRMED (official)**: https://www.anthropic.com/engineering/writing-tools-for-agents
  - Consolidate tools, e.g. `schedule_event` instead of `list_users` + `list_events` + `create_event`.
  - "More tools don't always lead to better outcomes."
  - Namespace tools with prefixes.
  - Offer a `response_format` concise/detailed enum.
  - Paginate or truncate output; Claude Code's limit is about 25k tokens.
- **GitHub MCP server**:
  - It consolidated tools into read/write tools with a `method` discriminator. Example: `issue_read` covers get, get_comments, get_labels and get_sub_issues. Changelog: https://github.blog/changelog/2025-10-29-github-mcp-server-now-comes-with-server-instructions-better-tools-and-more
  - Consolidating the Projects toolset into 3 tools saved "around 23,000 tokens (50%)": https://github.blog/changelog/2026-01-28-github-mcp-server-new-projects-tools-oauth-scope-filtering-and-new-features/
  - Static `--toolsets` plus `--read-only` remain. The README says "Enabling only the toolsets that you need can help the LLM with tool choice and reduce the context size": https://github.com/github/github-mcp-server
  - **Dynamic toolsets were removed** in May 2026 as tech-debt cleanup ("local-only — never offered by the remote server"): https://github.com/github/github-mcp-server/pull/2512
  - All of the above: **CONFIRMED**.
- **Playwright MCP**: **CONFIRMED (official README)**: https://github.com/microsoft/playwright-mcp
  - Opt-in capability groups via `--caps` (vision, pdf, devtools, …).
  - The README says coding agents "increasingly favor CLI–based workflows exposed as SKILLs over MCP because CLI invocations are more token-efficient".
- **Cloudflare "Code Mode"** (2025-09-26): **CONFIRMED (official blog), no metrics**: https://blog.cloudflare.com/code-mode/
  - It converts MCP schemas to a TypeScript API and has the LLM write code. The argument is that LLMs have seen far more real code than tool-call tokens.
  - It claims agents handle "many more tools, and more complex tools" this way.
- **Block's MCP playbook** (2025-06-16): **CONFIRMED (official eng blog)**: https://engineering.block.xyz/blog/blocks-playbook-for-designing-mcp-servers
  - "Design top-down from workflows, not bottom-up from API endpoints."
  - The Linear MCP went from 30+ tools to 2 (`execute_readonly_query`, `execute_mutation_query`).
  - The Google Calendar MCP was consolidated into a `query_database` tool.
- **OpenAI**:
  - Fewer than 20 functions per turn, and fewer than 10 per namespace. **CONFIRMED** (see section 5).
  - "Combine functions that are always called in sequence." **CONFIRMED**, same function-calling guide.

## Net takeaways (inference, not sourced fact)
- Tool definitions now cost almost nothing up front in Claude Code (names only, on by default) and Codex (deferred when the model supports it).
- Cursor sends names plus a lookup folder, so its up-front cost is also small.
- Gemini CLI still loads every schema up front, and Cursor and Codex ignore `list_changed`.
- So a server with about 160 tools still costs a lot on Gemini CLI, and on any client running with tool search off. Separately from context cost, selection accuracy drops once more than about 30–50 tools are loaded at the same time.
