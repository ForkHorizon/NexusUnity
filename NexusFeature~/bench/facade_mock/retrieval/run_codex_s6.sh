#!/bin/zsh
# usage: run_codex_s6.sh <tag> <catalog json> <task>   (S6 surface: ONLY a stub `nexus-unity` serving the catalog file; Unity hidden)
R=${0:A:h}; TAG=$1; CAT=$2; TASK=$3; WD=${CODEX_RUN_DIR:-/tmp}; LOG=$R/codex_runs/stub_calls_$TAG.jsonl; : > $LOG
codex exec --json --skip-git-repo-check --sandbox read-only -C $WD \
 --disable apps --disable plugins --disable hooks --disable browser_use --disable computer_use --disable tool_suggest \
 ${=MODEL_ARGS} -c 'mcp_servers.node_repl.enabled=false' -c 'mcp_servers.unityMCP.enabled=false' -c 'mcp_servers.nexus-unity.default_tools_approval_mode="approve"' \
 -c "mcp_servers.nexus-unity.command=\"python3\"" -c "mcp_servers.nexus-unity.args=[\"$R/stub_mcp.py\",\"file:$CAT\",\"$LOG\"]" \
 "A Unity Editor for my project is reachable only through your MCP tools (do not read files or run shell commands). Task: $TASK Find the right tool, call it once, then reply with just the tool name and arguments you used." \
 < /dev/null > $R/codex_runs/$TAG.jsonl 2> $R/codex_runs/$TAG.stderr.txt
