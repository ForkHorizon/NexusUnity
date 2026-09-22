# Research artifacts

Unity ignores `~` folders. Nothing here compiles into production.

## Layout

| Path | Contents |
| :--- | :--- |
| `docs/` | Historical capture/architecture reports. Retracted claims live here only and are marked as superseded; none of these files defines current public behavior or performance. |
| `capture/` | Spike/validation C# moved out of `Editor/`. |
| `scripts/` | Benchmark/spike Python. Not package CI. |
| `notes/editor-tick-cadence.md` | Performance backlog: 3 ticks ≈ 300 ms idle vs 6–8 ms historical active Editor. |

Production Capture V2 is `Editor/Capture` DriverOwnedReadback. Do not call untracked research methods from `MCPServerMethods.Init`.
