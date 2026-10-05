# Smoke code state

The smoke process ran on branch `lab/architecture-evolution-v1` at git HEAD `9269ebc627ab4c48030051bc0350ff95645f5a41` plus uncommitted lab edits. Those edits, at smoke time, were the Codex isolated home, newline-delimited MCP replies, lab-server tool approval, token-count redaction, and a prompt that named every model-visible tool.

That prompt suffix was a fairness hole. The runner no longer appends tool names. This sqlite was not rewritten and was not re-run. Do not treat its token counts as a comparison of the two surfaces.

`runs.sqlite` identity records `lab_git` `9269ebc`. It does not record Unity, CLI, or Codex versions, and it does not record a worktree hash. `environment-after.json` is a probe taken immediately after the run. The fixture hash in the sqlite is the fingerprint from before validation and holdout notes were added. Later self-checks use a different fingerprint. The smoke rows were not edited to match.

Per-call `duration_ms`, `result_bytes`, and `ts` are null in this sqlite. Later runs copy them from one backend line per MCP call. This smoke was not repeated after that change.

Label remains `SMOKE_ONLY`. These four rows are not an architecture result.
