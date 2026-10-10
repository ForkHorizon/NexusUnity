# Synthesis

Decisions below use the compiler log, the 0.7 and 0.8 drift XML, the CLI recompile JSON, and the pixel table. They do not treat a release note as a measurement.

## Decision table

| Item | Call | Evidence |
| --- | --- | --- |
| P0-06 | BUILD NOW | Unchanged Nexus fails CS0246 on Pipeline 0.8. The dual reference compiles on these two editors and the drift suite passes on both. Commit `596d171`. 0.8 did not repeat envelope, registry, or capture-look. `01D` did not stamp the commit. |
| S-02 test holes | BUILD NOW | MF-1, MF-2, FP-2, FP-3, and FP-4 are closed and committed (`5ecc7fb`). Residuals remain: a discarded `ReadErrorLogs` list, view/delivery not snapshotted, `max_logs: 0`, the host CLI parser, and the short-name attribute decoy. |
| S-05 | TEST FIRST | R2 and R3 return file, line, column, code, and message for `CS0103` in one file. Exit 6 is also a network error and an HTTP 401. CS1030 is in the editor log and absent from the saved JSON. Do not ship a command on that partial contract. |
| S-06 | WAIT | No new routing measurement. The discovery arms did not run. |
| S-03 | WAIT | No new evidence in this sprint. |
| L-01 | WAIT | No new evidence in this sprint. |
| L-02 | WAIT | R1 is exit 6 with a network error, then a clean status object. The JSON does not say domain reload. The in-flight call can still fail while status is later clean. |
| L-03 | WAIT | No new evidence in this sprint. |
| L-04 | WAIT | No new evidence in this sprint. |
| C-04 | WAIT | No new evidence in this sprint. |
| S-10 | WAIT | No new evidence in this sprint. |
| S-11 | WAIT | No new evidence in this sprint. |
| S5 | WAIT | Not tested. Discovery did not run. |
| S6-P | WAIT | Bench-only candidate was not run. It is not a production design. |
| Mixed | WAIT | Not tested. |
| Gate F | WAIT | This sprint's experiment was the cheap falsification, and even that did not run. |
| S-07 removal | DROP as a deletion | JPEG and `Temp/NexusCapture` without `.meta` are still different from official PNG under `Assets/Captures`. Overlay parity is unknown: one scene shows some magenta on `source=screen` and none on `source=camera`, and the Nexus images are not those bytes. Scene framing was not shown, and the two scene images disagree vertically. Nothing was deleted. |

## Ranked next actions

1. Restart the scratch editor with `m_Port` 7810 and run the frozen discovery prereg. Do not use port 7800. The two product commits are `5ecc7fb` and `596d171`. They are not pushed.
2. Add an S-05 fixture for "exit 6 because the socket dropped" versus "exit 6 because `data.errors` is populated", and for a warning that is actually inside the `unity recompile` call.
3. Repeat the scene-view framing capture with a scene camera aimed at the marker. The CLI `target=CenterGreen` call did not reach the command. Record framing as unproven until that rerun.
4. If the drift suite is touched again, snapshot view and delivery, send `max_logs: 0`, and stop treating a discarded `ReadErrorLogs` list as success.

## Response to 09

`09_ADVERSARIAL_REVIEW.md` was written before `06_DISCOVERY_PREREG.md` landed. The prereg is on disk now, frozen, with no scores. The review's other credible limits are adopted above: two editors are not every 0.7 and 0.8 build, 0.8 reran three drift tests, exit 6 has three saved bodies, CS1030's evidence is the editor log excerpt rather than a recompile JSON, official screen omit matches official screen bytes, Nexus images do not, and discovery was not run because the scratch editor was left on port 7800. The required omit-path holes stay closed. The residuals in the S-02 row stay open. No architecture was selected.
