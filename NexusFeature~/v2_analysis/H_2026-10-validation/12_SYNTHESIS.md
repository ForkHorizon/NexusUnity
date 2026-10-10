# Synthesis — 2026-10-04 intelligence sprint

Lead synthesis. Architecture was not chosen. `MASTER_PLAN_V2.md` was not edited. No production commit. Tip remains `bfdd0f7` on `rework2/S-02`.

Evidence labels in the supporting files stand. This file does not upgrade DOCUMENTED claims into MEASURED ones.

## Q1. What changed in Unity since the plan

The plan's September research already named Pipeline 0.8 and CLI beta.11. What is now checked against the packages and the posts:

- **CONFIRMED** from the 0.8 tarball (`01_UNITY_PIPELINE.md`): `CliCommandAttribute` and `CliArgAttribute` moved to assembly `Unity.Pipeline.Attributes`. Namespace stayed `Unity.Pipeline.Commands`. No type forwarder. The attributes assembly already existed in 0.7 for code-reload attributes only, so a blind reference swap breaks 0.7.
- **DOCUMENTED** and not loaded into this Editor: 0.8 binds `127.0.0.1` only, `capture_game_view` defaults to `source=screen` and works in Edit Mode, nine `*_status` commands return objects instead of JSON strings, `/api/commands?detail=tags` exists, `CommandRegistry` is public.
- **DOCUMENTED**: CLI `1.0.0-beta.12` (2026-10-01) makes bare `unity command` a tag index of about fifty rows, only with Pipeline 0.8. Older packages still return the flat list. `unity recompile` (beta.11) lists file and line. `unity status --until-ready` exists and is Unity's ready flag, not Nexus epochs.
- **DOCUMENTED**: Unity 6.6 new projects do not domain-reload on play entry. **CONFIRMED** this project's `EditorSettings.asset` has both disable bits off, and the Editor is `6000.3.16f1`. **INFERRED** from those bits, not from pressing Play: play entry here still reloads. Compile reload is still documented through 6.6. No Play Mode was entered.
- **CONFIRMED** installed: Unity `6000.3.16f1`, Pipeline `0.7.0-exp.1`, CLI `1.0.0-beta.10`.

## Q2. Stale Nexus assumptions

- "Agents face 154 flat commands" is stale for a shell on beta.12 + Pipeline 0.8. It is still the MCP `tools/list` story, and it is still this harness.
- P0-06's attribute move is no longer a forecast.
- "Unity capture cannot see Overlay in Edit Mode" is only half-checked. **MEASURED**: 0.7 `source=screen` returns HTTP 400 and says Play Mode is required. **NOT a pixel proof**: the red/blue marker was absent from both the Nexus JPEG and the Unity camera PNG. Source comments say Nexus reads Overlay from the Game View texture. This shot did not show it.
- S-05 should not reimplement compiler-message listing if `unity recompile` already returns file and line. That output was not captured on this machine. The beta.11 note does not specify column, diagnostic id, warnings, Safe Mode, or JSON shape.
- L-02's acceptance line "must survive play-entry reload" is **INFERRED** true for this project's settings and **DOCUMENTED** false as a universal for 6.6 new projects. Play was not pressed.
- `recompile_status` on 0.7 is a JSON string (**CONFIRMED** in source). On 0.8 it is an object (**DOCUMENTED**). Callers that search the string form will break.

## Q3. Did Unity discovery weaken Nexus-primary?

For a **shell** agent on the latest pair, the default catalog dump is gone. That removes one argument for hiding Unity. It was not timed and not given to a model here. **DOCUMENTED**, not measured on this machine.

For an **MCP** client, the published behaviour is still one tool per command (`02_UNITY_CLI.md`). The discovery problem those clients had is not fixed by beta.12.

C1 was not run against S4. A smaller official allowlist might reproduce the Nexus benefit. This sprint does not know. Nexus-primary is not supported and not refuted. Gate F stays unrun.

Refusing to run C1 versus S4 on this beta.10 + 0.7 Editor was an Editor-safety and draft-protocol choice. It is not a finding that the central hypothesis is unanswerable on the installed pair. The draft says that comparison can be scored when the new tag arms cannot run. It was not scored. `13_ADVERSARIAL_REVIEW.md` does not approve running it.

## Q4. Compact semantic Editor truth

On this Editor, the three canonical commands are small and the failure envelope is real:

- Project map about 1.4 KB. Handler time under 1 ms is not the agent-visible cost: client round-trip on those samples was about 100 ms. One Unity camera call returned in 49.4 ms, so 100 ms is not a machine floor (`06_NEXUS_PERFORMANCE.md`).
- Empty compile groups about 400 B.
- Warm look after the first shot: about 9 ms, 768×407 JPEG, 12387 bytes. File result about 700 B. Base64 result about 17 KB (16516 base64 chars).
- Unity camera PNG of the same view: 75456 bytes, wire about 100 KB when inlined. Orientation matched Nexus within 2 RGB levels.
- Nexus scene look contained the green bench cube. Unity `capture_scene_view` did not.
- Missing Game View is `unavailable` + retryable, HTTP 200. Unity camera capture does not need a Game View.

That is a measured difference in payload and in scene framing. It is not a measured difference in task success.

## Q5. Who is strongest, by job

These rows are source-shape judgments. No model picked a tool. No task succeeded or failed. `list_tools` on this tip is 120 tools (`S02_ADVERSARIAL_REVIEW.md` N-4). "Three commands" means the canonical trio, not the catalog a client loads.

| Job | Strongest current approach | Why this is the pick |
|---|---|---|
| Discovery | Official CLI tag index on 0.8, for the shell. Hatayama's 22-command CLI, with MCP removed, for product shape. | **DOCUMENTED** counts, not a measured agent: Akiojin skills sit in front of 154 tools and `unity-cli mcp` republishes them. Funplay core is 40. Coplay core is action routers. |
| Jobs | Pipeline Temp receipts for recompile, packages, and async tests. Funplay's "do not replay" for a single pending call. | A general store does not exist in the tools that ship. `/api/job` dies on reload. |
| Reload survival | Split it. Token and port file for transport (Pipeline). SessionState for one workflow cursor. Do not resume the mutation. | Coplay's open issues are reconnect bugs. The reload hole in Nexus is real. L-02 still has no scheduled caller before L-04. |
| Capture | Nexus on this 0.7 Editor for JPEG size, file-outside-Assets, and target framing. | Pixel match vs Unity camera was measured. Overlay was not reproduced in either image. Do not call Overlay a Nexus win from this sprint. |
| Compile | Official `unity recompile` for the list, once its payload is captured. Nexus grouping is not built. | Building a second scraper is the stale plan. The beta.11 note is file and line only. |
| Context | Small typed results and paths for bulky bytes. Hatayama writes hierarchy to a file. Nexus project map is already small. | Funplay's 40-tool schema is a size counterexample, not a task result. |
| Agent ergonomics | A short skill with Use when / Do not. Not a 49-tool router. | Installers and client writers are a product, not the next patch. |

## Q6. Copy

- P0-06 as written: version-gated reference to `Unity.Pipeline.Attributes`, keep 0.7 compiling. Change the drift test so a loaded Pipeline that does not contain the attribute **fails**. Ignore only when Pipeline is absent.
- S-05 as a grouper on top of `unity recompile`, after one captured fixture of that command's real output.
- Skill text for the three commands: Use when, Do not, file delivery when the model does not need pixels, branch on `error.code`.
- The existing rule that an unknown outcome is not retried. Funplay and Coplay both got duplicate work from retries.

## Q7. Do not copy

- `execute_code` / `eval` as the way to keep the tool list small.
- A 40, 49, or 154 tool core, or an MCP adapter that lists all of it.
- Desktop screenshot fallback.
- Default-on telemetry.
- A general `NexusJobStore` before L-04 or verify needs one.
- Scene-view capture that flips canvas render mode.
- Unauthenticated bridges.

## Q8. What Unity has caught up to

Nothing shipped in S-04 / S-07 / S-02 should be deleted on 0.7 evidence. Edit Mode Overlay is not a measured Nexus capability in this sprint: the marker was missing from both images. The 0.8 note says screen capture works in Edit Mode. That shot was not taken. If it works as written, the unmeasured overlap is full-frame Edit Mode UI, and the measured differences that remain are JPEG size, file location outside `Assets/`, crop (not pixel-checked here), scene framing, and the envelope.

`group_compile_errors` as a log scraper becomes the weak half of compile truth once `unity recompile` is the lister. The command can stay as the grouper. Do not keep a second parser as the source of positions.

## Q9. What got stronger

Kept, because the files support them:

- One envelope with retryable and hint, against Funplay's mixed prose and Coplay's split advice strings. This is a contract comparison, not a task-success result.
- JPEG 768 and file delivery, as **bytes**. 12387-byte JPEG and about 700-byte file JSON, against an inlined Unity camera PNG of about 100 KB. That is compression and file indirection. No model tokenizer and no task-level token ratio were measured.
- Scene framing as a behaviour: Nexus aimed and the green cube is in the frame; Unity's scene capture has no target and the cube is absent. Not shown: that an agent needed the aim.

Withdrawn after `13_ADVERSARIAL_REVIEW.md`:

- "A drift lock that actually ran (3/3)" is not a stronger contract. The suite passed and did not Ignore on this 0.7 Editor (**MEASURED**). `evidence/baseline/S02_ADVERSARIAL_REVIEW.md` shows two false passes on that same green run: `max_logs` is JSON equality on the CS-error set (MF-1), and format is checked through `FormatOrDefault` rather than the HTTP apply path (MF-2). A moved attribute still Ignores (SF-1). Package CI never installs Pipeline.
- Readiness epochs were not shown to survive a reload today. The 17/17 suite passed. The 20/20 live compile was not repeated. Coplay's sequence resetting is their source, not a measurement that Nexus epochs won a reload.

## Q10. Three decisions for the next development sprint

1. Scratch-compile the Pipeline asmdef against 0.8 before changing references. The attribute move is confirmed. The Nexus `CS0246` is not.
2. Close MF-1 and MF-2 in the drift test on the current sources. That patch does not need 0.8 and must not be described as "P0-06 done, so the lock is strict."
3. Do not pick S5, S6-P, Mixed, or Gate F. Do not start L-02. Do not start S-05 from the beta.11 note. One saved `unity recompile` transcript is necessary and not sufficient.

## Classifications

| Item | Class | Why |
|---|---|---|
| S-05 `nexus_compile` | TEST FIRST | P0-02 is done. "Do not scrape positions" matches the beta.11 note (file, line, exit 0/6/7). Column, diagnostic id, warnings, Safe Mode, and the 0.7 string versus 0.8 object status shape were not captured. One transcript can kill a guessed schema. It does not authorize the 3-day command. |
| S-06 `nexus_diagnose` | WAIT | Safe Mode and compile truth should follow the measured `unity recompile` / Editor.log behaviour, not a new diagnostic product. |
| S-03 profiles | WAIT | It changes the default tool surface. Upstream discovery just changed. Do not reshuffle tools in the same sprint. |
| L-01 seams | WAIT | Those seams exist to host a façade. The façade is not decided. |
| L-02 jobs | WAIT | Compile reload is still real (**DOCUMENTED**). `/api/job` does not survive it (**CONFIRMED** in 0.7, byte-identical in the 0.8 tarball). Play-entry reload here is **INFERRED** from settings. No caller is scheduled before L-04. WAIT is not DROP. See `09_JOBS_RELOAD.md`. |
| L-03 snapshot | WAIT | Hierarchy is still unbounded in the 0.8 manual (`path` only, agent B). Useful later. Not ahead of the 0.8 measurement. |
| L-04 `nexus_run` | WAIT | Depends on L-02. |
| C-04 detail/tokens | WAIT | S-02 deliberately left defaults out of `list_tools`. |
| S-10 dump cleanup | WAIT | The replacement snapshot does not exist. Removing the dump without a replacement changes the agent surface. |
| S-11 discovery metadata | WAIT | An offline recall run on the old catalog does not test tag discovery. |
| S5 façade | WAIT | No C1 comparison. |
| S6-P | WAIT | Not supported and not refuted. |
| Mixed | WAIT | Same. |
| Gate F | WAIT | Stage A was not run. Do not start the confirmatory gate. |

## Ranked next actions

### NEXT #1 — Scratch-compile the Pipeline asmdef on 0.8

Class: TEST FIRST. Do not land the reference change in this repo until the compiler has spoken.

Expected value: know whether this package fails to compile when `CliCommandAttribute` lives only in `Unity.Pipeline.Attributes`. If `CS0246` appears, P0-06's version gate is the fix. If it does not, the task shrinks to the Ignore path and whatever the compiler actually said.

Evidence: **CONFIRMED** in the 0.8 tarball: types moved, namespace unchanged, no type forwarder. Nexus asmdef references `Unity.Pipeline` only. URP hit `CS0246` on the same move (**DOCUMENTED**). Nexus itself was not compile-tested. **INFERRED**.

Effort: hours if a second project already exists. Not the plan's 1–2 day implementation.

Dependency: a project that is not PID 4465. Do not change this harness manifest. Do not dial port 7800 from that project.

Risk: a hard reference to `Unity.Pipeline.Attributes` breaks 0.7, because 0.7's attributes assembly does not contain the CLI types. Do not "fix" that on the live Editor to learn the answer.

Why now: 0.8.0-exp.1 is the registry latest. The Ignore path is real in the test source. The compile failure is not yet a Nexus fact.

### NEXT #2 — Close the two drift-test false passes, separately

Class: BUILD NOW, test-only, on the current 0.7 sources.

Expected value: `format` and `max_logs` cannot move on the HTTP omit path while `NexusCommandCliDriftTests` stays green.

Evidence: `evidence/baseline/S02_ADVERSARIAL_REVIEW.md` MF-1 and MF-2. The suite already passed 3/3 on this Editor, which is why the holes matter. SF-1 (Ignore when the attribute leaves `Unity.Pipeline`) belongs with the 0.8 compile, not with this patch. Folding them together would let "P0-06 done" mean "the lock is strict."

Effort: a short test change. Run `NexusCommandCliDriftTests` on PID 4465 after the assertions exist. Independent review of the patch, then one commit. Not done in this sprint.

Dependency: none on 0.8. Does not require an architecture choice.

Risk: a stricter `max_logs` check that only compares empty consoles will stay green. The assertion has to observe the integer the omit path passes, or a fixture with more CS errors than the wrong limit.

Why now: the review is already written. Waiting for a second Editor does not make these two holes clearer.

### NEXT #3 — Do not start a façade, L-02, or S-05

Class: WAIT. Not DROP.

Expected value: the next sprint does not spend itself on a surface, a job store, or a compile command whose payload is still a release note.

Evidence: C1 versus S4 was not run (`11_DISCOVERY_RESULTS.md`). That hypothesis remains open on the installed pair; this sprint did not answer it and does not approve running the draft. L-02: compile reload is real, `/api/job` dies with the domain, and no caller is scheduled before L-04 (`09_JOBS_RELOAD.md`). S-05: file and line in the beta.11 note do not specify the rest of the planned command. `execute_code` is the competitor pattern not to copy.

Effort: zero code.

Dependency: before any S-05 implementation, capture `unity recompile` and 0.8 `recompile_status` on a second project, including a non-empty error file, and write down column, id, warnings, and Safe Mode. That measurement also covers 0.8 Edit Mode `source=screen` versus Nexus look. It is the experiment that gates S-05 and any S-07 claim change. It is not itself a build, and one transcript does not authorize the command.

Risk: WAIT gets read as DROP, and compile-reload survival for L-04 is forgotten. The classification table is the sentence to keep: the hole is real, the caller is not next.

Why now: this sprint's job was to stop the roadmap from continuing by number.

## Proposed master-plan delta (not applied)

Facts to add:

- Pipeline 0.8.0-exp.1 is published. Attribute assembly move is confirmed in the tarball. Namespace unchanged. No forwarder.
- CLI 1.0.0-beta.12 tag index requires that package. MCP `tools/list` was not shown to change.
- Unity 6.6 new projects skip play-entry domain reload. This repo's EditorSettings have the disable bits off (**CONFIRMED**). That play entry reloads was not observed (**INFERRED**). Script compile still reloads on 6.6 per the manual.
- 0.7 capture `source=screen` throws in Edit Mode. Nexus and Unity camera full frames matched orientation on one Edit Mode shot. Nexus scene framing showed a target Unity's scene capture missed because Nexus aimed. Overlay marker was not visible in either Edit Mode image.
- The S-02 suite can pass while HTTP `format` and `max_logs` omit diverge (MF-1, MF-2).

Tasks to move:

- P0-06 stays the compatibility task, and it starts with a scratch compile, not with a reference edit on this branch.
- A separate test-only patch closes MF-1 and MF-2. It is not part of P0-06.
- S-05 does not become next merely because one `unity recompile` transcript exists. The transcript is a gate, not the design.
- P0-17 / Gate F do not move earlier because the shell catalog got tags.

Scope changes:

- S-05: do not scrape `Editor.log` as the primary list unless Safe Mode blocks Pipeline. Do not treat the beta.11 file/line note as the schema. Record column, id, warnings, Safe Mode, and the 0.7 string versus 0.8 object status before writing the command.
- S-07 claims: do not say this sprint showed Overlay capture. JPEG size, file location, and scene framing were measured. Crop was not pixel-checked. Stop saying Unity cannot capture Overlay in Edit Mode until a 0.8 shot says otherwise, and do not claim Nexus did it in this session.
- L-02 acceptance: split "settings request a play-entry reload" from "6.6 new projects do not" and from "Play was pressed." Do not require a play-entry domain reload on a 6.6 fixture that has it disabled.

Drop: nothing. The façade options stay open because C1 was not run. Not running it is not evidence that C1 would lose.

New experiments, in order:

1. Scratch project, Pipeline 0.8, compile the Nexus Pipeline asmdef. Record the errors. Do not patch until that log exists.
2. On current 0.7, tighten MF-1 and MF-2 and re-run `NexusCommandCliDriftTests` only.
3. Same scratch project, CLI beta.12: `unity command` with no args, `unity command --detail full`, `unity recompile` on a 3-error file, default `capture_game_view` in Edit Mode versus Nexus look, with a red/blue overlay given a frame to render.
4. Only then decide whether `10_DISCOVERY_PREREG.md` is worth freezing. The installed pair can still host C1 versus S4 later. This sprint does not run it.

## Adversarial review

`13_ADVERSARIAL_REVIEW.md` was written by an agent that did not write the research. Objections 1–8 landed and are folded above. Objections 9–12 did not land: the synthesis never called the scene swap a flip, never called +23.3 MB a leak, never re-proved the `base64_ms` double count, and Q3's "not supported and not refuted" stays.

## What this sprint did not do

- No production fix. The 0.8 Nexus compile break is inferred. The attribute location is confirmed in the tarball. URP's `CS0246` is a forum report.
- MF-1 and MF-2 were not patched.
- No second Editor. PID 4465 was not killed and not upgraded.
- No Play Mode. No domain reload during capture. No 20-compile rerun of P0-02.
- Discovery experiment not run (`11_DISCOVERY_RESULTS.md`).
- `MASTER_PLAN_V2.md` was not edited. This delta is a proposal.
