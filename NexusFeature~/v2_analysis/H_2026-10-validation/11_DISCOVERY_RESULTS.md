# Discovery experiment — not run

STATUS: the protocol in `10_DISCOVERY_PREREG.md` is still a draft. It was not frozen and not executed.

## Why it was not run

The arm that would test current Unity discovery is U-TAGS. Agent B and the 2026-10-01 CLI notes agree: bare `unity command` lists about fifty tags only when the Editor has Pipeline **0.8.0-exp.1**. This harness is Pipeline **0.7.0-exp.1** and CLI **1.0.0-beta.10**. On that pair the full listing is still what `unity command` returns (**DOCUMENTED** by Unity, not re-dumped against PID 4465, because a live `unity command` against this project can attach to the only Editor).

Running C1 versus S4 on beta.10 would measure the old flat catalog. That result cannot answer whether Unity's new tag index removed the reason for a Nexus-primary surface. MCP `tools/list` is still documented as one tool per command even on the new CLI (**DOCUMENTED** in `02_UNITY_CLI.md`). An MCP-only experiment would still be the old question, and it would lock this Editor for the whole run.

N proposed by the draft is 12 scored runs per arm. C1 versus S4 alone is 24 runs. That is enough to see a large gap and not enough for Gate F. It was not started.

## What is already known without the runs

- Shell discovery on the latest pair is a tag index. **DOCUMENTED**, not measured here.
- This machine does not have that pair. **CONFIRMED** versions.
- MCP listing was not shown to have gained the same default. **DOCUMENTED**.
- No semantic-success comparison of C1 and S4 was produced this sprint. The hypothesis "fewer official tools explain the Nexus win" is **UNKNOWN** on current Unity.

Do not treat this file as a Gate F result.
