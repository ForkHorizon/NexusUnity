# Backlog: Editor update cadence vs capture submit→done

Historical in-engine Capture V2 measurement (active Editor):

- submit→done ≈ 6–8 ms
- about 3 `EditorApplication.update` ticks

Stabilization persistent MCP + HTTP, same session, idle/focused Game View:

- submit→done p50 ≈ 297–299 ms
- exactly 3 `EditorApplication.update` ticks
- stall p50 ≈ 10–18 ms

This is **not** a Pipeline-specific tax. Both transports observe GPU done on the third Editor tick.

Possible future work (do not treat as architecture):

- Editor update cadence while idle
- Game View repaint
- `EditorApplication.QueuePlayerLoopUpdate`
- a non-blocking freshness trigger

Do not add `WaitForCompletion` to hide this.
