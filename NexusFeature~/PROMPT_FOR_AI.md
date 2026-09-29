# Unity MCP Tools Benchmark — Executor Brief

You are running a controlled latency/capability benchmark of Unity Editor automation
tools on this Mac. You have terminal access. Work autonomously, but STOP and ask me
whenever a manual Unity Editor action is needed (I will click in the Editor myself).
Never guess or fabricate results: a failed measurement is recorded as a failure, and
you continue with the next scenario.

PROJECT_ROOT = /Users/daliys/Daliys/UnityProjects/NexusMetricsTest   # this Unity project; Nexus Unity is already installed
PROTOCOL = /Users/daliys/Daliys/NexusFeature/BENCHMARK_PROTOCOL.md   # read it FIRST, it defines everything
NEXUS_RAW_HARNESS = /Users/daliys/Daliys/NexusFeature/bench_nexus.py # raw JSON-RPC harness, adapt/reuse it

## Why this benchmark exists
Nexus Unity is my own open-source Unity automation tool (JSON-RPC server in the Editor
+ Python MCP bridge). An informal measurement said MCP tools take 3-10s per call while
the official Unity CLI takes ~200ms. Before a month of rework I need to know WHERE the
time goes: transport? Python bridge? main-thread dispatch? compile/domain-reload?
And how the 3 competitors below compare on identical scenarios. The protocol file
(BENCHMARK_PROTOCOL.md) defines scenarios C1-C12, metrics, and decision rules — follow it.

## Tools under test (in this exact order)
1. Nexus Unity — already installed in this project (git-URL package; sources will be
   under Library/PackageCache/com.forkhorizon.nexus.unity@*/Editor/). Raw server port 8081,
   auth token file: /Users/daliys/Daliys/UnityProjects/NexusMetricsTest/Library/NexusUnityAuthToken.txt (read it, never print
   it in full, never commit it).
2. funplay-unity-mcp — UPM git URL: https://github.com/FunplayAI/funplay-unity-mcp.git
   (HTTP MCP server inside the Editor; per-project port shown in its window).
3. CoplayDev/unity-mcp — UPM git URL: https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity
   (Unity plugin + local Python MCP server started per its README; MCP port 6500).
4. IvanMurzak/Unity-MCP — install exactly per its README (openupm package
   com.ivanmurzak.unity.mcp or `npx unity-mcp-cli install-plugin`); streamableHttp transport.

For each competitor: fetch its README first and follow its official quick start. Do not
improvise installation steps.

## Phases

### Phase 0 — Environment + workspace (no Unity needed)
- Record environment: Mac model (`sysctl -n hw.model`), chip, macOS version, Unity version
  (ProjectSettings/ProjectVersion.txt), versions of all relevant packages (manifest.json + lock).
- Create bench/ (put all scripts there; add to .gitignore) and results/ folders in PROJECT_ROOT.
- Read the protocol end to end. List any scenario you cannot implement and why.

### Phase 1 — Nexus raw JSON-RPC (Unity open, Nexus server started)
- ASK ME: open this project in Unity and start the server (Window > Nexus Unity > Start Server).
  Verify: token file exists; get_server_status answers. If 401/no token — stop and tell me.
- Run/adapt bench_nexus.py (pass PROJECT_ROOT as argv[1]). N=10 reps per method.
  If a method name or param mismatches, verify against the package source and fix the
  HARNESS (never the package); log every fix you make.
- Add the missing protocol scenarios in the same harness style: C1 (initialize+list_tools),
  C8 (capture_game_view_screenshot), C10 (run_tests + get_test_results until done),
  C12 QA cycle (toggle_play_mode -> click_object_in_game or simulate_mouse ->
  capture_game_view_screenshot -> read_logs -> toggle_play_mode off). Use exact method
  names from the package source. Script-write scenario C7: Nexus requires confirm:true
  on .cs writes; write a trivial script, measure (a) time-to-accepted, (b) time-to-compiled
  (poll wait_for_editor_idle / is_asset_import_idle), then delete the script + .meta.

### Phase 2 — Nexus via MCP bridge (bridge-overhead metric G)
- Locate nexus_unity_bridge.py (package folder in Library/PackageCache, or deployed to
  project root; find it with `find`). Speak MCP over stdio from a Python adapter:
  spawn the bridge subprocess, JSON-RPC initialize -> tools/list -> tools/call, strictly
  one request at a time, measure send->response wall time.
- Repeat every C-scenario that has a mapped unity_* bridge tool; record the mapping
  (scenario -> bridge tool name). G = bridge latency - raw latency, per operation.
- Save the full tools/list JSON: tool count, per-tool schema size, total chars,
  approx tokens (chars/4). This is the "what does the agent actually see" metric.

### Phase 3 — Competitors, one at a time (identical scenarios)
For each competitor, in order 2,3,4:
- ASK ME to fully quit Unity, then add the package to Packages/manifest.json (or run its
  official installer), then ASK ME to reopen the project and start its MCP server window
  (give me the exact menu path from its README). funplay: ask me to paste the server URL
  shown in its window. Coplay: start its Python MCP server yourself per README (uv/uvx).
- Drive it programmatically (stdio MCP or streamable HTTP from a Python adapter in bench/).
  Do NOT use an LLM/chat client for any measurement — only direct protocol calls.
- Run C1-C12 where supported; map each scenario to the tool's closest equivalent and
  record the mapping. Unsupported scenario = record "not supported" (e.g. execute_code
  C11 exists only in funplay). Same metrics: cold first call, warm p50/p95/min/max over
  N=10, ok%, payload bytes.
- C6/C7 cleanup: only delete objects/files the benchmark itself created.
- After each tool: write results/bench_<tool>.json, then ASK ME to quit Unity before
  the next install. If Unity hangs or reloads (domain reload), wait, retry once, record it.

### Phase 4 — Report
- Write results/BENCH_RESULTS.md:
  - one results table per metric family, rows = protocol §7 format:
    tool | scenario | tool_name_used | cold_ms | p50 | p95 | ok% | payload_B | notes
  - capability matrix tool x C1..C12 (supported + tool name / not supported)
  - Nexus bridge-overhead table (G per operation)
  - tools/list comparison: count, total schema chars, ~tokens (Nexus vs funplay core vs others)
  - findings mapped explicitly to protocol §8 decision rules (which fix wins first)
  - environment block; list of all harness fixes you made
- Keep all raw JSON outputs in results/. Do not modify Nexus package sources, competitor
  sources, or ProjectSettings beyond what installation requires.

## Hard rules
- Never fabricate or smooth numbers. Errors are data.
- Compare tools ONLY on warm values; cold recorded separately.
- One Unity instance at a time; never batch-install competitors together.
- Ask before anything destructive outside benchmark-created objects.
- If stuck >15 minutes on one tool (install fails, server won't start), record the blocker
  and move to the next tool — an install failure is also a result.
