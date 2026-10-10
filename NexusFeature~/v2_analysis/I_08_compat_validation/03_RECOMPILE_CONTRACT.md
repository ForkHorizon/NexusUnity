# Unity recompile contract on CLI 1.0.0-beta.12 and Pipeline 0.8.0-exp.1

Scratch editor Unity `6000.3.16f1`, project `/tmp/nexus-p08-harness`, CLI `/tmp/unity-cli-beta12/unity`. Raw JSON is in `evidence/contracts08/`. Exit 6 is three saved bodies in this folder: R1's network error, R2/R3's populated `data.errors`, and R4's HTTP 401 (also `r2_restore`, `r3_restore`, `r4_restore`). R1's JSON does not contain the words "domain reload." The later status object is clean. Treating R1 as a second measurement of L-02's reload mechanism is an interpretation of that network error plus a clean follow-up status.

## R6 — nothing to compile

`unity recompile` exit 0, CLI wall 1017.7 ms.

```json
{"status":"up_to_date","compiling":false,"failed":false,"compilationFailed":null,"errors":[],"warnings":[],"summary":{"errors":0,"warnings":0}}
```

The payload says nothing was recompiled. The 1017 ms is the CLI process, not a measured Editor compile interval. There is no `<50 ms` figure in this run.

## R1 — first recompile after the editor was already up

Exit 6, CLI wall 935.1 ms. Body: `COMMAND_FAILED`, `Network error: An error occurred while sending the request.` `recompile_status` immediately afterwards was an object, not a JSON string:

```json
{"status":"completed","failed":false,"errors":[],"compilationFailed":false}
```

The triggering call died. The status file says the compile completed with no errors. A client that treats exit 6 as "there are compiler errors" is wrong for this response.

## R2 — one error

`Assets/P08ProbeErrors/One.cs` line 2, `return missingName`. Exit 6, CLI wall 13459.9 ms (this was the first import of that file). `success: false`. One entry:

| field | value |
| --- | --- |
| type | `compile-error` |
| file | `Assets/P08ProbeErrors/One.cs` |
| line | 2 |
| column | 34 |
| code | `CS0103` |
| message | `The name 'missingName' does not exist in the current context` |
| raw | `Assets/P08ProbeErrors/One.cs(2,34): error CS0103: The name 'missingName' does not exist in the current context` |

`summary` is `errors: 1`, `warnings: 0`. `failed` and `compilationFailed` are true. `status` is `completed`.

`recompile_status` for the same moment is a smaller object. `errors` is an array of raw strings, not the structured objects above. Fields: `status`, `failed`, `errors`, `compilationFailed`.

## R3 — three errors

`Three.cs` methods A, B, C. Exit 6, CLI wall 2474.0 ms. Three structured entries, in source order, columns 22, codes all `CS0103`. No truncation at three. `summary.errors` is 3.

`recompile_status.errors` is the three raw strings in the same order.

## R4 — warning

The scratch editor log `/tmp/nexus-p08-logs/editor-08.log` contains the warning twice, at lines 1992 and 2007. The excerpt is `evidence/contracts08/editor-warning-cs1030.txt`:

`Assets/P08ProbeErrors/Warn.cs(2,10): warning CS1030: #warning: 'P08_WARNING_MARKER'`

The `unity recompile` call made while that file was new returned HTTP 401 `Unauthorized`, exit 6, empty data. That body is not a warning payload. The following `recompile_status` was `completed`, `failed: false`, `errors: []`, and it has no warning field. A later `--strict` call returned `up_to_date` with `warnings: []` and exit 0, after status was already clean.

So this run did not show `--strict` failing on CS1030, and it did not show a successful `unity recompile` response that omitted a warning from that same call. The warning is in the editor log. It is absent from the saved status and strict JSON. The CLI success payload has a `warnings` array; every saved one is empty.

## What the CLI adds on top of `recompile_status`

`unity recompile` turns each error string into `type`, `raw`, `file`, `line`, `column`, `code`, and `message`, and adds `warnings` plus `summary`. `recompile_status` keeps the raw strings. Neither payload in this run has an assembly name or a diagnostic id.

The in-flight CLI call can still die while a later status object is a clean `completed`. R1 saved that as exit 6 `COMMAND_FAILED` / network error. R4 and the restore calls saved exit 6 as HTTP 401. Those are not the same body as R2/R3's `data.errors`.
