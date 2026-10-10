# S-05 input contract

Source: the R2/R3/R4/R6 captures in `03_RECOMPILE_CONTRACT.md`. This file does not authorize an implementation. S-05 was not started.

## Official primitive, measured

`unity recompile` on CLI beta.12 + Pipeline 0.8, when the call returns a populated error array (R2, R3), already returns:

- file
- line
- column
- compiler code (`CS0103` in the fixture)
- message separate from the raw line
- raw line
- error count
- order matching source order for three errors in one file
- `failed` and `compilationFailed`
- `status` of `completed` or `up_to_date`

`recompile_status` is a JSON object. On 0.8 it is not a JSON string. Its `errors` values are raw strings. It does not repeat file, line, column, and code as fields.

## Not in the captured payload

- assembly name
- diagnostic id
- a warning entry for CS1030 in any saved JSON. The editor log does contain `warning CS1030` at `Warn.cs(2,10)` (`evidence/contracts08/editor-warning-cs1030.txt`). The `unity recompile` call beside that compile returned HTTP 401, so this run has no successful recompile body that omitted the warning
- a proven `--strict` failure on that warning (the strict call ran only after status was already clean)
- one meaning for exit 6. Saved bodies are a network error, populated `data.errors`, and HTTP 401
- a server-side duration for the no-change compile. CLI wall time for `up_to_date` was 1017.7 ms

## Nexus semantic value still not provided by that primitive

- grouping by root file across the console, which is the current `nexus_group_compile_errors` behavior
- source context around the line
- did-you-mean
- the canonical Nexus envelope
- a hot-reload or Safe Mode explanation
- a result that is still available when the triggering CLI call dies and only a later status object remains

S-05 can consume the CLI's structured errors when the call returns them. It still has to define exit 6 as three saved shapes: a network error, populated `data.errors`, and HTTP 401. R1's JSON does not name a domain reload. That split is part of the command's input contract. Do not start the command from this file.
