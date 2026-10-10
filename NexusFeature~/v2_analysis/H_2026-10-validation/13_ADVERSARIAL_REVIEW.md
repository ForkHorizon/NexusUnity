# Adversarial review of `12_SYNTHESIS.md`

Written after the synthesis. The S-02 review was not in the tree when `12_SYNTHESIS.md` was written. This file does not add measurements. Labels name the supporting file, not a new run.

**Verdict.** NEXT #3 survives as a negative only: do not start S5, S6-P, Mixed, Gate F, or L-02. NEXT #1 survives only if narrowed to a scratch compile plus the Ignore-to-Fail probe, and it must not be read as proof that Nexus already fails with `CS0246` or that the drift lock is then closed. NEXT #2 survives only as two unrun release-note checks on a second project; one `unity recompile` transcript does not settle S-05, and a 0.8 shot does not retroactively prove Nexus captured Overlay here. The unsupported conclusion is Q9's "what got stronger" list, especially "a drift lock that actually ran," read as a locked contract, and Q8's "candidate overlap is Edit Mode full-frame overlay." Nexus-primary is not justified. Staying put is not a measured win for S4 or S5.

## 1. Correlation treated as causation

**MIXED. The payload and framing sentences are careful. The ranking sentences are not.**

Q4 says the JPEG, the file result, and the green cube are "a measured difference in payload and in scene framing. It is not a measured difference in task success." That blocks the worst reading. The cube is still not an independent quality win. `08_CAPTURE_DEATHMATCH.md` says Nexus called Frame/Aim and Unity has no target. The cube is in the Nexus frame because Nexus aimed. Q9 then lists "Scene framing. The green cube was in the Nexus frame and not in Unity's" under what got stronger. Cause: the aim call. Not shown: that an agent needed it.

Q5's table is the real leap. "Strongest current approach" for discovery, jobs, reload, compile, context, and ergonomics is assigned with no task success, no model, and no discovery arm. Tag-index "about fifty" rows (`02_UNITY_CLI.md`, **DOCUMENTED**), Funplay's 40, Coplay's routers, and Hatayama's 22 are counts. Counts did not cause a better agent in this sprint.

Q9's epochs line — "Readiness epochs, which Coplay's in-domain sequence still resets" — pairs a prior readiness suite (`00_BASELINE.md`, 17/17 EditMode) with Coplay source (`04_COMPETITOR_COPLAY.md`). `07_NEXUS_STRESS.md` says session-generation stability across a domain reload was **NOT RUN**, and the P0-02 20/20 live compile was not repeated. A resetting competitor counter does not measure that these epochs survived anything today.

The ~100 ms client time in Q4 sits beside "handler under 1 ms." `06_NEXUS_PERFORMANCE.md` says that floor is not handler cost and that one Unity capture returned in 49.4 ms. The synthesis does not make the false causal claim. It also does not carry the warning.

## 2. Competitor claims that are marketing or breadth counts

**DOCUMENTED claims, not measurements. Several "objectively better/worse" endings are catalogs wearing a verdict.**

The synthesis does not repeat Funplay's "Most Advanced," the one-sentence snake game, or "<1 min," and `03_COMPETITOR_FUNPLAY.md` already marks those as marketing. It also does not repeat Coplay's "10–100x" or "the wrong-tool rate drops measurably," which `04` marks as unmeasured. Good.

What it does import, in Q5, is the count: "Akiojin puts skills in front of 154 tools and `unity-cli mcp` republishes all of them. Funplay core is still 40. Coplay core is still action routers." That is the same move as the competitor endings:

- Funplay "objectively better": "Nexus's agent-facing contract is **three commands**" against a 40-tool core. The baseline `list_tools` check is 120 tools (`evidence/baseline/S02_ADVERSARIAL_REVIEW.md`, N-4). Three is the canonical trio, not the surface a client loads.
- Funplay "objectively worse": "Full catalog is 180 tools / 42 modules" against "Nexus's three commands." Breadth, stated as worse.
- Coplay "objectively better": "Nexus's three canonical commands are the opposite bet," with a caveat that the legacy catalog is not small. "Objectively worse" is the wired domain list plus ~14.7k stars. Stars are adoption, not a capture or compile result.
- Akiojin "objectively better": "a real small primary contract" versus 154 tools. The same file says the legacy list is still there. "Bounded look" adds "did this object look right," which this sprint did not score.

`03` also says Nexus "reads Game View Overlay off the framebuffer with a vertical flip." That is a source comment (`Editor/Commands/CaptureGameViewCommand.cs`: "Uses Capture V2 for Game View Overlay"), not the deathmatch. Official CLI "fit on one screen" (`02`) is a docs sentence. No token count was taken.

Spot-check, not a README: `NexusCommandCliDriftTests.CliArgsMatchCanonicalDescriptors` still `Assert.Ignore`s when `FindType("Unity.Pipeline", ...CliCommandAttribute)` returns null, and that return is before the "projection did not compile" assert. The S-02 review's SF-1 quote matches the file.

## 3. Unity changes not verified on a running Editor

**Most of Q1 is honestly DOCUMENTED. Two labels are hotter than the run.**

Not run on PID 4465, and the synthesis says so in places: Pipeline 0.8 was not loaded; CLI on the machine is `1.0.0-beta.10`; no `unity recompile` transcript; no beta.12 tag index; no Play Mode; no second Editor. Bind-to-`127.0.0.1`, `detail=tags`, public `CommandRegistry`, the nine `*_status` object returns, and `capture_game_view` defaulting to `source=screen` are tarball or post (`01_UNITY_PIPELINE.md`). The synthesis calling the 0.8 behaviour **DOCUMENTED** is stricter than `01`'s **CONFIRMED**-from-tarball. That part does not over-upgrade.

These do:

- "so play entry here still reloads" is stamped **CONFIRMED**. `09_JOBS_RELOAD.md` read the same `EditorSettings.asset` bits and marked the conclusion **INFERRED**, "not from pressing Play." The asset values can be confirmed. The reload was not. The synthesis's own "No Play Mode" line contradicts the **CONFIRMED** stamp.
- "`Unity capture cannot see Overlay in Edit Mode` is still true on 0.7 (**MEASURED** error string)." The measured string is that `source=screen` requires Play Mode (`08`). Both images lacked the red and blue bars. Camera capture missing the bars was **inconclusive**, not a pixel proof that Unity cannot see Overlay. The error string was upgraded into a visibility result.
- Nexus `CS0246` on 0.8 is **INFERRED** in the NEXT #1 evidence box (tarball, no forwarder, URP forum precedent). Q2's "P0-06's attribute move is no longer a forecast" is fair for the move, which `01` confirmed in the tarball. The NEXT #1 expected-value sentence is not fair: "an upgrade to the current Pipeline does not fail the package with `CS0246`" states the inferred failure as the thing the task prevents. URP's error is not this package's compile. This Editor was not compile-tested. Do not let "no longer a forecast" cover the Nexus error.

## 4. Benchmarks that favour Nexus

**MEASURED, on one Edit Mode Editor, and only as payload or framing.**

From `06` and `08`, not from a model: default look 768×407 JPEG, 12387 bytes, against a Unity camera PNG of 75456 bytes and about 100 KB on the wire; file-delivery JSON about 700 bytes and not imported under `Assets/`; warm file looks about 8–14 ms after a cold first shot; closed Game View returns `unavailable`, retryable, HTTP 200. The green cube is in the Nexus scene JPEG and not in Unity's scene PNG because Nexus framed it. Those are the Nexus-favouring numbers. They are not task success, and they are not 0.8.

## 5. Benchmarks that favour Unity

**MEASURED, and easy to drop because Q4 does not say "Unity was faster."**

`06`: Unity `source=camera` client times were 138.2 / 116.1 / 49.4 ms, and "Unity was faster and much larger" than the cold Nexus looks (about 347–428 ms). `08`'s comparison call was 1042 ms, so "faster" is the earlier trio, not every row. Warm Nexus file looks at about 9 ms beat that Unity trio. A single speed winner does not exist. Unity camera rendered `Main Camera` with no Game View; Nexus refused. Unity scene capture returned in 191 ms versus Nexus scene at 403 ms, on a different pose. Unity's Edit Mode screen source did not run (HTTP 400). No Unity token win was measured. The official tag index and `unity recompile` file/line list were not run, so they are not benchmarks.

## 6. Is the task set representative? Discovery was not run

**UNKNOWN for Q3. The draft task list is not a result.**

`11_DISCOVERY_RESULTS.md`: the protocol was not frozen and not executed. No semantic success, false success, wrong-tool rate, or tokens-per-success exists. Q3's "Nexus-primary is not supported and not refuted" is the only reading the files allow. Payload numbers cannot fill it.

The draft in `10_DISCOVERY_PREREG.md` would also not represent Gate F if it had been run. N=12 is a large-effect screen. The plan's Gate F power note, quoted in the draft, is on the order of 235–290 paired runs. The fixture is absent. DCAP assumes an Overlay the deathmatch did not reproduce. Plan S4 is a frozen 1.7.0 release; the draft's S4 is tip `bfdd0f7`. Those are not the same arm.

Refusing to run C1 versus S4 on beta.10 is correct as process and as Editor safety: the file says DRAFT, DO NOT RUN, and `11` says a live catalog dump can attach to the only Editor. It is not correct as science. The draft's own rule is that C1 versus S4 can still be scored when both U arms are NOT RUN, and that you do not install 0.8 to manufacture U-TAGS. "Running C1 versus S4 on beta.10 would measure the old flat catalog. That result cannot answer whether Unity's new tag index removed the reason for a Nexus-primary surface" swaps the tag-index question for the central hypothesis, which is whether fewer official tools reproduce the Nexus benefit on the catalog this product actually meets. Q3 admits the sprint does not know, then the experiment list says "Do not run it on beta.10." That ban is stronger than the evidence. This review does not approve running C1.

## 7. Are the token improvements only output compression?

**MEASURED as bytes. Not a token-economics result. Yes, the measured part is compression and file indirection.**

Q9: "JPEG 768 and file delivery. Measured against a 100 KB Unity PNG of the same view." The 12387-byte JPEG and the ~700-byte file JSON are smaller because the pixels are JPEG and, in file mode, not in the tool result. `06` says the figures are bytes/4 or base64 length, not a model tokenizer, and that task-level economics were not measured. The draft's `OUTPUT_LENGTH_ONLY` label exists so a shorter payload is not written up as a semantic layer. Q4's last sentence matches that. Q9's "got stronger" does not repeat it. There is no uncached-token ratio.

`base64_ms` of 6.6–9.2 ms against `encode_ms` of 1.4–1.5 ms is in `06` and `07`. Both say it matches "larger than encode" and does not re-prove a double count. The synthesis does not mention it. That attack does not land.

## 8. Is C1 a sufficient control, and was it run?

**UNKNOWN as a result. It was not run. It is not a sufficient architecture control even on paper.**

The draft says N=12 cannot confirm an architecture, cannot clear Gate F margins, and that `INSUFFICIENT` is an acceptable label. C1 is an allowlist of official names with `eval` / `run_script` / `batch` excluded. It is a probe of "fewer tools," not a control for epochs, JPEG, or reload. The allowlist is provisional. The synthesis does not claim C1 would have picked a façade. It does claim, by omission, that a beta.10 run would be empty. See §6. Do not treat "C1 was not run" as "C1 would not have mattered."

## 9. Does the evidence justify Nexus-primary?

**No. UNKNOWN on the hypothesis. The synthesis agrees in Q3 and then over-claims a strength beside it.**

"Nexus-primary is not supported and not refuted. Gate F stays unrun." That sentence should stay. Nothing in `06`, `08`, or `11` is an agent choosing the canonical trio over a shorter official list. Q6's skill text (Use when / Do not / file delivery / `error.code`) is a copy proposal, not evidence that a primary surface won.

Q9 over-claims the drift lock. Quote: "A three-command contract with a drift lock that actually ran (3/3, not ignored) on 0.7." The 3/3 is **MEASURED** (`00_BASELINE.md`: passed, skipped 0). "Actually ran" is true on this Editor. "Got stronger," under a heading that lists it next to the envelope and the JPEG, reads as "the contract is locked." The review that landed after the synthesis says the opposite while the suite stays green:

- MF-1: `max_logs` JSON equality does not lock the omit integer. Empty CS-error sets match.
- MF-2: format omit is `FormatOrDefault` in the test, not `ExecuteEnveloped`. HTTP can apply png and the test stays green. CLI pre-applies the helper.
- SF-1: a moved `CliCommandAttribute` `Assert.Ignore`s, and the message still says Pipeline is not loaded. Package CI does not install Pipeline and does not fail on skipped tests.

The synthesis already says "The drift lock's 0.8 behaviour is a weakness, not a strength. It ignores." That covers SF-1 only. It does not cover MF-1 or MF-2, which fail open on 0.7, where the test did not Ignore.

NEXT #1 should not be "only P0-06" if that means the holes stay green and the lock is then called closed. The holes should not be stuffed into the scratch-compile patch either. P0-06, as the plan and the synthesis define it, is a version-gated attributes reference, proved in a second project, plus Ignore-only-when-Pipeline-is-absent. That needs a project that is not PID 4465. MF-1 and MF-2 are a smaller test fix against the current sources. They do not need 0.8. Coupling them to the scratch Editor delays a fix that is already specified, and it lets "P0-06 done" mean "the lock is strict." SF-2 (short-name decoy) is not that fix.

## 10. Does the evidence justify staying with S4 / S5?

**It justifies not starting S5. It does not justify S4 as a winner, and it does not justify S5 remaining a build.**

No C1 comparison was scored. "Stay" is the residue of an unrun test, not a finding that the current surface beat an allowlist. Plan S4 is not this tip. The classification row "S5 façade | WAIT | No C1 comparison" is a hold, not an endorsement. This review does not approve S5.

L-02 "no caller yet" is a real scheduling reason, not a claim that the hole is fake. `09`: script compile still unloads the domain (**DOCUMENTED**), this project's play-entry flags still request a reload (**INFERRED** for the actual reload), `/api/job` is in-memory (**CONFIRMED** in the 0.7 cache), and the scheduled callers are L-04 and V-02b, which are not next. "There is no caller until L-04" in the classification table is the full sentence. NEXT #3's evidence line, "L-02 has no caller," drops the reload half. WAIT is not DROP. The short line is how WAIT gets read as DROP. The hole stays.

## 11. Which proposed implementation should not happen

**The S-05 grouper, if "one captured fixture" or the beta.11 note is treated as the design. Also any capture or lock patch that the pixels and the review do not support.**

Quote, classification table: "The design (group official `unity recompile`, do not scrape) still matches the beta.11 note. The payload was not measured. A 3-day build now can wrap the wrong shape."

The negative half matches the plan's v2.2 line: do not duplicate Unity's error listing. The beta.11 note, as `02` records it, is file and line, exit 0 / 6 / 7. It does not state column, diagnostic id, JSON shape, warnings, Safe Mode, or whether the verb is only a wrapper over Pipeline `recompile`. `01` says `recompile_status` on 0.8 returns an object; on 0.7 the source returns a string. One saved transcript of a 3-error file can falsify a guessed schema. It cannot settle grouping, SessionState persistence, ±3 lines, or the Safe Mode path. "Still matches" is too wide. Waiting for a transcript is necessary. Waiting for one transcript is not enough to start the 3-day command. Building S-05 from the note should not happen. This review does not approve S-05.

Also should not happen, on these files:

- A reference change on PID 4465, or a version gate shipped as if `CS0246` had been compiled.
- Deleting S-04 / S-07 / S-02 because 0.8's note mentions Edit Mode screen capture.
- A scene-orientation "fix." `08`: the edge-color swap is "too confounded to call it a bug." The synthesis does not call it a flip. `07` says the same. That attack does not land on the synthesis.
- A leak fix for +23.3 MB. `06` and `07` both refuse a leak verdict. No idle control. The synthesis never mentions the number. That attack does not land.
- `NexusJobStore`, `execute_code`, a 40/49/154-tool core, or a façade. The synthesis already lists those under do-not-copy. Leave them there.

## 12. Cheapest next experiment that could change the decision

**Not NEXT #2's full day, and not a discovery run.**

The drift-lock decision has already changed. `evidence/baseline/S02_ADVERSARIAL_REVIEW.md` is the experiment: read the test. Q9 cannot stay as written. No Editor.

The decision NEXT #1 still depends on is whether this package fails to compile. The cheap version is a scratch compile of the Pipeline asmdef against the 0.8 attributes layout, not capture and not `unity recompile`, and not PID 4465. If `CS0246` does not appear, the expected-value sentence is wrong and the task shrinks to the Ignore path plus whatever the compiler actually said. If it appears, the inference is confirmed and MF-1/MF-2 are still a separate test fix.

S-05 cannot be cheaply resolved here. Installed CLI is beta.10. `02` says `unity recompile` arrived in beta.11. Updating the CLI and attaching it to this Editor is the thing the sprint refused. A second project is the first legal transcript, and one transcript still does not authorize the command.

The only experiment that could change the architecture stay/go line is a frozen C1 versus S4 on the installed pair. It was not run. Running it would lock the only Editor, the prereg is a draft, and N=12 cannot pass Gate F. Refusing it this sprint is correct. Calling the result scientifically void is the duck. This review does not approve C1, S5, S6-P, Mixed, or Gate F.

## Objections the lead must address

Ranked. LANDS means the synthesis sentence is ahead of the file named.

1. **LANDS.** Q9, drift lock as a strength because 3/3 ran. File: `evidence/baseline/S02_ADVERSARIAL_REVIEW.md` (MF-1, MF-2, SF-1). Narrow NEXT #1. Do not fold the HTTP-omit holes into the scratch compile. Do not leave them out of the account.
2. **LANDS.** "The candidate overlap is Edit Mode full-frame overlay, and only in the 0.8 release note." File: `08_CAPTURE_DEATHMATCH.md`. The marker was absent from both images. Source comments claim Overlay. This shot did not show it. Q5's capture row, which says "Overlay was not reproduced," is the sentence to keep. The Q8 sentence still implies Nexus has the capability Unity is about to match.
3. **LANDS.** Play entry "still reloads" marked **CONFIRMED**. File: `09_JOBS_RELOAD.md`. **INFERRED**. No Play Mode.
4. **LANDS.** NEXT #1 expected value names `CS0246` as the failure an upgrade prevents. File: `01_UNITY_PIPELINE.md` and the synthesis's own **INFERRED** box. The attribute move is confirmed in the tarball. The Nexus compile is not.
5. **LANDS.** "Do not run it on beta.10" treats the central hypothesis as unanswerable on the installed pair. File: `10_DISCOVERY_PREREG.md` §2.2 and §0.1, against `11_DISCOVERY_RESULTS.md`. Process refusal stands. Scientific emptiness does not. Not an approval to run it.
6. **LANDS.** "The design still matches the beta.11 note." File: `02_UNITY_CLI.md` and plan S-05 (file/line/col/id, SessionState, hints, context). The note matches "do not scrape positions." It does not match the rest, and it was not captured.
7. **LANDS, narrowly.** NEXT #3 evidence: "L-02 has no caller." File: `09_JOBS_RELOAD.md`. The caller gap is real. The reload hole is also real. The classification-table sentence is the one that should have been used. Does not land as "they pretended the hole does not exist." The table says compile reload is still real.
8. **LANDS.** Q5 "strongest" rows that rest on 3 vs 40 vs 154. Files: `03`, `04`, `05` endings, plus N-4's 120-tool `list_tools`. Breadth is not a measured product win. `05` already caveats the legacy list. Q5's Why column drops that caveat.
9. **DOES NOT LAND.** Scene edge-color swap called a flip. The synthesis does not. `08` and `07` refuse the bug call. Game View matched Unity's camera within about 2 levels.
10. **DOES NOT LAND.** +23.3 MB called a leak. `06` and `07` say no idle control and not a leak verdict. The synthesis is silent.
11. **DOES NOT LAND.** `base64_ms` > `encode_ms` re-proved as a double count. `06` and `07` refuse that. The synthesis does not mention it.
12. **DOES NOT LAND.** Q3's "not supported and not refuted." That sentence matches `11`. The over-claim is the later ban and Q9, not this sentence.

## What this review did not read

Do not treat this as an audit of every page.

- `00_UPSTREAM_PRIMARY_SOURCES.md` was not opened. Upstream claims were taken from `01`, `02`, and `09`.
- `01_UNITY_PIPELINE.md` and `02_UNITY_CLI.md` were not read line by line. Heads, the attribute section, the capture and `recompile_status` rows, and the discovery / MCP sections were.
- `03`, `04`, and `05` were read at the better / worse / copy / do-not-copy endings, plus Coplay's performance and telemetry tables and Funplay's performance and security notes. Architecture, transport, and install sections were not read through.
- `09_JOBS_RELOAD.md` was read through the recommendation and the survival matrix, not re-checked against Pipeline source.
- Evidence JSON, JSONL, pytest, and the quality-gate log were not re-opened. Numbers above are the markdown's numbers. Deathmatch images were not opened.
- `MASTER_PLAN_V2.md` was read at the version header, F14, P0-06, S-05, L-02, L-04, and the Gate F pointer. The rest of the plan was not.
- `CaptureGameViewCommand` was read only at the Overlay comment. `GameViewCaptureSource.CopyCropped` was read. The flip math and `FormatOrDefault` were not re-derived past the drift test and the S-02 quotes.
- The Funplay, Coplay, and Akiojin clones were not opened. One Nexus source check replaced a competitor README spot-check.
- The handoff, `graphify-out`, and the rest of `NexusFeature~/v2_analysis/` outside this folder were not read.
