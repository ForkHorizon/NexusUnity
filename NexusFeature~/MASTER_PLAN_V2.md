# NEXUS UNITY — MASTER PLAN v2

Version 2.6 · 2026-10-04 · Owners: Kiryl / Daliys (ForkHorizon)
Revised after an adversarial review (technical + strategy challengers, independent judge); see §15.
v2.2: aligned with two Unity research passes (what Unity ships now, what it is likely to ship next); see §14b.
v2.3: blind-spot review (Unity dropping domain reload, agent hooks, MCP 2026-07-28, headless agents, ToS, privacy, package footprint); see §16.
v2.4: façade hypothesis (Unity CLI as a hidden backend behind a compact Nexus API) researched and measured; decision deferred to a pre-registered spike + Gate F; see §17.
v2.5: corrective pass — v2.4 had narrowed the hypothesis to "S4 + selective reads"; the Nexus-primary / Unity-hidden-backend architecture (S6) is now a separate, untested candidate with its own arms, a pilot, and a re-built Gate F decision matrix; see §17.3.
v2.6: planning-only — adds an Architecture Theory Register (§18) and a cheap-falsification stage (P0-17) **before** the expensive confirmatory benchmark; Gate F becomes the final confirmatory gate for ≤3 surviving challengers against permanent comparators S4 and C1; separates architecture-independent work (continues) from architecture-dependent work (blocked); transport becomes its own question; see §17.5.
Horizon: 12 weeks (target start 2026-10-05) with two hard decision gates (week 4, week 9).
**Status: ACTIVE. Supersedes `MASTER_PLAN.md` (v1, 2026-09-01) and the forward-looking parts of
`ImageCapturePlan.md` (M0–M4 are done; M5 is replaced by §9 here).** Only one plan is active at a time.

Inputs: review of all `NexusFeature~` material, the repo at `ef13f8e` (re-checked at `46a00d7`/`f4d728f`; cited lines
still match), web research on Unity CLI / Pipeline (Sep 2026), four independent analyses kept in `NexusFeature~/v2_analysis/`:
- `A_build_on_unity.md` — what to build on top of Unity CLI (composites, speed, tokens)
- `B_skeptic.md` — what not to do, risks, kill list, process guardrails
- `C_agent_efficiency.md` — adoption, token economics, benchmark v2 spec
- `D_architecture_roadmap.md` — target architecture, method migration, roadmap, go-to-market
- `F_facade_hypothesis.md` (v2.4) — façade vs direct Unity: client tool-loading research, measured schema/output/latency costs, spike design; evidence in `F_facade_evidence/`

and two adversarial challenges of v2.0 (technical, strategy), judged in §15.

---------------------------------------------------------------------
## 0. КРАТКО ДЛЯ ВЛАДЕЛЬЦЕВ (RU)
---------------------------------------------------------------------

**Суть.** Unity бесплатно отдала агентам базовый доступ к Editor: транспорт, подключение клиентов,
базовые команды (включая скриншоты, иерархию, свойства компонентов), `eval`, тесты, билды, 31
официальный skill. Соревноваться там бессмысленно, и метафору «Unity — руки, Nexus — глаза» мы
убираем: у Unity тоже есть «глаза». Nexus позиционируется на проверяемом утверждении:
**«Nexus возвращает то, что Editor реально показывает и реально скомпилировал, — иерархию, значения
инспектора, состояние импорта, картинку с Overlay UI, ошибки компиляции — одним вызовом и до 1k
токенов, и доводит до конца запуск ваших editor-инструментов».**

**Главная проблема — агенты не вызывают Nexus** (в 4 ретроспективах реальных сессий: в 3 — ни
одного вызова, в 4-й — 2; это гипотеза на N=4, а не доказанный факт). Поэтому первые 4 недели —
честные замеры, дешёвые фиксы, доступность (skills с публикацией уже на 2–3 неделе, инструкции в MCP,
`unity command nexus_*`) и первые внешние тестеры.

**Флагман изменён.** Реальные сессии просили не «play → input → assert», а «живую правду Editor»:
снимок иерархии/инспектора/импорта с картинкой (отчёт 3 — пользователь вручную слал скриншоты),
правду компиляции (отчёты 1–2) и запуск существующего editor-инструмента с аргументами до
завершения импорта (отчёт 4). Поэтому:
- **Шаг 1 (недели 4–6) — «Live truth»:** `nexus_snapshot`, `nexus_run`, плюс `nexus_compile` /
  `nexus_look` из Phase 1. Демо: «агент перестал просить у вас скриншоты».
- **Шаг 2 (недели 6–9) — `nexus_verify`, только если есть место.** Ниша занята: uLoopMCP (v3.0.1:
  клавиатура/мышь/клик по UI/replay), akiojin/unity-cli (skill playmode-testing), Funplay, а демо
  самой Unity показывает, как агент тестирует в Play Mode и ловит баг. Если в спайке P0-10b ≥2
  конкурента находят наш подложенный баг — verify не строим как флагман, строим только недостающее.

**Технические поправки.** Domain reload при входе в Play Mode и после компиляции — это норма, а не
сбой: длинные операции делаем возобновляемыми (job store + resume). Ускорение main thread — сначала
1-дневный спайк (Pipeline страдает от того же троттлинга), релиз 1.7.0 его не ждёт. Публичные имена —
`nexus_*` с подчёркиванием (точки в именах тулов не проверены и уже не используются в коде). Ввод —
только Input System. Один `EditorUnityPrimitives` на публичных API вместо двух адаптеров. Без
генератора кода: ручные обёртки + тест на расхождение. Идемпотентность запросов (`request_id`).

**Чего НЕ делаем:** гонку за количеством тулов, свой `execute_code` (и его «прокладку» — используем
`unity eval`, это документируем), свой транспорт, брокер, установщики под 10 клиентов, перехват
нативных диалогов, телеметрию, миллисекундные сравнения как главный аргумент.

**Два решения-«ворот» с правом остановиться (критерии регистрируются заранее, промпты пишет не автор
skill, N=8–10 на ячейку, ≥2 задачи из стороннего open-source проекта):**
- **Неделя 4 (Gate A):** (A1) Nexus вызывается на шаге, где нужна правда Unity, в ≥60% релевантных
  прогонов, и нижняя граница 90% CI выше верхней границы базовой линии; (A2) решение по verify по
  результатам спайка конкурентов. Провал A1 → 2 недели на доступность и повтор, затем запасной план.
- **Неделя 9 (Gate B):** verify против **лучшего** конкурента (не только против голой Unity CLI):
  успешность ≥ конкурента **И** ложные «готово» ≤ конкурента, плюс заранее заданный эффект; прогон и
  на Windows; ≥5 внешних пользователей попробовали, ≥3 подтвердили использование.

**Параллельно:** в неделю 1 — предложить Capture V2 (Overlay) / readiness апстриму (Unity, Ivan,
uLoop); публикация на OpenUPM вместе с 1.7.0; Windows-проверка до Gate B; COMPARISON с Ivan,
Coplay, Funplay, akiojin, uLoop.

**Сверка с Unity (v2.2, подробно в §14b).** Unity строит маленькие примитивы и CI-инструменты, а
сценарии оставляет skills своих команд. Уже есть или скоро будет: `unity recompile` с файлом/строкой
ошибок, тесты (`--affected`, JUnit), билды и hot reload в плеере, `batch` с одним Undo, `eval`,
профайлер («до GA» по словам сотрудника), `simulate_key/pointer` (по отчёту — только в dev-плеере и
только Input System; проверяем в P0-10). Это **не строим**, а оборачиваем. **Строим**: UI-таргетинг
по имени (UGUI + UITK), проверки состояния/лога/UI/пикселей, сценарий verify (с выводом в JUnit),
бюджетированный контекст, диагностику, которая работает даже в Safe Mode, правдивую готовность.
**Срочно:** Pipeline 0.8.0-exp.1 (28.09) перенёс атрибуты в сборку `Unity.Pipeline.Attributes` —
наша Pipeline-сборка, вероятно, перестанет компилироваться; P0-06 переходит в первые дни.

**Слепые зоны (v2.3, подробно в §16).** (1) **Unity убирает domain reload:** в 6.6 (31.08.2026) новые
проекты входят в Play Mode без перезагрузки домена, в 6.8 это единственный режим, Unity 7 — CoreCLR.
Поддержка Unity 6.0 заканчивается 16.10.2026. Значит, часть плана про «переживание reload» нужна только
для старых проектов, а новая реальная проблема — статика, которая не сбрасывается между запусками
(V-08 переписан под проверку этого). Добавлен спайк по матрице версий (P0-13). (2) **Хуки агентов**
(Claude Code / Codex / Gemini CLI): после правки `.cs` автоматически `nexus_compile`, перед «готово» —
`nexus_diagnose`. Это снимает проблему «агент не вызывает Nexus» (S-09, G13). (3) **MCP 2026-07-28:**
`initialize` заменён на `server/discover`, P0-07 обновлён. (4) **Облачные агенты без Editor** — спайк
P0-14. (5) **ToS Unity §17.2** и **приватность/NDA** — правило: Nexus не ходит в онлайн-сервисы Unity,
картинки можно выключить (`images: off`). (6) **След пакета в проектах:** жёсткая зависимость от Input
System и Runtime-сборка в релизных билдах — P0-15. (7) Бенчмарк публикуем как открытый eval.

**Граница Nexus ↔ Unity CLI (v2.4 → v2.5, подробно в §17.3).** Вопрос: оставаться ли Nexus небольшим набором тулов
*рядом* с Unity CLI, или стать **основным компактным интерфейсом для агента**, а Unity CLI/Pipeline — скрытым официальным
исполнителем (Nexus-primary, S6)? v2.4 по ошибке сузила это до «S4 + несколько сжатых чтений» (S5) и назвала полный фасад
отклонённым — это исправлено: **S6 не отклонён, не подтверждён и не запланирован**, это открытый кандидат.
Что измерено: схема 154 тулов ≈21–25k токенов, но Claude Code/Codex/Cursor грузят тулы лениво; при этом поиск/обнаружение
тулов не бесплатен (Codex в code mode вываливает 25–29k токенов списка тулов на один запрос); ответы Unity без границ
(иерархия ≈86k). Формат «менеджеров» сам по себе экономит ≈4% — но это не то же самое, что **сокращение видимых возможностей**
(154 → компактный набор), которое мы и проверяем. Пилот (Codex, 51 прогон, предрегистрация, `PILOT_RESULTS.md`): **данных
недостаточно**, и обе направленные метки **неустойчивы** — при небольших обоснованных вариациях (без задачи T3, которую S4
решал grep-ом без инструментов Unity; с исходными прогонами до починки моста; до исправления парсера T3) они становятся
нейтральными. По наблюдаемому успеху S6-domain ≈ S4 (10 vs 9); 4 из 6 его «провалов» — штраф за `eval` по продакшен-политике.
Мок S6 был плоским (общие параметры менеджеров, с `eval`, без композитов) — это **не** тот S6, который мы хотим проверить. Твёрдые находки: нормализация режет вывод тулов в 20–100 раз; самый «шумный» вывод в S4 —
наш же legacy `unity_dump_scene_graph`; Unity CLI из песочницы Codex не работает и удаляет port-файл Pipeline (F27).
Решение: **ничего не строим.** Как дальше проверяем — см. абзац v2.6 ниже (реестр теорий → дешёвая фальсификация →
финалисты → Gate F); запись через фасад — отдельный исследовательский вопрос (P0-16d), `eval` не делегируется никогда.

**v2.6 — как теперь принимаем архитектурное решение (§18, P0-17, Gate F).** Полный эксперимент сразу (≈1 600–2 400
прогонов, ≈130–320 часов Editor) с большой вероятностью снова дал бы «данных недостаточно». Поэтому порядок такой:
имеющиеся данные → **реестр архитектурных теорий** (H1–H7) → **дешёвая фальсификация** (Stage A, один клиент, ≈8 задач × ≈6
прогонов на руку, ловим только большие эффекты) → отсев слабых теорий → **≤3 претендента** (плюс постоянные контроли S4 и C1) → **подтверждающий бенчмарк**
(Stage B, парный дизайн, больше N, нормальные сторонние задачи) → **Gate F** → решение о реализации. Наиболее интересная гипотеза сейчас (это априорное мнение владельца, **не** результат) —
**смешанная Nexus-primary архитектура (M)**: Nexus — предпочтительный компактный слой (Live truth, snapshot, diagnose, compile,
look, run, ограниченные семантические чтения, handles), Unity Pipeline/CLI — официальный исполнитель, а прямой Unity остаётся
выходом для исключений (`eval`, `run_script`, свободный `batch`, пакеты, билды, опасные настройки). Это **гипотеза, не
одобренная архитектура**. Обязательные постоянные контроли — текущий дизайн **S4** и **C1** (официальный Unity, просто урезанный/allowlist): если C1
не хуже, фасад не оправдан. «13 менеджеров со всеми 154 командами» — не цель (≈4% экономии); цель — **сокращение видимых
возможностей**. Работа, не зависящая от архитектуры (readiness, epochs, jobs, ошибки, Capture/Look, бюджеты, handles,
`nexus_snapshot/diagnose/compile/run`, чистка шумного legacy `unity_dump_scene_graph`, Pipeline-совместимость, skills),
**продолжается**; всё, что зависит от выбора архитектуры (полный фасад, делегирование записи, генерируемые менеджеры), —
**заблокировано** до доказательств. Транспорт (warm MCP / Pipeline HTTP / постоянный процесс) — отдельный вопрос (H5):
shell-first больше не считается универсально надёжным (F27).

**Решения, которые нужны от вас сейчас** — см. §10 (G1–G17). Рекомендации даны.

---------------------------------------------------------------------
## 1. FACTS THIS PLAN RELIES ON (each with evidence and expiry)
---------------------------------------------------------------------

Rule (new): a "fact" needs an evidence link, a date and an expiry. Anything else is a hypothesis.
No "DO NOT REOPEN" sections. Facts expire on a Unity / Pipeline version bump or after 30 days.

| # | Fact | Evidence | Expires |
|---|---|---|---|
| F1 | Unity CLI 1.0.0-beta.11 (2026-09-22) + `com.unity.pipeline` (experimental, 0.x-exp), free, Unity 6.0+ | docs.unity.com/en-us/unity-cli/release-notes (via search only; docs.unity.com is not fetchable from the review container) | next CLI release |
| F2 | Unity deprecated its in-Editor AI Assistant MCP server; `unity mcp` replaces it; Unity recommends `unity command`/`unity eval` over MCP for shell-capable agents | docs.unity.com/en-us/unity-cli/replace-mcp-server-unity-cli (via search) | next CLI release |
| F3 | Pipeline built-ins include status, console, hierarchy, component get/set, play/stop, screenshot/capture_game_view, recompile, build, tests, packages, bakes, eval, menu, `set_autotick`; `[CliCommand]` auto-discovery with `MainThreadRequired` (default true); `unity shell --protocol ndjson` warm process. **`--detach` + `unity job` was first sourced only from a forum post; v2.2 research (U1/U2) found it in the official skills material — treat as likely, confirm in P0-10** (not in the official SKILL.md) → hypothesis until P0-10 | Unity-Technologies/skills `unity-cli` SKILL.md; community command list; beta.4 forum post | next Pipeline release / P0-10 |
| F4 | **Revised (v2.2):** Pipeline 0.7 reportedly ships `simulate_key` (down/up/press) and `simulate_pointer` (screen x/y, move/down/up/click), on the **runtime (dev Player) server only**, Input System only, no key hold, no text entry, no UI-element targeting. Whether they work in Editor Play Mode is unknown. No UI automation (UGUI/UITK) command exists. Reported from a third-party copy of the 0.7.0-exp.1 package source (its CHANGELOG checked; the command source not independently verified) | `v2_analysis/U1_unity_shipped.md`; github.com/bigkaka111-oss/ReactorCrystal/tree/main/Packages/com.unity.pipeline | **P0-10 must confirm on the harness** |
| F14 | `com.unity.pipeline` 0.8.0-exp.1 (~2026-09-28) has breaking changes; `[CliCommand]`/`[CliArg]` moved to a new `Unity.Pipeline.Attributes` assembly, so Nexus's Pipeline asmdef (no reference to it, open-ended `0.0.0-exp` range) will likely fail to compile with CS0246 on 0.8 | U1 §2, U2 §1 | P0-06 |
| F15 | Unity CLI beta.9–11 added `unity recompile` (errors with file and line, exit codes), `unity test --affected`, `watch test`, `unity vcs affected`, `--detach`/`unity job`, `--caller`/`--skill` flags; Pipeline 0.7 has `wait_for`, transactional `batch` (single Undo, `dry_run`), `run_script`, `audit`, `search`, `get_performance_stats`, `set_autotick`, ~162 commands | U1, U2 (search snippets + GitHub skills repo) | next CLI/Pipeline release |
| F16 | **Unity is removing classic domain reload.** Unity 6.6 (2026-08-31) makes Fast Enter Play Mode (no domain reload, scene reload only) the default for new projects; 6.7 LTS is the last Mono release; 6.8 makes it the only Play Mode option and moves to CoreCLR/.NET (assembly-level reload via Assembly Load Contexts); Unity 7 (early beta ~Dec 2026, GA Q1 2027) is CoreCLR-only. Unity 6.0 LTS support ends 2026-10-16. Static fields/events are no longer reset between Play Mode sessions | Unity Discussions "Path to CoreCLR, 2026"; Unity 6.6 manual (configurable Enter Play Mode, via search); STYLY-NetSync issue #523; `v2_analysis/E_blind_spots.md` #1 | 6.8 beta |
| F17 | MCP spec 2026-07-28 removes `initialize`/sessions (`instructions` move to `server/discover`), makes tasks an extension and replaces server-initiated elicitation with a new input-request mechanism; clients will speak both 2025-11-25 and 2026-07-28 for a while | modelcontextprotocol/modelcontextprotocol spec changelog (reported by E; not re-verified by the lead) | next MCP spec release |
| F18 | Unity ToS (2026-06-30) §17.2 restricts agentic access to Unity "Offerings" to authorized agentic access; Unity staff said informally that local Editor integrations are unaffected, but the text is unchanged | unity.com/legal/terms-of-service; Unity Discussions thread 1724661 (search only) | ToS amendment |
| F19 | Nexus footprint in user projects: `package.json` hard-depends on `com.unity.inputsystem` 1.19.0; `Runtime/UnityMCP.Runtime.asmdef` has no platform/define constraints, so it compiles into release player builds | `package.json:23-26`; `Runtime/UnityMCP.Runtime.asmdef` (verified by the lead) | P0-15 |
| F20 | `unity mcp` (CLI beta.10, Pipeline 0.7.0-exp.1) exposes **154 tools, one per Pipeline command**, ≈20.8k schema tokens with compact JSON, ≈24.6k with default spacing (cl100k proxy; bare names ≈0.45k, ≈1.4k with client prefixes like `mcp__unity__`); `tools/list` is empty until an Editor connects; `listChanged:true` | MEASURED 2026-10-02, `v2_analysis/F_facade_evidence/unity_mcp_tools_list.jsonl` | next CLI/Pipeline release |
| F21 | Client capability loading (v2.5, measured/source-checked): **Claude Code** defers MCP schemas (bare names at start; keyword ToolSearch, top-5; deferral off for non-first-party base URLs, some Vertex/Foundry models) and spills large MCP/tool outputs to a file the model reads back (observed in this session; v2.4's "truncated at 25k" was wrong). **Codex 0.160** default models use **code mode**: the model filters an `ALL_TOOLS` list (name+description+schema) in JS, output capped at 10k tokens; only `tool_search`-capable models (e.g. gpt-5.5) use BM25 top-8 over name/server/description/top-level property names (not `oneOf`/enum/param descriptions); `list_changed` ignored. **Cursor** keeps names, greps description files. **Gemini CLI 0.60** sends every schema every turn | `G_nexus_primary/A_tool_search.md` (source + 10 live Codex runs), `F_facade_evidence/R_clients.md` | 30 days |
| F22 | Unity read outputs are unbounded: `get_scene_hierarchy` has no depth/subtree/limit parameter (`path` = scene file) and returns ≈86k tokens for a ≈1.1k-object scene (above Claude Code's inline output limit, so the result is spilled to a file and read back with extra calls, or the agent falls back to `eval`/filters); `console` tail 100 ≈29k; `--format human` is not smaller. The 100× reduction of a prototype normaliser is an upper bound against untargeted calls; tokens per *successful task* are unmeasured. Warm `unity mcp` call ≈100 ms (unfocused Edit Mode), cold `unity command` ≈1.2–2.2 s | MEASURED, `F_facade_evidence/compact.py`, `warm.py`, `raw_samples/` | next Pipeline release |
| F23 | Nexus's 14 managers ≈5.3k tokens (the "~4.9k" figure was chars/4); the same 71 actions as atomic tools ≈5.6k (+4%); the 2026-05 consolidation's saving came from hiding ~50 methods, not from the `action` format | MEASURED, `F_facade_evidence/R_repo.md` | next bridge change |
| F24 | Discovery cost is real even with deferral: on Codex code mode, a single-intent turn over S4's surface (154 Unity + 14 Nexus tools) dumped 25.7–29.3k tokens of tool listing (cut to ≈11.4k) and took 67k–174k input tokens; offline Codex-BM25 recall@1 was .53 (S4) vs .79 (13 domain managers, terse queries); in S4, a Unity tool ranks first for 28 of 32 intents where both a Unity and a Nexus tool fit | `A_tool_search.md` (N=10 live runs + 40-intent offline set; directional) | next Codex release |
| F25 | Catalog-driven adapter feasibility: Unity's command tags place 151/154 commands with 20 hand mappings (name rules: 98.1% with 2 exceptions); 13 domain managers covering all 154 cost ≈25.7k tokens typed (+4% vs atomic) or ≈12.5–16k flat (lossy); curated 5-tool surface ≈6.9k; router ≈0.2k. Inputs are fully typed; **no output schemas**, so normalisers stay hand-written; 0.3→0.7 churn: 13 commands added, 2 removed, output envelopes changed in 0.4/0.6/0.7. Estimated hand code for a façade: ≈720–1,130 lines (vs bridge 1,933) | `G_nexus_primary/B_catalog_adapter.md` | next Pipeline release |
| F26 | Pipeline 0.7 security model: loopback + Origin refusal + one 256-bit bearer token for all 154 commands (no scopes); **no pre-dispatch hook** (only post-command `OnCommandDone`; Unity's own policy item CAT-2509 unimplemented); command registry is internal (no public in-process call); `set_authoring_root` has no confirm; `write_text_file` creates `.cs`/`.asmdef`/`.rsp` without confirm; no idempotency id. Nexus token file is world-readable (0644, issue #0096) | `G_nexus_primary/D_security.md` (package source, file:line) | next Pipeline release |
| F27 | Unity CLI inside the Codex default sandbox (`workspace-write`, network on): `unity command …` cannot find the Editor ("No Unity Editor instances found with reachable Pipeline servers") **and deletes `Library/Pipeline/.unity-pipeline-port`**, making the Editor undiscoverable for every client until restart; MCP (`unity mcp`) works because MCP servers run outside the sandbox | MEASURED 2026-10-03 (`codex sandbox` repro; pilot run T3_S4_1; PREREG A3) | next CLI/Codex release |
| F5 | Third parties already ship Play Mode input + UI clicks + screenshots: `hatayama/unity-cli-loop` (uLoopMCP) v3.0.1 has `simulate-keyboard`, `simulate-mouse-input`, `simulate-mouse-ui`, `replay-input`, `screenshot`, `record-video` over its own socket/named-pipe IPC (not Pipeline); `akiojin/unity-cli` ships `unity-playmode-testing`, `unity-input-system`, `unity-ui-automation` skills; Funplay ships a core profile with input sim + screenshots | github.com/hatayama/unity-cli-loop; github.com/akiojin/unity-cli; skills.sh listings | P0-10b |
| F6 | **Hypothesis (N=4, self-written agent retrospectives, not transcripts):** in 3 of 4 sessions Nexus was never called; in the 4th, 2 calls. Agents used shell, asked the user for screenshots, or drove the GUI with computer-use. Report 3 cannot confirm Nexus was even installed (`3.md:31`); report 4 ran in a computer-use client | `AI Chat reports Before all changes/1-4.md` | re-measured in P0-09 |
| F7 | v1 benchmark "ok%" = no JSON-RPC error; C10 invalid for all tools (fixture has 0 tests; bridge result was `Submitted`/`Timeout`); C12 invalid (no EventSystem; Funplay click hit nothing; Coplay screenshot `success:false`); C8 bridge used OS screencapture; C7 raw 99 ms = false-ready (n=4; listener died on reload) | raw JSON in `NexusFeature~/results/`; `BENCH_RESULTS.md:117` | permanent (v1 numbers are void) |
| F8 | Main-thread latency steps (~100 ms) = Editor update throttling in Edit Mode while unfocused. Bridge "fast" run was in Play Mode (`isPlaying:true`), raw runs in Edit Mode. Nexus queue drains only on `EditorApplication.update`, nothing wakes it. **Persistent Pipeline shows the same 3 ticks × ~100 ms**, so pipeline mode does not escape it; macOS App Nap is already disabled (`AppNapBypass.cs`), so the remainder is Unity's own background throttling | `bench_nexus_bridge_fast.json`; `MCPServer.cs:145`, `MCPServerMethods.cs:215`; `STABILIZATION_ACCEPTANCE_REPORT.md` §E (lines ~135-144) | until P0-01a re-measures |
| F9 | Hybrid M1–M4 is implemented but only 3 commands reach Pipeline; they are registered as **`nexus_project_map`, `nexus_group_compile_errors`, `nexus_capture_game_view` (underscores)**; the dotted `nexus.*` string is only the internal `Id` | `Editor/Pipeline/NexusPipelineCommands.cs:21,31,43`; `Editor/Commands/*Command.cs` `Alias` | — |
| F10 | Capture V2 includes Screen Space Overlay UI; orientation proven on Metal only; depends on private `GameView.m_RenderTexture`; applies an **unconditional** vertical-flip Blit, so D3D output is likely upside down | Acceptance report §F; `Editor/Capture/GameViewCaptureSource.cs:14-16,52-55` | Windows run (L-05) |
| F11 | Unity's own launch material shows an agent (Pi harness + Unity CLI) entering Play Mode, inspecting runtime state with `unity command eval`, fixing and re-verifying a bug — "agent verifies its own fix" is the category's default demo, not a Nexus-unique claim | gamedev.net/news/5423; runtimewire.com (Unity CLI article); unity.com Pipeline walkthrough (via search) | next Unity CLI release |
| F12 | Claude image input costs ≈ w×h/750 tokens: 768×432 ≈ 0.44k, 1024×576 ≈ 0.79k, 1024×1024 ≈ 1.4k | platform.claude.com vision docs | model change |
| F13 | Tool names in major LLM APIs are restricted to `[a-zA-Z0-9_-]` (Anthropic `^[a-zA-Z0-9_-]{1,64}$`); dotted MCP tool names may be rejected or mangled by clients (not yet tested for `unity mcp`) | Anthropic tool-use docs; P0-10 naming check | P0-10 |

Verified code defects used below (file:line checked on `ef13f8e`, re-checked on `f4d728f`):
- D-1 (minor) `shutdown_server` needs no token and requests without `Origin` are accepted → any local process can stop the server (`MCPServer.Http.cs:23,132`). Impact is nuisance-level: a same-user process can read the token file anyway (`MCPServer.Identity.cs:122`), and browsers are blocked by Origin/Host loopback checks. Fix stays, severity lowered.
- D-2 A main-thread call that times out after 60 s still runs later → a client retry can apply a write twice; after timeout the lambda also calls `signal.Set()` on a `ManualResetEventSlim` already disposed by `using` → `ObjectDisposedException` on the main thread. A cancel flag only helps calls still *queued*; a call already *running* past 60 s still applies (`MCPServerMethods.cs:215-227`).
- D-3 `wait_for_editor_idle` checks only `IsCompilingCached/IsUpdatingCached` (`MCPServerMethods.Sync.cs:76`), which refresh only on a main-thread tick (stale while throttled) and ignore pending refresh / play transition (#0013). `Status.cs:22` has a richer predicate → two inconsistent readiness predicates today.
- D-4 Bridge compile wait uses fixed sleeps (0.5 s / 2.0 s / 1.0 s) and can wait ~20 s for a reload that never comes (`nexus_bridge/routes_editor.py:73-97`).
- D-5 Pipeline asmdef `versionDefines` is open-ended `0.0.0-exp` → a breaking Pipeline release breaks user compiles (`Editor/Pipeline/UnityMCP.Editor.Pipeline.asmdef:22`).
- D-6 Bridge `initialize` sends protocol `2024-11-05`, no `instructions`, declares `prompts` but `prompts/list` returns -32601 (`nexus_unity_bridge.py:164-166`); results are text-only (images arrive as base64 text, `:150-153`).
- D-7 Bridge never sets MCP `isError`; `Timeout`/`PartialSuccess` look like successful calls to agents.
- D-8 Legacy HTTP bind is skipped when the port **is owned by another Unity project** and Pipeline is the effective runtime (`MCPServer.Networking.cs:105-113`, `NexusRuntimeSelector.cs:60-68`); the HTTP-only Python bridge then talks to the wrong project or nothing → ~117 methods unreachable for that project. Fix = per-project port discovery for the bridge, not "always bind".
- D-9 Full stack traces returned in JSON-RPC error `data` (token waste, info leak) (`MCPServerMethods.cs` `CreateExceptionResponse`/`CreateErrorResponse`).
- D-10 `group_compile_errors` scrapes Nexus's own 50-entry console ring buffer: line/column stay only inside raw message strings (unstructured), errors logged before server init or across reload are missed, file key is a substring heuristic (`GroupCompileErrorsCommand.cs:61-80`).
- D-11 The "confirm gate" for `.cs` writes is `confirm:true`, which the agent sets itself (`MCPServerMethods.Utils.cs:287-291`). It is an intent flag, not a human gate; docs and SECURITY.md must not call it a gate.

---------------------------------------------------------------------
## 2. POSITIONING
---------------------------------------------------------------------

> **Nexus — live Editor truth for Unity agents.** Nexus returns what the Editor actually shows and
> actually compiled — hierarchy, inspector values, import state, Overlay-correct images, structured
> compile diagnostics — in one call and under 1k tokens, and runs your existing editor tools to
> completion. Works with Unity CLI; also runs standalone.

- The v2.0 metaphor "Unity gives hands, Nexus gives eyes and judgment" is **dropped**: Unity already ships screenshots, hierarchy and component reads (F3), so the metaphor invites "Unity already has that". Claims must be concrete and checkable against a benchmark artifact.
- Brand "Nexus" + badge line "Works with Unity CLI". Do not name the product after Unity CLI (beta dependency, trademark risk). The **skill id and listing use a descriptive, searchable name** (working name `unity-live-editor-truth`, title "Nexus: live Editor truth for Unity agents"); acceptance test in S-01.
- Headline demo: **"Your agent stops asking you for screenshots."** — a replay of real report 3 (and report 4: run the packer tool to completion) side by side with Nexus.
- "The agent says it's fixed. Nexus proves it." becomes the **secondary** line and is used publicly only if Gate B passes (F11: it is the category's default demo).
- Security is repositioned honestly: *Nexus's own commands are sandboxed; Unity `eval` is outside Nexus's control; `confirm:true` is an agent-set intent flag, not a human gate (D-11).* It is not a headline differentiator.

### 2.1 Target user
- **Primary:** a Unity programmer using Claude Code / Codex on an existing production project (not greenfield scene-building), on Windows or macOS — the user who produced reports 1–4.
- **Secondary:** tools programmers who need agents to run existing editor tooling (report 4).
- **Not targeted:** QA teams testing player builds (AltTester, GameDriver own this), no-code creators (Unity AI).
- Every benchmark task set must include tasks derived from this persona's real sessions (reports 1–4) and ≥2 tasks from a third-party open-source Unity project, not the synthetic fixture alone.

---------------------------------------------------------------------
## 3. PRINCIPLES (apply to every task)
---------------------------------------------------------------------

1. **Don't duplicate Unity.** If Pipeline has an atomic command, Nexus calls it (pipeline mode) or keeps its frozen legacy equivalent (legacy mode). No new atomic CRUD. **v2.4 — three distinct things, never conflated:**
   - *Duplicate implementation* (forbidden): Nexus offers a **capability** Unity already offers with equivalent bounds — whatever the code path (own Editor-API code, or a wrapper that only renames a Unity command).
   - *Delegated façade* (allowed only if Gate F passes, §7): Nexus action → **official** Unity command → Nexus normalisation / budget / handle → agent. Nexus owns the schema and output shape, never the primitive. **Production policy now:** a first façade is read-only and allowlisted. **Research question, not policy:** whether official Unity *write* primitives can later be delegated with Nexus guarantees intact (P0-16d; `D_security.md`: bounded Undo writes "later, with conditions"; settings/destructive/build/package stay Unity-direct for now). `eval`, `eval_file`, `run_script` and agent-authored `batch` are arbitrary code and are never delegated.
   - *Representation compression* (14 managers vs 71 atomic tools: ≈4%, F23) is not the same as *capability-surface reduction* (154 visible commands vs a task-oriented subset): the latter changes retrieval, routing and discovery (F24) and is what S6 tests.
   - *Nexus composite* (the product): Nexus workflow → several Unity state sources / primitives → orchestration, truthful waits → compact semantic result.
   - A Nexus read that adds bounds Unity lacks (e.g. depth/limit on hierarchy, F22) is not a duplicate **only while Unity lacks them**: first ask upstream (P0-12), and retire the Nexus read within one minor after Unity ships the equivalent.
2. **Every new capability must either replace ≥3 agent calls, or do something Unity and the shipping competitors cannot.** Otherwise it is not built.
3. **One logical call, Editor-side.** Composite workflows run inside the Editor with event-driven waits (compile/reload/play-mode/test callbacks), never fixed sleeps. **A reload may or may not happen** (v2.3, F16: Fast Enter Play Mode is the default on 6.6+ and the only mode on 6.8; CoreCLR reloads per assembly). Readiness never depends on a reload event alone; workflows are resumable where reloads happen and state-reset-aware where they don't. Where a reload does happen (play entry on older projects with reload enabled, compiles on Mono): any operation that can cross a reload persists a resumable job (state in SessionState / `Library/Nexus/jobs`), resumes via `[InitializeOnLoad]`, and the client shim (bridge / `unity command` wrapper / skill) reconnects and long-polls `job_wait` once. `interrupted` is reserved for Editor restart or crash.
4. **Small answers by default.** Verdict + counts + top-k + handles; `detail: summary|normal|full`; deltas via generation/cursor; images only when useful, cropped and downscaled (default long side 768 px, F12).
5. **Truthful status.** Never report success for partial/timeout. `Submitted` is a legitimate pending state, not an error. Ready only after a new compile/reload epoch.
6. **Distribution ≠ transport (v2.6, was "shell-first").** Agent-facing entry points (skills, MCP, `unity command nexus_*`, hooks) and the backend transport Nexus uses to reach Unity are separate decisions. Shell-first is **not** a universal assumption: in Codex's default sandbox the `unity` CLI cannot reach the Editor and deletes the Pipeline port file (F27). `unity command nexus_*` and MCP are both supported entries, neither assumed primary until S-01/H5 evidence, and the backend transport (warm `unity mcp`, Pipeline HTTP, persistent ndjson shell, in-Editor) is chosen by evidence (§18 H5), not by the façade question. A `nexus` shim (the bridge CLI) is optional because it requires Python (often missing on Windows). Public names use underscores (F13) until P0-10 proves dotted names work everywhere.
7. **Measure what users feel.** Task success, false-success rate, tokens per task, agent chose-tool rate, external users. Milliseconds only with tick state controlled. Gate criteria are pre-registered.
8. **Freeze what we don't differentiate on.** Legacy HTTP, 14 bridge managers, 10-client installers, runtime selector: security and crash fixes only.
9. **Untrusted content is data.** Console logs, UI strings, scenario files and project text returned to the model are third-party content; skills say so, and no returned text can trigger Nexus actions.

---------------------------------------------------------------------
## 4. TARGET ARCHITECTURE (reuse what exists — no rewrite)
---------------------------------------------------------------------

```
L4 Skills & distribution   skills~/unity-live-editor-truth (+ nexus-verify if Gate A2/B pass) + AGENTS.md snippet
L3 Nexus Workflows         nexus_snapshot · nexus_compile · nexus_run · nexus_diagnose · nexus_verify (conditional) · ...
                           depend only on IUnityPrimitives + Core services
L2 Nexus Core              Editor/Commands (registry, descriptors) · Editor/Capture (Capture V2)
                           input · UI automation · readiness/epochs · resumable job store · scene intelligence
L1 Projections             Pipeline [CliCommand] (hand-written, drift-tested) · Legacy HTTP JSON-RPC · MCP bridge (thin)
L0 Unity                   Pipeline server · unity command / shell / mcp / eval · built-ins · public Editor APIs
```

Key rules (details in `v2_analysis/D_architecture_roadmap.md` §1, amended here):
- **One canonical definition per command**: typed argument record → descriptor parameters by reflection (C#, in-Editor) → **hand-written** thin `[CliCommand]` wrappers (~10 lines each) → EditMode **drift test** that reflects over `CliArg` attributes and fails when they differ from descriptors. No code generator (Python cannot reflect C#; batchmode generation would need Unity in CI). Legacy projection already exists (`NexusLegacyCommandProjection`).
- **`IUnityPrimitives`** (play/stop/recompile/tests/refresh/console) has **one** implementation, `EditorUnityPrimitives`, built on public Editor APIs (`EditorApplication.EnterPlaymode`, `CompilationPipeline.RequestScriptCompilation`, `AssetDatabase.Refresh`, `TestRunnerApi`, `Application.logMessageReceivedThreaded`). Pipeline is a **projection (L1) only**, never a primitive provider — no reflection into the experimental package. Workflows never call `MCPServerMethods.*` JObject handlers.
- **Seams before any assembly split**: `IMainThreadDispatcher`, `IConsoleLogSource` (Capture and GroupCompileErrors currently call `MCPServer.*` directly).
- **Error envelope for canonical commands**: `{ok, data, error:{code, message, retryable, hint}, meta:{ms, mode, gen, request_id}}`; stable string codes; stack traces only with `NEXUS_DEBUG`. Legacy raw shapes unchanged.
- **Job model**: `NexusJobStore` (SessionState + `Library/Nexus/jobs/<id>/`, size and retention capped) with step cursor + `[InitializeOnLoad]` resume, so a job survives play-entry and compile reloads and completes; `interrupted` + partial report only on restart/crash. Legacy and MCP get `nexus_job_status/wait/cancel`; pipeline mode uses Unity `--detach` + `unity job` only if P0-10 confirms they exist.
- **Idempotency**: mutating calls accept `request_id`; the server remembers results ~5 min; a retry with the same id returns the stored result; a mutation that times out while executing returns `outcome:"unknown"` + `request_id`.
- **Handles and job ids** (`cap://`, `log://`, `snap://`, jobs) are server-issued opaque ids `[a-z0-9]{8,}`, never paths.
- **Pipeline compatibility**: pin `versionDefines` to a tested range; out-of-range → assembly excluded → `auto` falls back to legacy; CI contract job for Pipeline N and N-1. Write-capable parameters are **not** projected to Pipeline until Pipeline's auth model is documented (Pipeline does not check the Nexus token).
- **Delegated façade / normalised reads (v2.4, conditional on Gate F; v2.5 note):** S5-style candidate is **Core `[CliCommand]` read commands** (reachable via `unity command nexus_*`, MCP and legacy, no Python needed, Principle 6) that obtain Unity's data through `IUnityPrimitives` / public Editor APIs and add only bounding, grouping, budgets and handles; the alternative candidate is an **L1 client-side adapter** (bridge/shim) over a warm `unity mcp` / `unity shell --protocol ndjson` child — never a cold `unity command` per call (+1.2–2 s, F22), never reflection into Pipeline. Either way: **read-only allowlist**; no write parameters, `eval`, `run_script` or `batch` routed through Nexus (rule §6.8, G3, R5); the L1 adapter must not hold a Unity session that bypasses the Nexus token/sandbox, and its auth model is checked in P0-10. For this S5-style candidate, coverage means every Unity command stays reachable **directly through Unity**, not through Nexus (an S6 candidate is evaluated by Gate F D6 instead). **v2.5 — S6 (Nexus-primary) location, research only:** Pipeline's command registry is internal (F26), so true delegation cannot run in-process without reflection; an in-Editor Nexus command waiting synchronously on a main-thread Pipeline command over loopback would likely deadlock (HYPOTHESIS, D_security.md). An S6 product would therefore be an **out-of-process** layer (bridge/shim over a warm `unity mcp` / ndjson shell or the Pipeline HTTP endpoint), generated from Unity's catalog (F25), holding the Pipeline token as a deputy (needs P0-16d conditions). Nothing here is scheduled.
- **Security boundary (v2.6, policy — separate from architecture evaluation).** Nexus is **not** a containment boundary around Unity writes today (the agent can reach Pipeline directly; F26), so façade routing is never sold as sandboxing. Production policy until evidence changes it: reads / inspection / diagnostics → Nexus candidates; bounded object writes → later research only (P0-16d); destructive writes → Unity-direct now, possibly gated later by a dedicated per-call-approval tool; `eval`, `eval_file`, `run_script` → Unity-direct permanently unless the premise radically changes; agent-authored free-form `batch`, package install/remove, builds/build-target changes, risky project settings → Unity-direct. Reads, writes and destructive writes are never combined in one approval-scoped tool (clients approve per tool name, D_security.md), in production or in any arm whose results inform production. Experiments may expose more for *observation*, but scoring always reports the production-policy view separately (§18).
- **Transport (v2.6).** Whatever the agent-facing surface, the backend link to Unity is a separate research question (§18 H5): warm `unity mcp` child (≈100 ms per call, F22), Pipeline HTTP (auth per F26), persistent ndjson shell, or in-Editor public APIs. A transport failure (e.g. F27) never counts as a façade failure, and vice versa.
- **Python bridge**: freeze the 14 managers; add dynamic pass-through of canonical commands driven by `list_tools` metadata (new C# command appears in MCP with zero Python edits); profiles chosen at startup (env var / argument), `load_profile` + `tools/list_changed` best-effort only; per-project port discovery (D-8); reuse its CLI mode as the optional `nexus` shim.

---------------------------------------------------------------------
## 5. THE PRODUCT SURFACE
---------------------------------------------------------------------

### 5.1 Default profile `core` (≤8 commands, schema budget ≤2.5k tokens, measured)
| Command | What it does | Replaces |
|---|---|---|
| `nexus_status` | Truthful state + compile/reload epochs; `wait` mode returns the epoch it observed; `detail:"capabilities"` returns a ≈300-token map of all jobs/profiles (answers "only 14 of 120 visible"); `detail:"doctor"` = which transport, port, profile, skill installed | status polls, false-ready, first-run confusion |
| `nexus_compile` | (optional sandboxed writes) → refresh → compile → grouped diagnostics with file/line/col + "did you mean" hints; reports Hot Reload / FastScriptReload presence | 6–15 calls + log dumps |
| `nexus_snapshot` | Live truth in one call: selection/path → hierarchy path, active state, RectTransform, component summary, serialized refs, import state, optional cropped Game/Scene View image | report 3: user-sent screenshots + YAML reading |
| `nexus_look` | Capture V2: JPEG, default long side 768 px, crop to object/UI element, Game View (incl. Overlay) or Scene View framed on object, optional numbered UI marks | full-frame PNG / human screenshots |
| `nexus_run` | Run a menu item or an allowlisted editor method with arguments, then wait event-driven for import/compile/reload; reports a probable modal dialog blocking the main thread | report 4: computer-use on folder dialogs |
| `nexus_diagnose` | "Why is it broken now": state, compile, grouped console errors, missing refs, last test failures | 5–7 calls |
| `nexus_find_references` *(only if C-02 beats `search`/`vcs affected` in P0-10; otherwise removed from core)* | Project-wide cached reverse index (scenes, prefabs, variants, SOs, Addressables); Force Text serialization only | guid grep that misses semantics |
| `nexus_get` | Handle resolver (`cap://`, `log://`, `snap://`) with range/crop | large default payloads |

Other profiles, chosen at startup (or loaded on demand where the client honours `list_changed`): `qa` (`nexus_verify`, input, ui_query/ui_act, step_frame — only if Gate A2/B pass), `context` (`nexus_context`, promoted to core only if C-01 passes its ≥30% bar), `scene` (scene_diff, snapshot — backlog), `perf` (later), `full` (legacy managers + raw escape hatch `nexus_call` / `nexus_list`). Before promising the budget, measure the verify DSL schema (may cost ~1k alone).

**v2.4:** the ≤8 / ≤2.5k budget stays. If Gate F passes, normalised reads must fit inside it by merging into core entries (`nexus_snapshot` for bounded hierarchy/find, `nexus_diagnose` for grouped console/test/compile results) or replacing one, not by growing it; each manager schema ≤ ~600 tokens (a deferred client loads a manager whole — today's `unity_hierarchy_manager` alone is 1.7k). This budget applies to the current architecture's core profile; an S6 surface is judged by Gate F (D3–D5, D7) and its manager sizes are measured, not pre-capped.

**v2.6 — legacy noise is cleaned up regardless of architecture (S-10).** In the pilot, the 12 largest tool outputs in the S4 arm (>40k characters each) were the current Nexus `unity_dump_scene_graph`; it and other unbounded dumps (`read_logs` without limits) leave the default agent surface, get bounded defaults where still reachable, and are replaced by `nexus_snapshot` / bounded reads. **Relation to S6-P:** the production-shaped Nexus-primary candidate (§18 H2) is essentially this core profile (`nexus_status/snapshot/diagnose/compile/look/run/get` + at most one bounded query) with Unity hidden behind it — not 154 Unity actions regrouped into managers.

In-memory C#: Nexus does not ship `execute_code` or an `eval` forwarder. Skills, `nexus_status detail:"capabilities"` and COMPARISON.md state: "in-memory C# = `unity eval` (requires Pipeline, same Unity 6.0 floor as Nexus)".

### 5.2 Method migration (from ~121 raw methods)
Summary (details: A §1, D §2):
- **~64 duplicate Pipeline built-ins** (scene/GameObject/component/asset CRUD, play/undo, prefs, menu): **frozen, legacy-only**, hidden from default profiles, still dispatchable.
- **~40 unique** (Capture V2, input, UI Toolkit automation, semantic_find, scene_delta, symbol_index, missing refs, diffs, timeline, lint, step_frame): become or feed canonical commands.
- **~10 rebuilt as composites** (waits, write+compile, tests-wait, batch).
- **7 transport methods**: Pipeline owns them in pipeline mode; legacy keeps them.
- **v2.4:** if Gate F passes, a frozen legacy duplicate is *not* revived for the façade; façade actions route to the Unity command, and the frozen method stays legacy-only.
- Deprecation policy: hide first (not a break) → `deprecated:true` + `replacement` in `list_tools` → dispatchable for ≥2 minors and ≥90 days → remove only in a major. No security gate is relaxed during migration.

---------------------------------------------------------------------
## 6. GLOBAL RULES v2 (paste to every agent, together with the task)
---------------------------------------------------------------------

1. Repo `ForkHorizon/NexusUnity` (MIT). Branch `rework2/<task-id>` from latest `development`. One task = one agent = one PR. Never merge your own PR. Do not push to `development`/`main`.
2. **Scope cap:** ≤15 files and ≤800 changed lines per PR unless the task says otherwise. If you would exceed it, STOP and report.
3. **Stop rule:** if effort passes 2× the estimate, or you are blocked >2 h on one approach, stop and write what you learned. Never silently widen scope.
4. **Definition of done** = user-visible behavior + raw evidence artifact committed under `Research~/evidence/<task-id>/` (JSON/log/PNG) + exact reproduction command + environment (OS, GPU API, Unity version, Pipeline version, Editor focus state).
5. **You don't accept your own work.** Never write "ACCEPTED"/"IMPLEMENTED/ACCEPTED". A different agent or a human verifies from the artifact on a clean checkout.
6. **Honest results:** failures are recorded, never smoothed. Unverifiable items are marked UNVERIFIED in the PR.
7. **TDD** for behavior changes (EditMode tests in `Tests/Editor`, Python tests next to the bridge). `PYTHONDONTWRITEBYTECODE=1 scripts/prepush-validate.sh --static-only` must pass.
8. **Security invariants** (loopback, token, path sandbox + symlink resolution, `confirm:true` intent flag for `.cs` writes / delete_asset / PlayerPrefs all, type allowlists, batch cap 50, ProjectSettings/Packages write-protected) are untouchable. Additionally for new commands: compile writes also block `*.rsp`, `.dll`, `.asmdef`/`.asmref` with `precompiledReferences`, and `Packages/`; handles/job ids are opaque server ids; job storage is size- and retention-capped; `nexus_context` honours `.gitignore` + a deny-list (`*.env`, keystores, `**/secrets*`); scenario files live in a non-imported folder (`NexusScenarios~/`) and use a closed step set (no reflection setters, no menu steps outside `full`); any temporary ProjectSettings/EditorSettings change is restored after the run and labelled in the report; write parameters are not projected to Pipeline. **v2.3:** Nexus never calls Unity online services (ToS §17.2, F18); image capture honours an `images: on|off` setting and a redaction deny-list (UI paths/regions), and `PRIVACY.md` states what leaves the machine. If the task conflicts, STOP.
9. **Frozen areas** (legacy HTTP methods, 14 bridge managers, installers, runtime selector): only security/crash fixes unless the task explicitly names them.
10. **Public API**: raw JSON-RPC methods keep their shapes. New canonical commands follow SemVer (add param = minor; remove/rename = alias for 2 minors). Public names use underscores (`nexus_*`). Keep `CHANGELOG.md`, `README.md`, `DOCUMENTATION.MD`, `API_REFERENCE.MD` aligned.
11. Unity floor 6000.0. Newer APIs behind `#if UNITY_X_OR_NEWER`. Pipeline code only in the Pipeline asmdef.
12. Benchmarks: success = semantic check (task oracle), never "no JSON-RPC error". Record Editor focus/tick state and `isPlaying` per sample. Gate runs follow the pre-registration file; no post-hoc changes to tasks, relevance or thresholds.
13. No public performance claims without a benchmark v2 artifact for that platform.
14. Human review is the bottleneck: at most 4 open PRs at once. Unity-facing evidence runs take the harness lock (P0-08c); never run two agents against one Editor.

---------------------------------------------------------------------
## 7. ROADMAP (weeks from 2026-10-05; each task ≤3 days)
---------------------------------------------------------------------

Legend: **E:** = evidence required in the PR.

### PHASE 0 — Honest measurement + cheap fixes (weeks 1–2)

**P0-01a · Main-thread wake-up spike** (1d) [spike, doc only]
- Measure Edit Mode read latency with Editor focused / unfocused / minimized / on another Space, with and without Pipeline `set_autotick`, Interaction Mode settings, App Nap state; find out what `set_autotick` actually calls. Note: `QueuePlayerLoopUpdate` is main-thread-only and posting to `UnitySynchronizationContext` runs on the same throttled tick — do not assume either wakes the loop.
- E: `Research~/evidence/P0-01a/wakeup.md` with histograms and a sized proposal for P0-01b.

**P0-01b · Main-thread wake-up implementation** (≤3d, sized by P0-01a) · Files: `MCPServer.cs`, `MCPServer.Logs.cs` (`Enqueue`), settings
- Agent-session-scoped: no throttling only while an agent session is active; restore the user's interaction mode after idle. Do not call main-thread-only APIs from the listener thread. If the spike finds no safe lever, record it and drop the task (P0-02/S-05 must not depend on it).
- E: p50 object read <20 ms *while an agent session is active* (Edit Mode, unfocused, macOS); idle CPU unchanged *when no session is active*.

**P0-02 · Truthful readiness (#0013) + epochs** (2–3d) · Files: `MCPServerMethods.Sync.cs`, `.Status.cs`, `.Hierarchy.cs`
- `compile_epoch`/`reload_epoch` from `CompilationPipeline` + `AssemblyReloadEvents`, **persisted in SessionState** (statics reset on reload); pending-refresh / script-write / play-transition latches; **one** readiness predicate shared by status and waits (replaces the two in D-3); wait returns `{ready, epoch, reason}`; a wait cannot span a reload in one HTTP request, so the bridge/shim re-polls after reconnect.
- E: write→compile 20/20 runs report ready only after reload; time matches Editor.log (≈7.5–8.5 s on the harness), not 99 ms.

**P0-03 · Remove fixed sleeps from the bridge** (1–2d) · Files: `nexus_bridge/routes_editor.py`
- Replace 0.5/2.0/1.0 s sleeps and the 20 s "wait for reload" with epoch-based waiting (P0-02). `no_change` returns immediately.
- E: "refresh + wait compilation" with nothing to compile returns in <1 s (report 2 took 19 s).

**P0-04 · Truthful status to agents** (2d) · Files: bridge result wrapping, `MCPServerMethods.cs` error helpers
- `Timeout`, `PartialSuccess`, `Error` → MCP `isError:true` with `code/hint/retryable`; a mutation `Timeout` → `retryable:false`, `outcome:"unknown"`, `request_id`. **`Submitted` → `isError:false` with `{state:"pending", job, next:"job_wait"}`** (flagging it as an error teaches agents to retry → duplicate test runs/writes). Stack traces only with `NEXUS_DEBUG` (D-9).
- E: contract tests for each status; v1 C8/C10 responses reproduced and now flagged correctly.

**P0-05 · Security + timeout safety** (2d) · Files: `MCPServer.Http.cs`, `MCPServerMethods.cs`
- `shutdown_server` requires the token (zombie cleanup reads the token file) — minor (D-1). Cancel flag so a timed-out *queued* call never executes later; fix the disposed-`ManualResetEventSlim` bug (D-2); `request_id` idempotency cache (~5 min) for mutating methods; SECURITY.md states `unity eval` is outside Nexus gates and `confirm:true` is an intent flag (D-11).
- E: tests — unauthenticated shutdown rejected; timed-out queued mutation not applied; retry of a *running* mutation with the same `request_id` is not applied twice; no `ObjectDisposedException` after timeout.

**P0-06 · Pin Pipeline version range + 0.8 compatibility** (1–2d, **URGENT: first task of week 1**, F14) · Files: `Editor/Pipeline/UnityMCP.Editor.Pipeline.asmdef`, tests
- Replace `0.0.0-exp` with the tested range, with an explicit test of Unity prerelease (`-exp`) range semantics; out-of-range → legacy fallback.
- Pipeline 0.8: reference `Unity.Pipeline.Attributes` (attributes moved there) via a second version-gated define or a separate thin asmdef; keep 0.7 compiling. Prefer depending on the attributes assembly only, not the full package.
- E: compiles with Pipeline absent and in range; out-of-range verified in a **second scratch project** with a different Pipeline version (a package version cannot be faked in EditMode).

**P0-07 · MCP handshake that sells Nexus** (1–2d) · Files: `nexus_unity_bridge.py`, bridge schemas
- **v2.3 (F17):** support both `server/discover` (2026-07-28) and legacy `initialize` (2025-11-25), with the same `instructions` (≈150 words: when to use Nexus vs shell, lists the profiles and `nexus_status detail:"capabilities"`) in both; deterministic `tools/list` order; image content blocks instead of base64 text; implement `prompts/list` with 3 prompts or stop declaring `prompts`; tool descriptions start with "Use when …".
- E: `initialize` transcript; screenshot arrives as an image block; token count of the default tools/list.

**P0-08 · Benchmark v2: fixture + protocol harness** (3d) · Files: `NexusFeature~/bench/`, new fixture project (separate repo or `Research~/bench-fixture`)
- Fixture and scenarios P1–P11 per `v2_analysis/C_agent_efficiency.md` §3 (Canvas+EventSystem+toggle button + planted-bug variant incl. an Overlay-only visual bug; 12 EditMode tests with 1 known failure; 3-error compile case incl. asmdef; prefab reference graph with decoy; 1,500-object scene) + a report-3 replay scene (Canvas/Image mis-setup) and a report-4 replay (menu tool with folder arguments + import).
- Three scores per sample: transport_ok / tool_status_ok / **semantic_ok (headline)**; focus/tick condition and `isPlaying` recorded; assert-mode dry run before timing; histogram with 100 ms clustering flag; N=30 warm reads / 10 for long ops; interleaved rounds; **token column for raw `unity command` outputs** (hierarchy, console, component) — decides fallback A4.
- Competitors: Unity CLI (persistent `unity mcp` / `unity shell`, and cold `unity command`), Nexus 1.6 vs 1.7. Others in P0-10b.
- E: raw JSON + `BENCH_RESULTS_V2.md`; `BENCH_RESULTS.md` v1 marked void where invalid.

**P0-08b · Agent-run harness** (3d) · Deps: P0-08
- `claude -p` / `codex exec` runner, per-run fixture reset, transcript token parsing, automated oracles per task, chose-tool classification of calls, result JSON per run. Cost is modest (~$100–400 API or subscription limits); serial Editor time (15–37 h for ~90 runs) dominates, hence P0-08c.
- E: 3 dry runs per task end-to-end, oracles agree with a human check.

**P0-08c · Harness lock + second fixture Editor** (2d)
- 2 worktree copies of the fixture, each with its own Editor, port, token and `Library`; a lock/schedule script that every Unity-facing E run and benchmark run must acquire. (Not a full farm; 2 Editors match 2 reviewers.)
- E: two concurrent agent runs on separate copies without port/token/reload interference.

**P0-09 · Adoption baseline (agent-level pilot)** (3d) [DECISION input] · Deps: P0-08, P0-08b
- **Pre-registration first** (`NexusFeature~/results/PREREG_GATE_A.md`, committed before any run): tasks, per-task definition of the "Unity-state step" that counts as relevant, oracles, thresholds, N. Prompts are written by the partner or a separate agent that has not seen the skill, then frozen.
- Tasks: 4–5 modelled on reports 1–4 + fixture (live-truth Canvas question, why doesn't it compile, who references Enemy.prefab, run packer menu item and confirm import, source-only control) + **≥2 tasks from a third-party open-source Unity project**. Arms: S1 Unity CLI + official skills + an equivalent "use the Unity CLI skills" AGENTS.md snippet; S3 Nexus MCP **pinned to the 1.6.0 release** (so P0-04/P0-07 merges cannot contaminate it). Agents: Claude Code + Codex, default settings (deferred tools on). **N=8–10 per cell on the 2–3 relevant tasks**, N=3 on the controls.
- Metrics: semantic success, **false-success rate**, tokens, wall time, calls by category, **chose-Nexus** (with Wilson 90% intervals).
- E: `NexusFeature~/results/ADOPTION_V1.md` with transcripts.

**P0-10 · Ground truth on Unity** (2d) [spike, doc only]
- On the harness: dump live `unity command` list for the installed Pipeline; confirm or refute `--detach` / `unity job` (F3); Play Mode input? Overlay UI in `capture_game_view`? profiler depth? player-build command registration? Pipeline pre-dispatch hook (could a policy layer gate `eval`? — input to G8).
- **Naming check:** register a test command as `nexus.verify_probe` and `nexus_verify_probe` via `[CliCommand]`; call both from `unity command`, `unity mcp` in Claude Code, and Codex. Until the dotted form passes everywhere, all public names are `nexus_*`.
- **Unity input check (F4):** does `simulate_key`/`simulate_pointer` exist in the installed Pipeline, and does it work in **Editor Play Mode** (runtime server with `enableInBuilds`) or only in a dev Player? Can it hold keys or type text? Result decides whether V-03 wraps Unity or builds its own injection.
- **Unity diagnostics check (F15):** output and token size of `unity recompile` on the 3-error fixture; `unity vcs affected --json` and `search` on the prefab reference graph (decides C-02); `wait_for` and `batch` semantics (inputs to V-05 and backlog `mutate`).
- **Input focus check** (0.5d): inject a key via Input System into an unfocused Editor in Play Mode, with and without Game View focus; note which `backgroundBehavior`/focus settings are required and whether they need a ProjectSettings write.
- E: `Research~/evidence/P0-10/unity_capabilities.md` with command list and screenshots. Updates F3/F4/F13.

**P0-10b · Competitor verify spike** (2d) [spike, DECISION input for Gate A2]
- Install in scratch copies of the fixture: uLoopMCP (unity-cli-loop), akiojin/unity-cli, Funplay core profile, IvanMurzak Unity-MCP, and Unity CLI alone. For each, an agent tries to detect the fixture's planted Overlay-toggle bug and the report-3 Canvas problem. Record: detected yes/no, tokens, calls, what was missing.
- E: `Research~/evidence/P0-10b/competitors.md` + raw transcripts. Updates F5.

**P0-11 · Release 1.7.0 + OpenUPM** (1–2d) · Deps: P0-02..P0-07 (**not** P0-01a/b; the release does not wait for research)
- Ships Unreleased CHANGELOG + fixes; no new features; OpenUPM listing + release automation (former R-01) so install stats exist from week 2. E: tag, release notes, clean-install smoke on macOS, OpenUPM page.

**P0-12 · Upstream outreach** (0.5d) [parallel, week 1]
- One issue/post each to Unity (Pipeline forum), IvanMurzak/Unity-MCP and uLoopMCP offering Capture V2 Overlay-correct capture as a `[CliCommand]`/library and readiness epochs. **v2.4:** also file a Pipeline feature request for `depth`/`root`/`limit`/`filter` on `get_scene_hierarchy` and grouped/deduplicated `console` output (F22). Record responses; input to §9 fallback, G9 and Gate F.

**P0-13 · Version matrix + CoreCLR spike** (2d) [spike, DECISION input for L-02/V-02b/P0-02 scope] (F16)
- Run the fixture on 6000.3 LTS, 6000.6 (Fast Enter Play Mode default) and a 6.8/Unity 7 alpha/beta when available. Record: does play entry reload? which Nexus statics (`MCPServer`, queues, capture state, runtime selector) misbehave without a reload? does compile still reload the domain?
- Output: which reload-survival work is still required (legacy projects) and which becomes static-reset work. E: `Research~/evidence/P0-13/version_matrix.md`.

**P0-14 · Headless reach spike** (1d) [spike]
- Can a cloud/background agent (Codex cloud, Claude Code web, CI) use Nexus at all? Test `-batchmode -nographics`, `unity command` against a batchmode Editor, Unity licensing in containers; list which Nexus commands could run Editor-less ("file truth": YAML/asmdef/`Editor.log` analysis). E: `Research~/evidence/P0-14/headless.md` + go/no-go for an Editor-less CLI.

**P0-15 · Package footprint** (1–2d) (F19)
- Make Input System optional via `versionDefines` (input commands report `INPUT_SYSTEM_MISSING`); constrain `UnityMCP.Runtime` to `UNITY_EDITOR || DEVELOPMENT_BUILD` (or move it behind a define) so nothing ships in release players. E: a clean project without Input System compiles; a release build contains no `UnityMCP.Runtime`.

**P0-16 · Agent-facing boundary research inputs + confirmatory stage** (v2.4, re-scoped in v2.5, **re-staged in v2.6**: P0-16a/b/d are inputs; P0-16c is now **Stage B — confirmatory, finalists only, runs only after P0-17**) [DECISION input for Gate F] — **no production code**; mock façades, routers, fixtures and scripts live in `NexusFeature~/bench/facade_mock/` and are never shipped. It tests **three different architectures**, not "façade yes/no":
- *(v2.5 arm definitions kept for the record; the v2.6 arms are defined in P0-17b: S4, S4+, C1, S5, S6-P, M.)*
- **S4 — current design** (baseline): Unity CLI / `unity mcp` / official skills + current Nexus + routing instructions.
- **S5 — selective façade / hybrid**: S4 + Nexus-normalised versions of high-volume reads (bounded hierarchy, grouped console, bounded find, stripped component reads, handles). Unity stays directly visible.
- **S6 — Nexus-primary / Unity-hidden-backend**: the agent sees only a compact Nexus surface; every primitive is delegated to the **official** Unity command (no reimplementation), large reads are normalised; Unity's own tools/CLI are not exposed as a competing surface where the client allows hiding them. Variants: **S6-domain** (≈6–15 typed domain managers + composites, generated from Unity's catalog), **S6-curated** (≤6 tools for common work + typed `describe`/`invoke` for the rest), **S6-router** (one generic tool + discovery; a minimal-schema *control*, never treated as representative of S6).
- Controls (v2.5; in v2.6 S1e is folded into S4+ as skill recipes, and S6-bypass runs only for an F/M finalist in Stage B): **S1e** (S4 + option-E skill recipes with `unity eval` snippets), **C1** (Unity-direct allowlisted to the same command subset as S6-curated, official names — tests "fewer visible commands" without Nexus), **S6-bypass** (S6 with the official Unity skill + shell CLI also installed — measures how often agents bypass the façade in realistic installs).
- **P0-16a · Schema, discovery and retrieval matrix** (1.5d; v2.6: measured for the primary client inside Stage A, multi-client only for Stage B finalists) · Deps: P0-10. Per client — Claude Code (ToolSearch on **and** `ENABLE_TOOL_SEARCH=false`), Codex default code-mode model **and** a `tool_search` (BM25) model, Gemini CLI (no deferral), Cursor if <0.5d — for every arm: persistent tool tokens at start, schema tokens actually loaded, **discovery calls** (ToolSearch / `tool_search` / code-mode `ALL_TOOLS` filtering / `nexus_describe` / `unity command` listing), **retrieval results and whether the correct capability is in the retrieved top-k**, tokens spent on discovery, discovery wall time, MCP output truncation/file-spill behaviour. Measured from real client transcripts (`count_tokens` API), never inferred from schema size. E: `Research~/evidence/P0-16/schema_matrix.md`, `retrieval.md`. Pilot inputs: `v2_analysis/G_nexus_primary/A_tool_search.md`.
- **P0-16b · Normalisation corpus + adapter sizing + option E** (2d; v2.6: this is the cheap offline test for §18 H6, run before Stage A agent cells) · Deps: P0-08 fixture, task freeze. Output classes on the fixture **and** ≥1 third-party scene with irregular depth, repeated logs, missing references, component/serialized-state questions, compile and test diagnostics; budgets `summary` / `normal` / `full-bounded`. For each: raw Unity tokens and what the client actually delivers (truncation/file spill), option-E output, normalised output; frozen sufficiency question answered from the output + handle fetches (count fetches, retries, omissions of critical data). **Catalog-driven adapter** measured on Pipeline N and N-1: % of commands representable from `unity list`/tag metadata without hand code, hand-mapping count, ambiguous commands, generated-schema size per surface and per manager, command-specific adapters (async/poll, image, confirm, code-exec), breakage across versions. Pilot input: `G_nexus_primary/B_catalog_adapter.md`.
- **Task freeze first:** task set, oracles and sufficiency questions are written by an author blind to the façade/normaliser design and frozen before normalisers change (pilot set: `G_nexus_primary/TASKS_v1.md`). ≥2 tasks from a real third-party open-source project; real-session replays where possible. Tasks cover the persona's real workflow (UI diagnosis, C# edit + compile truth, object/component relationships, references, broken-Editor diagnosis, running existing editor tools, a simple scene change through an official primitive, import state), not only reads.
- **P0-16c · Stage B — confirmatory architecture benchmark (finalists only)** (v2.6; ≈4d agent time + Editor time sized by the Stage B power calculation: D1 at 80% power needs ≈235–290 paired runs per arm with mild task heterogeneity (≈1,000+ with strong heterogeneity), so S4 + C1 + 1–3 challengers ≈ 900–1,500 runs ≈ 30–55 Editor-hours on the primary client; per-class verdicts and second-client checks will often end in I; the earlier ≈130–320 h figure described the all-arms × all-clients matrix, which is **no longer planned**) · Deps: **P0-17 complete (finalists named)**, P0-16a/b, P0-08b/c. Paired task/seed design; frozen ground truth and thresholds (`PREREG_GATE_F.md`) before the first Stage B run; independent scoring; contamination controls; ≥2 informative third-party tasks passing the P0-17 quality checklist; optional pre-specified interim look with alpha spending (sequential stopping). Other clients (Claude Code, Gemini CLI, Cursor if relevant) only for finalists **after** a clear primary-client signal. All arms on the same frozen tasks and oracles; arms differ only in the agent-facing surface and its one-paragraph instruction file; S6 must reach the same Unity capabilities the tasks need (gaps listed, never faked). Headline metrics per arm: **semantic success, false-success, total tokens per successful task, calls per successful task**; plus input/schema/discovery/tool-output/answer tokens, discovery calls, wrong-capability calls, invalid-argument calls, retries, wall time, Unity time, normalisation time, human intervention, **capability coverage** (tasks completed entirely through the compact surface / needing an escape hatch / needing direct Unity; real capability loss vs discoverability loss). Primary client first (the P0-17 primary client); Claude Code / Codex / Gemini CLI (no-deferral case) only for finalists after a clear primary-client signal. **Pilot lessons built in:** any Nexus-primary finalist is S6-P (§18 H2: typed contracts, real composites, no `eval` through Nexus — never a flat regrouping of all 154 actions), S4 includes the official Unity skill, every arm runs behind the same logging proxy, and the harness starts Nexus and protects Unity's port file (pilot amendments A3–A4). E: `NexusFeature~/results/FACADE_V1.md` with transcripts.
- **P0-16d · Delegated-write research question** (0.5d, desk): can official Unity write primitives be delegated behind Nexus while preserving token/sandbox/approval/idempotency/audit guarantees? Answer per write category (bounded Undo writes, asset/file writes, settings, destructive, build/package, `eval`/`run_script`/`batch`). Pilot input: `G_nexus_primary/D_security.md`. No implementation; production policy stays read-only until this is answered and Gate F passes.
- **Work classification (v2.6).** *Architecture-independent — continues on schedule:* truthful readiness and epochs (P0-02), job model (L-02), error envelopes (S-04), Capture/Look (S-07), bounded-output infrastructure, token budgets and handles/cursors (C-04, S-06), legacy-noise cleanup (S-10), discovery metadata (S-11), Pipeline compatibility (P0-06, A-01), skills/discoverability (S-01), `nexus_snapshot` (L-03), `nexus_diagnose` (S-06), `nexus_compile` (S-05), `nexus_run` (L-04), and the benchmark/testing infrastructure already in the roadmap (P0-08/08b/08c). *Architecture-dependent — not scheduled as production implementation until Gate F:* a full Nexus-primary façade, broad Unity write delegation, a generated domain-manager façade, replacing Unity primitives with Nexus actions, a fully hidden-Unity distribution model, a large routing layer, and any production escape/`invoke` system needed only by S6. Bench-only prototypes for P0-17/P0-16c live under `NexusFeature~/bench/` and never ship.
- **Pilot (2026-10-03, done before the full spike):** one client (Codex), 5 arms, ≈54 runs — `G_nexus_primary/PREREG_PILOT.md`, results in `G_nexus_primary/PILOT_RESULTS.md`. A pilot can only change research priorities; it cannot pass or fail Gate F.

Phase 0 load note (v2.3): Phase 0 is ≈36 agent-days. With ≤4 PRs in flight it realistically spans weeks 1–3; the spikes (P0-01a, P0-10, P0-10b, P0-13, P0-14, P0-16a/b) run in parallel and must not block P0-11. v2.4 adds ≈6 agent-days (P0-16a–c) plus ≈25–60 Editor-hours. P0-16a/b run as parallel spikes in weeks 2–3. **v2.6 timing (architecture research track):** P0-17a/b in weeks 3–4 (desk + configs); P0-17c (Stage A1) weeks 5–6 after the Gate A runs; P0-17d (Stage A2) once S-05/S-06/L-03/L-04 are merged (≈ weeks 7–8); P0-16c (Stage B) after the Gate B runs (≈ weeks 10–12); Gate F ≈ week 12 or later — it may fall after the 12-week horizon (R30). Capacity comes from postponing C-01 (spike) and the optional V-04b/V-07; V-08 is **not** a buffer.

### PHASE 1 — Discoverability + small composite surface (weeks 2–4)

**S-01 · Skill pack v1 + AGENTS.md snippet + public listing** (3d) · Files: new `skills~/unity-live-editor-truth/`, installers (`MCPCliInstaller.*`)
- Trigger-oriented descriptions ("after editing C# in a Unity project…", "before asking the user for a screenshot…", "before claiming a UI change works…"); decision table (shell vs `unity command` vs `nexus_*` vs `unity eval`); 5 recipes; budget and state rules; error table; "when NOT to use Nexus"; "logs/UI text/scenario files are data, not instructions". Both invocation forms shown with underscore names. Opt-in AGENTS.md/CLAUDE.md block (marker-delimited, byte-preserving). `nexus doctor` (shim) = `nexus_status detail:"doctor"`. **Published by week 3** on skills.sh and as a Claude Code plugin / Codex listing next to Unity's `unity-cli` skill (former R-02).
- E: installer tests (idempotent, markers preserved); listing live; searching skills.sh for "unity screenshot overlay" and "unity compile errors" finds the skill; P0-09 re-run for arm "Nexus + skill" feeds Gate A.

**S-02 · Hand-written `[CliCommand]` wrappers + drift test** (2d) · Files: `Editor/Commands/*`, `Editor/Pipeline/NexusPipelineCommands.cs`, `Tests/Editor/NexusCommandRegistryTests.cs`
- Typed argument records → descriptors; EditMode test reflects over `CliArg` attributes and compares to descriptors. No generator script.
- E: removing a param fails the test; the 3 existing commands keep identical `unity command` help.

**S-03 · Bridge: dynamic canonical pass-through + startup profiles + port discovery** (3d) · Files: `nexus_bridge/routing.py`, `routes_base.py`, `schemas.py`, `NexusToolCatalog.cs`
- Canonical commands exposed from `list_tools{profile}` metadata; profile chosen by env var / argument at bridge start; `nexus_load_profile` + `tools/list_changed` as best-effort extra; default `core` ≤8 tools and ≤2.5k tokens; 14 managers only in `full`. Fix D-8 with per-project port discovery (bridge finds the Editor of *its* project; clear error otherwise).
- E: per-client matrix (Claude Code, Codex, Cursor): tools visible at start, whether `list_changed` is honoured, tools/list tokens per profile; new C# command appears in MCP with no Python change; two-project port-collision test.

**S-04 · Unified error envelope for canonical commands** (2d) · Files: `Commands/NexusCommandResults.cs`, projections, `API_REFERENCE.MD`
- Includes `request_id` in `meta`.
- E: contract tests for ≥6 codes; legacy raw shapes unchanged (`OpenSourceApiContractTests`).

**S-05 · `nexus_compile`** (3d) · Deps: P0-02 · Files: new command, replaces scraping in `GroupCompileErrorsCommand`
- `CompilationPipeline.assemblyCompilationFinished` messages (file/line/col/id), persisted in SessionState across reload (fixes D-10); optional sandboxed writes with the extended deny-list (rule §6.8), write params legacy-only in the Pipeline projection; "did you mean" via `symbol_index` for CS0246/CS1061; ±3 lines of source context in `normal` detail; detect Hot Reload for Unity / FastScriptReload and warn that the verdict reflects Unity's compile, not the hot-patched state. **v2.2:** in pipeline mode, obtain compile truth from `unity recompile` (F15) and add only grouping/hints/context on top; do not duplicate Unity's error listing.
- E: fixture 3-error case returns exactly 3 errors with positions; token size of summary response; `no_change` <50 ms **only if P0-01b landed**, otherwise report measured time.

**S-06 · `nexus_diagnose` + `nexus_get` handles + capabilities map** (3d)
- Opaque server-issued handles; retention cap; `nexus_status detail:"capabilities"` (≈300 tokens, grouped by job with profile names).
- **v2.2 Safe-Mode-resilient path:** when Pipeline/Editor scripting cannot load (compile errors → Safe Mode), diagnose from `Editor.log` + asmdefs + last persisted compile result (§14b.2).
- E: fixture broken state → ≤15-line prioritized report; handle fetch with range/crop; path-like handle rejected.

**S-07 · `nexus_look` image hygiene + Scene View** (3d) · Files: Capture command/projections
- JPEG default, **default long side 768 px** (≤0.8k tokens for any aspect, F12), 1024 opt-in; crop to object/UI element; Scene View capture framed on an object; file-path mode for CLI; never both PNG and JPEG.
- E: token cost per capture measured for 16:9 and 1:1; Overlay marker test still passes; Scene View framing on fixture object.

**S-08 · External testers + usage counters** (0.5d, week 3)
- Call for 5–10 testers via the LinkedIn/Telegram contacts who already engaged; a short feedback form. Report OpenUPM downloads, GitHub traffic, skills.sh installs at each gate. (No in-product telemetry.)
- E: `NexusFeature~/results/USAGE.md` updated at Gate A and Gate B.

**S-09 · Hook pack** (3d) (v2.3, G13)
- Opt-in hooks for Claude Code / Codex / Gemini CLI: after an edit to `*.cs` → `nexus_compile` summary (≤200 tokens); before the agent's final answer (Stop hook) → `nexus_diagnose` guard. Fail open when no Editor is reachable; never block the user's session; installed and removed by the Nexus installer with marker-delimited config.
- E: P0-09 arm "Nexus + hooks" runs; hook overhead per edit measured; uninstall leaves configs byte-identical.

**S-10 · Legacy noise cleanup** (2d) (v2.6, architecture-independent) · Files: bridge profiles, `NexusToolCatalog.cs`, legacy dump methods
- Remove `unity_dump_scene_graph` and other unbounded dumps (`read_logs` without limits) from default agent surfaces (bridge default profile, skills, instructions); where still reachable (`full`), apply bounded defaults (depth, node count, `max_tokens`) and a handle for the rest; mark `deprecated` with `replacement: nexus_snapshot` per §5.2. E: on the fixture's 1,376-object scene, the largest default-surface scene read is ≤ the S-07/L-03 budget; pilot evidence: 12 S4 outputs >40k chars came from this dump.

**S-11 · Discovery metadata** (1.5d) (v2.6, architecture-independent; also builds the H4 arm) · Files: bridge schemas, `[CliCommand]` descriptions, skill text
- Top-level tool descriptions carry the action vocabulary ("Use when…", key nouns), search hints where clients support them (`searchHint`, `_meta`), task-named tools, profile/allowlist guidance in instructions and skills. Pilot input: Agent A's offline result that adding action lists to Nexus descriptions raised retrieval recall@8 from .55 to .88. E: offline retrieval set re-run (no agent runs) before/after.

**▶ GATE A (end of week 4), pre-registered in `PREREG_GATE_A.md`:** (v2.3: A1 is measured separately for "agent chose Nexus" and "Nexus ran via hooks"; hooks count as adoption only if the user opted in)
- **A1 adoption:** re-run P0-09 with S4 = Nexus 1.7 (instructions + skill + core profile + CLI). Pass if Nexus is called for the Unity-state step in **≥60% of pre-registered relevant runs** AND the lower Wilson 90% bound for S4 is above the upper bound for S3 AND false-success is not higher than S1 (reported with interval). External-user feedback (S-08) is reported, not a hard criterion yet. Fail → stop feature work, 2 weeks on discoverability, re-run; if still failing → fallback (§9).
- **A1 is unchanged by v2.4.** S5/S5r/S1e runs and the high-volume tasks do not count toward A1 and A1 is not re-defined; "chose Nexus" for S4 is still the adoption question for the current design. A façade arm makes "chose Nexus" meaningless *for that arm*, so S5 is judged only by Gate F.
- **A2 verify room (desk decision from P0-10b):** if **≥2 competitors detect the planted bug**, `nexus_verify` is not built as a flagship; Phase 3 shrinks to "verify-lite" (V-05 assertions usable by `nexus_snapshot`, Overlay capture as a `[CliCommand]` others can call, skill recipes that combine competitor/Unity input with Nexus assertions). Otherwise Phase 3 proceeds.

### ARCHITECTURE RESEARCH TRACK (v2.6; parallel to Phases 1–3, weeks 5–12; second fixture Editor)

**P0-17 · Architecture Theory Validation — Stage A, cheap falsification** (planned, **not started**) [DECISION input: names the challengers for P0-16c] — kills weak theories from the §18 register with small, paired, large-effect tests; it cannot confirm an architecture. Arm builds are bench-only configurations of existing code; no new production code and no bench re-implementation of Nexus composites. Stage A data are **never pooled** into Stage B (fresh runs only), which limits selection-by-noise (≈15% chance that one of five equal challengers survives by chance).
- **P0-17a · Register + pre-registration** (1d, desk) · Deps: none. Freeze `PREREG_P0-17.md`: theories in scope, arms, ≈8 tasks (incl. ≥1 third-party task passing the checklist below), primary client (default **Codex**, where a harness exists and code-mode discovery cost is measured; Claude Code if its CLI is available — the same client for Stage A and Stage B's primary run), model and reasoning setting, the §18 rules, the cost decider (total tokens per success; uncached reported), the drop-one-task rule, budget. Written by an author who does not build the arms; frozen before any Stage A run.
- **P0-17b · Arm configurations (bench-only)** (3d) · Arms: **S4** = the frozen Nexus 1.7.0 release + `unity mcp` + official Unity skill (fixed baseline; it does **not** absorb S-10/S-11); **S4+** = S4 + S-10/S-11 + skill routing / option-E recipes (H4); **C1** = official Unity allowlisted to the same task-relevant command subset, official names (H1 control); **S5** = S4 + normalised reads (H8); **S6-P** = the compact typed Nexus surface built from the *merged* composites (`nexus_status/snapshot/diagnose/compile/look/run/get` + ≤1 bounded query), Unity hidden, no `eval` through Nexus, ≤3 typed bounded-write actions in a separate write tool for research observation only (H2), no generic invoke; **M** = S6-P reads/composites + a declared Unity-direct path for writes and the exceptional categories (§4) (H3). S6-P and M therefore differ exactly in the write/exception boundary. Each arm's task capability is checked before runs (gaps listed, never faked).
- **P0-17c · Stage A1 — early kills** (≈2d agent time) · Deps: P0-17b, Gate A runs done (end of week 4). Arms S4, S4+, C1, S5 on ≈8 tasks × N≈6, paired by task and seed (≈190 runs). Decides H4 and H8 provisionally.
- **P0-17d · Stage A2 — Nexus-primary cells** (≈2d agent time) · Deps: S-05, S-06, L-03, L-04 **merged** (no bench re-implementation; if they are late, A2 waits). Arms S6-P, M + **re-runs of S4 and C1** (and S5 if it survived A1) on the same tasks/seeds in the same session window, so H1/H2/H3 comparisons are paired (≈190 runs).
- **P0-17e · Desk + micro-tests for H5/H7** (1.5d; no agent runs) · H5: per-call latency, reconnect after domain reload, sandbox behaviour and port-file safety for warm MCP vs Pipeline HTTP vs persistent shell. H7: run the existing catalog generator and pilot normalisers against Pipeline N / N-1 / N+1 where available; record breakage, repair time, LOC changed, contract-test failures.
- **P0-17f · Challenger selection** (0.5d, desk, independent reviewer) · Each theory → FALSIFIED / SURVIVED CHEAP TEST / INSUFFICIENT EVIDENCE per the frozen rules; update §18. **S4 and C1 are permanent Stage B comparators and do not count as finalists**; up to 3 challengers (from S4+, S5, S6-P, M) proceed. If an F/M challenger proceeds, the S6-bypass cell (official Unity skill + CLI installed) runs alongside it in Stage B. E: `G_nexus_primary/STAGE_A_RESULTS.md`, updated §18.
- **Third-party task quality checklist (P0-17 and Stage B).** Before a third-party task is frozen: (1) a human or independent agent sanity-checks the ground truth; (2) no obvious alternate correct explanation exists (the pilot's T9★ failed this); (3) every compared arm has the required capability; (4) the task is not trivially solvable by unrelated grep/file inspection unless that is intentionally allowed (the pilot's T3 was); (5) the oracle is frozen before runs; (6) confirmatory work uses ≥2 informative third-party tasks.
- **Budget (honest):** Stage A ≈ 336–384 runs; at the pilot's ≈1.7 min per run incl. reset, plus ≈16% voids/re-runs and occasional 8–9 min runs, ≈12–25 Editor-hours. Stage B is sized by its own power calculation (P0-16c). Together they replace the earlier ≈1,600–2,400-run, ≈130–320-hour all-arms × all-clients matrix. Capacity: S-10, S-11 and P0-17 add ≈13.5 agent-days, funded by postponing C-01, V-04b and V-07 (already optional) and by running this track on the second fixture Editor; Stage B is scheduled after the Gate B runs (week 9) so the two do not compete for Editors.

**▶ GATE F — final confirmatory architecture gate (v2.6; earliest ≈ week 10–12, may fall after the 12-week horizon; does not block Gate A, Gate B or Phase 2)** — evaluates **up to 3 challengers** that survived P0-17 (§18) against the permanent comparators **S4 and C1**, using P0-16c (Stage B). Sequence: existing evidence → §18 Theory Register → P0-17 cheap falsification → challengers (+ S4, C1) → P0-16c confirmatory benchmark → Gate F → implementation *decision* (design first, never automatic implementation). Thresholds, tasks, oracles and the analysis script are frozen in `PREREG_GATE_F.md` before the first Stage B run; the prereg author may tighten, never loosen. Exploratory arms (anything not a named finalist) may run alongside but are reported separately and cannot pass Gate F. No per-task vetoes (they fire by chance 50–68% at equal rates); no single prerequisite such as a mis-routing threshold (mis-routing is reported). Power is stated honestly in the prereg; if the run budget cannot reach the margins below, the expected outcome is **I** and that is acceptable. A pre-specified interim look with alpha spending may stop Stage B early for a clear winner or futility.

**Primary metrics (paired by task and seed):** (1) semantic success; (2) false-success; (3) total tokens per successful task; (4) uncached tokens per successful task; (5) total tool calls per successful task; (6) invalid-argument / wrong-tool calls; (7) discovery/retrieval calls; (8) escape-hatch usage; (9) human intervention; (10) capability coverage. Secondary: wall time, emitted tool-output tokens, schema tokens loaded, maintenance LOC. Raw compression ratio is never a headline metric. Every success figure is reported twice: **observed** and **production-policy** (runs that needed `eval`/`eval_file`/`run_script`/agent `batch` *through Nexus* count as failures) — the architecture is judged on observed success under the arm's declared capability set; the production-policy view decides deployability (security is evaluated separately, D9).

Decision matrix — each challenger vs S4 and vs C1 (and vs each other), pooled over frozen tasks and separately for read/diagnose and write task classes:

| Dimension | Pass condition for the challenger vs its comparator |
|---|---|
| D1 semantic success | non-inferior on **production-policy** success (identical to observed for arms that do not route `eval`/`run_script`/agent `batch` through Nexus): lower 90% bound of the paired difference ≥ −10 pp; observed success also reported; per-task differences reported, no per-task veto |
| D2 false-success | upper 90% bound of the paired difference ≤ +5 pp |
| D3 cost per success | decider: Σ total tokens over all runs ÷ successes per task; geometric mean of per-task ratios ≤ 0.8 with bootstrap 90% CI upper bound < 1.0 (power is capped near 50% if the true ratio is exactly 0.8 — the gate detects ratios ≈0.7 or better); uncached tokens reported and must not point the other way (ratio ≤ 1.0) |
| D4 calls per successful task | median ratio ≤ 0.8, CI upper bound < 1.0 |
| D5 routing/discovery failures | wrong-tool + invalid-argument + failed-discovery calls per run ≤ comparator's; retries not higher |
| D6 capability coverage | ≥90% of tasks completed without leaving Nexus to raw/unbounded output or direct Unity; typed discovery inside Nexus counts under D5, not as an escape; **for M only**, Unity-direct calls in its pre-declared categories are not escapes (owner requirement: an escape path is a designed feature) but are reported; every other escape classified as discoverability loss or capability loss |
| D7 maintenance | catalog-driven pass-through covers ≥80% of exposed delegated actions where generation applies; hand-written normalisers/adapters ≤ 600 lines total with N/N-1 contract tests; measured on real Pipeline versions (P0-17e), not estimated |
| D8 latency | median wall time per successful task not >10% worse; added per-call overhead ≤50 ms vs a warm Unity call |
| D9 security | no weakening of §6 rule 8 or the §4 security boundary; delegated writes only per P0-16d; `eval`/`run_script`/agent `batch` never delegated |
| D10 client portability | for the **selected** outcome only (not every arm): its D3 or D4 advantage holds in ≥2 of the 3 primary clients (Codex, Claude Code, Gemini CLI) and it is not worse on D1 in any of them, before an implementation design is approved |

Outcomes (all first-class; never forced binary):
- **A — direct Unity + Nexus composites**: Unity primitives stay agent-visible; Nexus focuses on Live truth, diagnostics and workflows (today's architecture). Chosen when no challenger passes and intervals exclude the challengers' pass regions.
- **C — selective façade**: Unity stays directly available; Nexus normalises only high-value unbounded reads and workflows. Requires D1, D2, D9 and **D3 itself** on the read/diagnose class, plus D7, D8, D10.
- **F — Nexus-primary**: Nexus is the primary AI-facing interface and delegates official Unity primitives internally (S6-P). Requires D1, D2, D6, D9, ≥2 of D3/D4/D5, D7, D8, D10, and that C1 does not match it (below).
- **M — mixed Nexus-primary**: Nexus is the preferred route for normal tasks; Unity-direct for writes and the declared exceptional categories (§4 security boundary). Requires F's conditions on the read/diagnose class, write tasks not worse than the current design on D1, ≥90% of Unity-direct calls inside the declared categories, and that C1 does not match it.
- **E — skills / Unity sufficient**: official Unity + better skills/allowlists/discovery (C1, S4+) performs as well as the best façade on D1/D3/D4; then the façade is compared on the product table (maintenance burden, client portability, prompt dependence, discoverability for users without the official skill, reliability across models, API stability, UX consistency), scored better/same/worse by an independent reviewer. Cheapest-to-build is one row, not the rule.
- **I — insufficient evidence** (checked first, before A): the D1 interval contains both −10 pp and 0, or the D3 interval contains both 0.8 and 1.0. Keep the existing architecture; no forced decision; the theory stays open in §18.
- **C1 attribution rule**: if C1 (official Unity, curated/allowlisted to the same task-relevant subset) matches F or M within D1 and D3/D4, the gain is attributed to capability-surface reduction, not to Nexus; the recommendation becomes "curate Unity's own surface (skill/config)" (E), not a Nexus façade.

### PHASE 2 — Flagship "Live truth" (weeks 4–6)

**L-01 · Seams + `EditorUnityPrimitives`** (3d): `IMainThreadDispatcher`, `IConsoleLogSource`, `IUnityPrimitives` with one public-API implementation. E: grep test — Core files contain no `MCPServer.` references.
**L-02 · Resumable job store** (3d): `NexusJobStore` with step cursor in SessionState + `[InitializeOnLoad]` resume, idempotent step contract, size/retention cap, `nexus_job_status/wait/cancel`. E: a job that crosses a forced `RequestScriptReload` **and** a play-entry reload completes 20/20; `interrupted` + partial artifacts only after a simulated Editor restart.
**L-03 · `nexus_snapshot`** (3d) · Deps: L-01, S-07: selection or path → hierarchy path, active state, RectTransform, component summary, serialized refs (missing refs flagged), import state (last import, pending refresh), optional cropped Game/Scene View image; ≤1k tokens in `summary`. E: report-3 replay — an agent resolves the Canvas problem with zero user screenshots; tokens per success vs S1.
**L-04 · `nexus_run`** (3d) · Deps: L-02: run a menu item or a method marked `[NexusRunnable]` (allowlist; arbitrary code stays `unity eval`) with arguments, then wait event-driven for import/compile/reload via L-02; if the main thread stops ticking during the run, return `PROBABLE_MODAL_DIALOG` with elapsed time (best-effort detection; native folder panels cannot be intercepted and this is not promised). E: report-4 replay (packer tool with folder arguments) completes and reports the import finished; a blocking dialog is reported, not hung.
**L-05 · Windows validation** (3d, moved before Gate B): install, token, ports, Capture V2 on D3D11/D3D12/Vulkan (orientation via `SystemInfo.graphicsUVStartsAtTop`; F10 unconditional flip), input focus, bridge + Python presence, skills. E: benchmark v2 protocol run on Windows.
**L-06 · Windows fixes** (≤3d each).
**L-07 · Release 1.8.0-preview "live truth" + demo** (2d): docs, CHANGELOG, report-3/report-4 replay GIF ("Your agent stops asking you for screenshots"); second call to external testers.

### PHASE 3 — `nexus_verify` (conditional on Gate A2; weeks 6–9)

Task list and oracles for Gate B are frozen in `PREREG_GATE_B.md` **before V-06a starts**, including ≥1 task uLoopMCP / akiojin are designed for.

**V-03 · Keyboard + unified `nexus_input`** (3d): key down/up/press/text/hold, action lists; **Input System only** (legacy `UnityEngine.Input` has no injection API → `LEGACY_INPUT_UNSUPPORTED`); focus handling per P0-10 input check; no ProjectSettings writes, or restore after run. E: PlayMode fixture — WASD moves player, text field receives string, with Editor unfocused.
**V-04a · UGUI targeting** (3d): EventSystem raycast, `NO_EVENTSYSTEM` error, `input_path: eventsystem|invoke` reported. E: fixture P9 (UGUI) passes 10/10.
**V-04b · UI Toolkit runtime panels** (3d, optional before Gate B): `panel.Pick` + synthesized pointer events under the Input System. E: UI Toolkit variant of P9 10/10.
**V-05 · Assertions** (3d): log present/absent, object property op value, UI element visible/text, pixel region color±tol, no new errors; structured fail reasons. Also used by `nexus_snapshot` (verify-lite). E: unit tests per assert.
**V-02b · Resumable scenario runner** (3d) · Deps: L-02: scenario step cursor survives play-entry reload; idempotent step contract. E: verify survives the play-entry reload with domain reload enabled and completes 20/20.
**V-06a · Runner + DSL core** (3d) · Deps: V-02b, V-03, V-04a, V-05: closed step set (5 step types: play, input, wait-frames/until, assert, capture), per-frame predicates, summary ≤400 tokens. E: planted bug is caught; DSL schema tokens measured. **Gate B runs on V-06a.**
**V-06b · Reporting + saved scenarios** (3d): capture-on-fail, report in job dir, scenarios saved under `NexusScenarios~/` (non-imported) as regression tests; **v2.2:** also emit NUnit/JUnit XML so results plug into `unity test` reports and CI (§14b.2). E: 3 recipe scenarios 10/10.
**V-07 · Verify via `unity command`** (1d, only if P0-10 confirms `--detach`/`unity job`): parity table legacy vs pipeline projection.
**V-08 · Static-state check** (2d) (v2.3, replaces the old "fast path" task, F16): `nexus_diagnose` flags mutable static fields / static event subscriptions / background threads that survive Play Mode entry without domain reload (with `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` reset hints). E: fixture with planted statics; no false positives on Unity/Nexus packages.
**V-09 · Release 1.9.0-preview "verify"** (1d): docs, CHANGELOG, GIF.

**▶ GATE B (end of week 9), pre-registered in `PREREG_GATE_B.md`:** agent-level run on the frozen verify tasks, macOS **and** Windows: Nexus + Unity CLI vs Unity CLI + official skills (with equivalent snippet) vs **the best verify competitor from P0-10b**. N≥10 per task per arm. Continue only if Nexus's semantic success ≥ best competitor **AND** false-success ≤ best competitor **AND** (≥15 pp higher success OR ≥30% fewer tokens-per-success); tokens alone never pass the gate. External: ≥5 non-team users tried Nexus on their own project and ≥3 report it was used (S-08). Otherwise → verify-lite + fallback options (§9); Live truth continues.

### PHASE 4 — Token-lean context (weeks 8–11, reduced)

**C-01 · `nexus_context` spike** (3d) [research]: task string + `max_tokens`; cached index in `Library/Nexus/` invalidated by postprocessor + compile events; `fingerprint` in responses; `.gitignore` + deny-list. E: tokens-to-first-correct-edit ≥30% lower than shell-only on the benchmark tasks, or the task is dropped (stays out of `core`).
**C-02 · `nexus_find_references`** (3d): project-wide cached reverse index over YAML; **Force Text serialization only** (declared limit; Binary mode → `UNSUPPORTED_SERIALIZATION`). E: fixture P10 returns exactly the 6 referrers, not the decoy.
**C-04 · `detail` / `max_tokens` convention** across canonical commands (2d). E: bench token column per command.

### PHASE 5 — Hardening, release & distribution (weeks 10–12)

**W-03 · Capture fallback + self-test** (2d): startup check that `GameView.m_RenderTexture` still resolves; fallback to Unity capture with an "Overlay may be missing" warning.
**A-01 · Pipeline N/N-1 CI contract job** (2d) + monthly check on Pipeline pre-releases.
**A-02 · Hide/deprecate sweep 1** (2d): per §5.2 policy; docs + CHANGELOG + `list_tools` flags.
**R-03 · Public TOKENS.md + COMPARISON.md** (2d): generated from benchmark v2 JSON only; compares Unity CLI alone, IvanMurzak Unity-MCP, CoplayDev, Funplay core, akiojin/unity-cli, uLoopMCP with scenario oracles; states honestly where each wins (e.g. Funplay for breadth/execute_code, uLoop for input) and where Unity CLI alone is enough. **v2.3:** after Gate B, publish the fixture, oracles and harness as a separate MIT repo `unity-agent-bench` and invite competitor configs (useful even if Nexus loses).
**R-04 · Demo** (2d): replays of the real failures (report 3 screenshots, report 4 folder dialog) side by side with Nexus; if Gate B passed, add "Agent says it's fixed. Nexus proves it." recorded with `nexus_verify` itself; demo scripts are bench scenarios.
**R-05 · Release 1.9.0** (1d): live truth GA, context tools, Windows supported, verify GA only if Gate B passed.

**Façade backlog (only if Gate F selects C, F or M; not scheduled; design before implementation):** C → F-01 normalised read commands in Core (`[CliCommand]`, read-only, `detail`/`max_tokens`/handles per C-04) merged into the ≤8 core budget, each schema ≤600 tokens · F-02 contract tests per normaliser against Pipeline N/N-1 in A-01 · F-03 retirement check: drop any Nexus read once Unity ships equivalent bounds (Principle 1). F or M → F-10 Nexus-primary / mixed **design** task (surface, escape boundary, transport per H5, catalog-driven adapter where it applies, security per P0-16d, migration) — design only; implementation needs its own owner decision.

**Later / backlog (not scheduled):** `nexus_scene_diff` + bounded `scene_snapshot` (former C-03, closes #0016) · `nexus_ui_check` (former C-05) · extract `Nexus.Core.Editor` asmdef (former A-03, only after L-01) · `nexus_perf` (**v2.2: do not build raw capture** — Unity signals profiler before GA; at most a perf-budget assert inside verify via `ProfilerRecorder`) · `nexus_player_check` (same scenario over `unity command --runtime` with `RuntimeOnly` steps, §14b.3; spike first) · ~~`nexus_mutate`~~ dropped in v2.2 (Unity `batch` is transactional with single Undo + `dry_run`) · opt-in local usage export · studio policy/audit layer over Pipeline (G8, only if P0-10 finds a pre-dispatch hook).

Capacity: ~51 tasks × ~2.2 d ≈ 110 agent-days (+≈6 agent-days for P0-16 and ≈10 agent-days for P0-17 (v2.6); Editor time ≈12–25 h for Stage A and ≈30–55 h for Stage B instead of ≈130–320 h for an all-arms matrix; S-10/S-11/P0-17 ≈13.5 agent-days funded by postponing C-01, V-04b, V-07; C-01 and optional V-04b/V-07 postponed) (spikes P0-01a/P0-10/P0-10b/C-01 included); with ≤4 PRs in flight, 2 fixture Editors and 2 reviewers this fills 12 weeks only if the gates pass. If Gate A2 says "verify-lite", Phase 3 drops to ~4 tasks and frees ~2 weeks for Live truth polish and context. Optional tasks (V-04b, V-07, V-08) are the buffer.

---------------------------------------------------------------------
## 8. v1 → v2 MAPPING (what happened to every v1 task)
---------------------------------------------------------------------

| v1 | v2 verdict | Where it went / why |
|---|---|---|
| T01 screenshot | DONE (acceptance numbers missing) | Numbers come from P0-08; Windows in L-05 |
| T02 reload survival | SHRINK | P0-02 epochs + L-02 resumable job store (reload is the normal path) |
| T03 run_tests | SHRINK | Failure summarizer inside `nexus_diagnose`/verify; Unity has `run_tests` |
| T04 QA cycle C12 | CONDITIONAL → V-03..V-06b | Only if Gate A2 says verify has room; needs real fixture (P0-08) |
| T05 perf CI gate | DROP | Tick noise; replaced by semantic + agent-level benchmarks |
| T06 401 matrix | SHRINK | Only if it reappears; P0-05 covers auth hole |
| T07 dispatch tax | SHRINK → P0-01a/b | It is a wake-up problem, and Pipeline has it too; spike first |
| T08 payload diet | KEEP → C-04, S-07 | Tokens are a core axis |
| T09 bridge efficiency | DROP | Bridge is frozen/thin |
| T10 14→32–40 tools | **REVERSED** | ≤8 default + profiles + skills + capabilities map (§5.1). v2.4: a delegated façade over Unity commands is tested in P0-16 / Gate F, not assumed |
| T11 response contract | SHRINK → S-04 | New canonical commands only |
| T12 zero-config wizard | DROP | `unity mcp configure` / skills; installer frozen; `nexus doctor` in S-01 |
| T13 readiness enum | KEEP → P0-02 | |
| D1 bridge language | DROP | Transport is Unity's |
| D2 / T21 execute_code | DROP | Use `unity eval`; documented in skills/COMPARISON; no forwarder |
| D3 broker | DROP | |
| D4 Unity floor | DECIDED: 6000.0 | |
| D5 layout | DONE | |
| T22 Pipeline adapter | DONE | Extend only via S-02 / V-07 (projection only) |
| T23 keyboard + raycast | CONDITIONAL → V-03 (Input System only), V-04a | After P0-10 input check and Gate A2 |
| T24 scene/inspector capture | SHRINK → S-07, L-03 | `nexus_look` crop + Scene View framed on object; `nexus_snapshot` for inspector values |
| T25 QA tools | CONDITIONAL → V-05 (always), V-06a/b (if Gate A2) | Assertions reused by snapshot |
| T26 profiler | BACKLOG | Only if Unity's is shallow |
| T27 skills | **TOP PRIORITY → S-01** | Published by week 3 |
| T28 audit backlog | SHRINK | Only issues new commands depend on (#0013 in P0-02; #0016 moves to backlog with scene_diff) |
| T31 docs overhaul | SHRINK | Docs for the new small surface |
| T32 OpenUPM | KEEP → P0-11 | Moved to week 2 for install stats |
| T33 comparison tables | SHRINK → R-03 | Task-success data only; 6 competitors |
| T34 onboarding smoke | DROP | |
| T35 multi-editor | DONE | D-8 port discovery in S-03 |
| T36 plugin API | DROP | `[CliCommand]` is the plugin API |
| T41–T47, T50 | DROP | Except T43 semantic_find (feeds C-01), T44 scene_delta (backlog), T45 step_frame (qa profile) |
| T48 demo | KEEP → L-07, R-04 | Real-failure replays |
| T49 SECURITY.md | KEEP (in P0-05 PR) | States `unity eval` is outside Nexus gates; `confirm:true` is an intent flag |
| ImageCapturePlan M5 + 11 gates | REPLACED | §9 rule |

---------------------------------------------------------------------
## 9. LEGACY POLICY AND FALLBACK PLAN
---------------------------------------------------------------------

**Legacy HTTP + Python bridge:** frozen (security/crash fixes only), kept as the path for users without Pipeline. Removal only in a major release, after Pipeline leaves experimental and one stable Nexus release has shipped with Pipeline-first `auto`. Everything new is written once in Core and reaches legacy through the existing projection for free.

**Verify-lite (Gate A2 or Gate B fails, Live truth still healthy):** keep V-05 assertions inside `nexus_snapshot`; publish Overlay capture as a `[CliCommand]` other tools can call; ship skill recipes that pair Unity/competitor input with Nexus assertions and captures.

**Fallback destinations if Gate A fails twice (pick by evidence, not by default):**
1. **Contribute (A1):** publish Capture V2 as a small standalone package (`[CliCommand]` + library) and upstream it / readiness epochs to whichever maintainer answered P0-12.
2. **Skills product (A2):** publish the skill pack as verification and debugging recipes on top of `unity command` / `unity eval` + the few Nexus commands.
3. **Editor-tool automation (A3):** if the replays show agents mostly need to run existing editor tooling, narrow to `nexus_run` + `nexus_compile` for tools programmers.
4. **Token-lean proxy / Nexus-primary (A4):** v2.6 — decided by the §18 → P0-17 → P0-16c → Gate F pipeline (outcomes A/C/F/M/E/I). Raw Unity reads are unbounded (F22) and discovery over 154 commands is costly in Codex code mode (F24), but whole-task economics are pilot-only. A4 becomes the leading fallback only if Gate F selects C, F or M on D1 while Gate A fails; if E wins, A4 is a skill/allowlist recipe set, not a product.
5. Put the rest in maintenance mode with a clear README note. This is a legitimate outcome, not a failure.

---------------------------------------------------------------------
## 10. DECISIONS FOR THE OWNERS (recommendation in bold)
---------------------------------------------------------------------

| # | Decision | When | Recommendation |
|---|---|---|---|
| G1 | Pipeline-first for new features (as projection); legacy frozen | now | **Yes** |
| G2 | Python bridge: freeze + thin pass-through, no rewrite | now | **Yes** |
| G3 | Own `execute_code` or an `eval` forwarder | now | **No — use `unity eval`; document it in skills and COMPARISON** |
| G4 | Flagship = "Live truth" (`nexus_snapshot`, `nexus_compile`, `nexus_look`, `nexus_run`); `nexus_verify` conditional on Gate A2 and Gate B | now | **Yes (changed in v2.1)** |
| G5 | Tool surface: ≤8 default, startup profiles, skills, capabilities map | now | **Yes (reverses v1 T10)** |
| G6 | Timebox: B's 6-week hard bet vs D's 12 weeks | now | **12-week plan with pre-registered Gate A (wk 4) and Gate B (wk 9) as hard stops** |
| G7 | Player-build verify in these 12 weeks | week 10 | **Spike only, and only if Gate B passed** |
| G8 | Monetization | week 12 | **None yet; keep core MIT. "Nexus CI" is no longer the default (AltTester/GameDriver own it). Candidates: studio policy/audit layer over Pipeline incl. `eval` (only if P0-10 finds a pre-dispatch hook; validate with 5 studio conversations) and consulting** |
| G9 | Upstream outreach in week 1, in parallel (P0-12) | now | **Yes** |
| G10 | Target user = Unity programmer on an existing production project (§2.1) | now | **Yes** |
| G11 | Public names `nexus_*` (underscores) until the P0-10 naming check passes | now | **Yes** |
| G12 | Unity floor after 6.0 support ends (2026-10-16, F16) | week 2 | **Keep 6000.0 installable; test and support 6000.3 LTS and 6000.6+; the harness moves off 6000.4** |
| G13 | Hooks as a first-class, opt-in entry point (S-09) | now | **Yes, opt-in** — changes Gate A's definition (reported separately) |
| G14 | Funding: GitHub Sponsors + apply to OpenAI's Codex open-source fund (Anthropic's OSS program needs ~5k stars) | week 2 | **Yes** |
| G15 | Agent-facing boundary: A (Unity visible + Nexus composites), C (selective façade), F (Nexus-primary), **M (mixed Nexus-primary — the owner's leading prior, not evidence, not approved)**, E (skills/allowlists), or I | ≈ week 10–12 (Gate F) | **Decide only via §18 → P0-17 (cheap falsification) → P0-16c (finalists) → Gate F. Nothing architecture-dependent is scheduled; architecture-independent work continues (§7 work classification). Pilot = INSUFFICIENT EVIDENCE; its S6-domain arm does not represent S6-P and is not evidence against Nexus-primary** |
| G16 | Backend transport to Unity (warm MCP / Pipeline HTTP / persistent shell / in-Editor) decided separately from the façade question | after P0-17e | **Yes — by H5 evidence; shell-first no longer assumed (F27)** |
| G17 | Remove unbounded legacy dumps (`unity_dump_scene_graph`, unlimited `read_logs`) from default agent surfaces (S-10) | now | **Yes — compatible with every architecture outcome** |

---------------------------------------------------------------------
## 11. SUCCESS METRICS (what "we're winning" means now)
---------------------------------------------------------------------

| Metric | Baseline (v1 evidence) | Target by week 12 |
|---|---|---|
| Agent chooses Nexus for the Unity-state step (neutral, pre-registered prompts) | ~0 of 4 sessions (hypothesis, F6) | ≥60% of relevant runs, CI above baseline |
| User screenshot requests per session on the report-3 replay | user sent screenshots manually | 0 |
| Report-4 replay (run editor tool with args, confirm import) | failed via computer-use | completes, semantic_ok |
| Semantic success on "fix and prove UI bug" (if verify built) | not measurable (no fixture) | ≥ best competitor, false-success ≤ it |
| False-success rate (agent says done, check fails) | unmeasured | not higher than any measured setup |
| Tokens per successful task (report-3 replay) | est. 20–30k (Nexus today) | ≤7k (estimate to be replaced by P0-09) |
| Main-thread call p50, Edit Mode, unfocused, agent session active | 100–300 ms | <20 ms if P0-01a finds a safe lever |
| Default tools/list tokens | ~5.3k measured (14 managers; ~4.9k was chars/4, F23) | ≤2.5k (count_tokens API) |
| Architecture metrics (v2.6, primary): semantic success, false-success, total and uncached tokens per successful task, calls per successful task, invalid-argument/wrong-tool calls, discovery calls, escape-hatch usage, human intervention, capability coverage — paired, per finalist vs current | pilot only (`G_nexus_primary/PILOT_RESULTS.md`; non-robust) | Gate F D1–D10 on the finalists; raw compression ratio is never the headline |
| Theory register hygiene (v2.6) | 7 theories, 0 falsified | after P0-17: every in-scope theory is FALSIFIED, SURVIVED CHEAP TEST or INSUFFICIENT EVIDENCE; ≤3 challengers (plus S4 and C1) enter Stage B |
| Default-surface noise (v2.6) | 12 S4 tool outputs >40k chars from `unity_dump_scene_graph` (pilot) | 0 unbounded dumps on default surfaces after S-10 |
| Screenshot cost to the model | base64 text (unusable) | ≤0.8k tokens, real image (768 px default) |
| Platforms with a recorded benchmark run | macOS only | macOS + Windows (before Gate B) |
| External users who tried it on their own project / report use | 0 | ≥5 / ≥3 by Gate B |
| Install signals (OpenUPM, skills.sh, GitHub traffic) | unmeasured | reported at each gate |
| Relevant tasks solved with zero images sent (privacy, tokens) | unmeasured | reported; trend up |
| Opt-in "doctor" reports received from users (versions, errors; no project data) | none | ≥5 by Gate B |

---------------------------------------------------------------------
## 12. RISK REGISTER
---------------------------------------------------------------------

| # | Risk | Mitigation | Early-warning trigger |
|---|---|---|---|
| R1 | Agents still don't choose Nexus | Skills published by week 3, instructions, CLI exposure; pre-registered Gate A | Gate A numbers |
| R2 | Unity ships input/UI automation/Overlay capture | Keep value in live truth + assertions + report; primitives swappable via `IUnityPrimitives` — wrap, don't fight | Pipeline changelog mentions input/simulate/overlay |
| R3 | Verify niche already occupied (uLoopMCP, akiojin, Funplay, AltTester, Unity demo F11) | P0-10b head-to-head; Gate A2 desk decision; Gate B vs best competitor; verify-lite fallback | ≥2 competitors detect the planted bug |
| R4 | Pipeline API churn | Pinned range (P0-06), N/N-1 CI (A-01), hand-written thin projection + drift test | Compile break on Pipeline bump |
| R5 | `unity eval` bypasses Nexus gates; `confirm:true` mistaken for a human gate | Honest SECURITY.md; stop selling security as headline; no write params projected to Pipeline | Any doc claiming full protection in pipeline mode |
| R6 | 3 transports, 2 people | Freeze list; new code only in Core | >20% of monthly commits touch frozen areas |
| R7 | Windows breaks capture/input (unconditional flip, F10; Python missing for shim) | L-05 before Gate B and before any public claim | No Windows run by week 6 |
| R8 | Private `m_RenderTexture` disappears | W-03 self-test + fallback | Field resolves null on a new 6000.x |
| R9 | AI agents widen scope / self-accept | Rules 2–5 in §6 | PR over cap; "ACCEPTED" written by implementer |
| R10 | Energy / focus | Gates and public releases (wk 2, 6, 9, 12) give visible checkpoints | Two weeks without user-visible change |
| R11 | Domain reload kills long operations (play entry, compile) — shrinking risk on 6.6+/CoreCLR (F16), still real on older projects | Principle 3; L-02 resumable job store; V-02b; scope set by P0-13 | Any verify/run ending `interrupted` on default settings |
| R12 | Gates self-graded or underpowered | Pre-registration, frozen external prompts, N=8–10, Wilson intervals, third-party tasks, external users | Gate criteria edited after runs |
| R13 | Single harness contention invalidates evidence | P0-08c lock + second Editor; rule §6.14 | Evidence runs overlapping in logs |
| R14 | Dotted tool names rejected/mangled by clients | Underscore public names; P0-10 naming check | Any client error on `nexus.` names |
| R15 | Prompt injection / secret leakage via scenario files, logs, context packs | Rule §6.8 (non-imported closed-step scenarios, deny-list, opaque handles); Principle 9 | Scenario or log text changing agent actions in tests |
| R16 | Wake-up has no safe public lever | P0-01a spike first; release not blocked; metrics conditional | Spike finds only machine-wide pref changes |
| R17 | ToS/legal perception (F18) or studio NDA blocks use (screenshots to cloud models) | README/SECURITY note; never call Unity online services; `images: off` + redaction; PRIVACY.md | ToS amendment; tester reports NDA block |
| R18 | Bus factor: 2 authors, 6 stars, 71 open issues | Partner reviews ≥30% of PRs; triage issues before new features; funding (G14) | Partner reviews <30% of PRs in a month |
| R19 | CoreCLR / Unity 7 breaks Nexus (reflection on private `GameView` fields, statics, `AppNapBypass` P/Invoke) | P0-13 matrix; CI job on the newest beta once available | Any failure on the 6.8/7 matrix |
| R20 | Gate B (week 9 ≈ early December) collides with the Unity 7 beta and holidays | Recruit external users by week 8; freeze scope in week 11 | <3 recruited users by week 8 |
| R21 | Competition beyond Unity: Unreal 5.8 ships an official MCP plugin; Godot MCPs; Aura (Coplay's owner) sells a cross-engine assistant | Stay Unity-deep (live truth, Overlay capture, verify); revisit cross-engine only after Gate B | A tester asks for Unreal/Godot parity |
| R22 | Façade turns into duplication by another name (hand-written schemas over ~154 moving commands; manager schemas bloat, as 2.7k→5.3k did from v1.0 to v1.6) | Read-only scope; ≤10 hand normalisers with N/N-1 contract tests (F5); each schema ≤600 tokens; retire reads when Unity ships bounds (F-03) | >10 hand-written normalisers, any schema >600 tokens, or a normaliser broken by a Pipeline minor |
| R23 | Normalised output hides the fact the task needs → token win, success loss | Sufficiency questions frozen before normalisers (P0-16b); D1 non-inferiority; handles for the rest; tokens alone never pass Gate F | Pooled D1 lower bound below −10 pp (per-task gaps are reported, not a veto) |
| R24 | Nexus-primary cannot actually hide Unity: users install Unity's official skill/plugin and `unity` CLI; agents bypass the façade | S6-bypass cell runs in Stage B whenever an F/M challenger proceeds (P0-17f); C1 control (Unity's own surface curated) | Bypass >25% of S6-bypass runs |
| R25 | An S6 advantage is really `eval`-mediated or capability-surface reduction, not Nexus | Production-policy scoring (eval through Nexus counts as failure); C1 attribution rule | >25% of S6 task calls eval-like, or C1 ≈ S6 |
| R26 | Nexus as confused deputy: an S6 layer holds the single all-command Pipeline token (no scopes, no pre-dispatch hook, F26) | P0-16d; read-only first; separate destructive tool with per-call human prompt; fix Nexus token file mode (#0096) | Any S6 design that forwards writes before P0-16d conditions are met |
| R27 | Theory accumulation: research keeps adding hypotheses and never kills any (v2.6) | §18 register with pre-registered pass/fail/inconclusive rules; P0-17f must assign a state to every in-scope theory; ≤3 finalists | A Stage A ends with no theory FALSIFIED or SURVIVED |
| R28 | Cheap tests kill a good theory or keep a bad one (N≈6 per task cell) | Stage A rules calibrated by offline simulation (≈6–10% false calls; ≈85–90% power on large effects; PLAN_REVIEW_v2.6_B); kills need drop-one-task robustness; Stage A data never pooled into Stage B | A FALSIFIED label that flips under drop-one-task |
| R29 | Architecture-dependent work starts early because Nexus-primary "sounds right" | §7 work classification; G15; façade backlog requires Gate F + a separate design decision | Any PR implementing façade/delegation/generated managers before Gate F |
| R30 | Gate F slips past the 12-week horizon | It never blocks Gate A/B or Phase 2; architecture-independent work proceeds; I is an acceptable outcome | P0-17 not started by week 6 |

---------------------------------------------------------------------
## 13. WHERE THE ANALYSES DISAGREED (and how v2.1 resolves it)
---------------------------------------------------------------------

- **Timebox.** B: hard 6-week bet with exit. D: 12 weeks. → 12 weeks with hard, pre-registered Gate A (wk 4) and Gate B (wk 9; moved from wk 7 because the verify critical path is ~15 agent-days and Live truth now comes first).
- **Is input a Unity "hand"?** Lead reviewer assumed possibly; A found nothing official. Challenger 1 showed uLoopMCP already ships input and UI clicks. → Input is not a Nexus differentiator; built only if Gate A2 shows verify has room, Input System only.
- **Flagship.** v2.0: `nexus.verify`. Challenger 2: the evidence (reports 3–4, socials) asked for live Editor truth and editor-tool runs, and verify is crowded. → v2.1: Live truth first (weeks 4–6); verify conditional (Gate A2, Gate B vs best competitor). Rejected from the challenge: intercepting native dialogs (not feasible without patching; detection only) and a week-3 demo (nothing to show yet; demo at week 6).
- **Verify shape.** A: declarative scenario run Editor-side in one call. D: composite command with steps. Challenger 1: must be resumable across the play-entry reload. → A's DSL (closed step set) executed Editor-side on D's job model with a resumable runner (V-02b).
- **Pipeline as primitive provider.** v2.0: two adapters (legacy + in-process Pipeline). Challenger 1: in-process Pipeline means reflection into an experimental package; public Editor APIs suffice. → one `EditorUnityPrimitives`; Pipeline is projection only.
- **Profiler.** B: drop (Unity has it). A: marker-level triage could differentiate. → Backlog, decided by P0-10.
- **Multi-editor / runtime selector.** B: freeze and consider dropping `auto` default. D: keep semantics. → Keep; fix D-8 with port discovery in S-03.
- **Default surface size.** C: ≤8; D: 5; A: ~12 + escape hatch. Challenger 2: "only 14 of 120 visible" must not get worse. → ≤8 in `core` + `nexus_status detail:"capabilities"` + profiles + `nexus_call/list`.
- **execute_code.** Challenger 2: ship a thin `eval` forwarder. → Rejected (same Unity floor as Pipeline, adds surface, blurs the R5 boundary); documented instead.

---------------------------------------------------------------------
## 14. SOURCES
---------------------------------------------------------------------

- Unity CLI release notes — https://docs.unity.com/en-us/unity-cli/release-notes
- Unity CLI as the replacement for the in-Editor MCP server — https://docs.unity.com/en-us/unity-cli/replace-mcp-server-unity-cli
- Unity-Technologies/skills, `unity-cli` skill — https://github.com/Unity-Technologies/skills/blob/main/skills/unity-cli/SKILL.md
- Pipeline command list (community) — https://github.com/menstood/unity-pipeline-commands-skill/blob/main/SKILL.md
- Unity CLI beta.4 (`--detach`, `unity job`; only source) — https://discussions.unity.com/t/unity-cli-1-0-0-beta-4-is-rolling-out/1733720
- Unity CLI launch coverage (agent play-tests and catches a bug) — https://gamedev.net/news/5423/ · https://runtimewire.com/article/unity-ships-a-cli-that-lets-ai-agents-operate-running-game-projects · https://unity.com/resources/unity-pipeline-cli-technical-walkthrough
- uLoopMCP / unity-cli-loop (v3.0.1: simulate-keyboard/mouse/ui, replay-input, screenshot; own IPC) — https://github.com/hatayama/unity-cli-loop · https://www.skills.sh/hatayama/unity-cli-loop/uloop-record-input
- akiojin/unity-cli (playmode-testing, input-system, ui-automation skills) — https://github.com/akiojin/unity-cli · https://www.skills.sh/akiojin/unity-cli/unity-playmode-testing
- Funplay Unity MCP (core profile) — https://glama.ai/mcp/servers/FunplayAI/funplay-unity-mcp
- AltTester CLI / AI skills — https://alttester.com/alttester-cli-built-for-the-way-ai-assisted-testing-actually-works/ · GameDriver — https://www2.gamedriver.io/blog/gamedriver-test-assistant
- "Adopt the Brain, Defer the Bridge" — https://dev.to/furic/adopt-the-brain-defer-the-bridge-unitys-agent-plugin-and-free-cli-three-weeks-on-1kei
- Claude vision token cost — https://platform.claude.com/docs/en/build-with-claude/vision
- Claude tool-name constraints — Anthropic tool-use docs on platform.claude.com (tool `name` pattern `^[a-zA-Z0-9_-]{1,64}$`)
- Cursor ignoring `tools/list_changed` mid-session (reported by Challenger 1, not independently verified) — forum.cursor.com thread 161459
- Adversarial reviews: `v2_analysis/CH1_technical.md`, `v2_analysis/CH2_strategy.md`; judge log `v2_analysis/JUDGE_log.md`
- Unity research (v2.2): `v2_analysis/U1_unity_shipped.md`, `v2_analysis/U2_unity_roadmap.md`; Pipeline 0.7.0-exp.1 package copy (third party) — https://github.com/bigkaka111-oss/ReactorCrystal/tree/main/Packages/com.unity.pipeline ; Unity-Technologies/skills and unity-agent-plugin (GitHub); Coplay news — https://coplay.dev/blog/whats-next-for-coplay

---------------------------------------------------------------------
## 14b. UNITY ROADMAP ALIGNMENT (v2.2)
---------------------------------------------------------------------

Sources: `v2_analysis/U1_unity_shipped.md` (what Unity ships now) and `v2_analysis/U2_unity_roadmap.md`
(what it is likely to ship next). Evidence levels: **CONFIRMED** (shipped / officially stated),
**SIGNAL** (staff draft PR or staff forum reply), **SPEC** (inference). Unity's docs sites were
reachable only through search snippets; treat every row as input to P0-10, not as settled.

### 14b.1 How Unity is shipping (pattern)
- Unity ships **small primitives + CI tooling** (commands, jobs, tests, builds, runtime connection) and
  leaves multi-step workflows to **skills written by its product teams**. No composed, token-budgeted
  verify/diagnose/context layer has appeared in anything found (SPEC: unlikely within 12 months;
  Unity's priority is breadth and GA before Unity 7, whose early beta is expected Dec 2026 and GA Q1 2027).
- Pipeline breaks its API roughly every minor (0.6→0.7→0.8 within weeks). **Capability probing beats
  version pinning** long-term; pinning (P0-06) is the short-term guard.
- Timelines: CLI 1.0.0-beta.11 (2026-09-22), Pipeline 0.8.0-exp.1 (~09-28). No public GA date
  (SPEC: CLI GA near the Unity 7 window).

### 14b.2 Capability map: Unity status → Nexus decision
| Area | Unity now | Unity next (6 / 12 mo) | Nexus decision |
|---|---|---|---|
| GameObject/asset/scene CRUD | CONFIRMED ~160 built-ins (154 measured on 0.7, F20), transactional `batch` with single Undo + `dry_run` | — | **AVOID implementing.** Frozen legacy only; backlog `nexus_mutate` dropped (use `batch`). Under the current production policy writes stay Unity-direct; whether official write primitives can be delegated is the separate research question P0-16d |
| Unbounded reads (hierarchy, console, find/search) | CONFIRMED, no depth/limit/grouping on hierarchy; console tail without grouping (F22); Unity already offers some economy options (`batch.result_fields`, `get_serialized_fields format=value`, `search limit`) | — | **AVOID implementing; normalise.** Delegated read façade (S5) or Nexus-primary (S6) decided by Gate F (P0-16); otherwise normalisation lives inside `nexus_snapshot`/`nexus_diagnose` |
| Agent-facing surface (whole catalog) | 154 commands, typed inputs, tags, no output schemas (F25); `unity mcp` exposes all as tools; shell CLI unusable from Codex's default sandbox (F27) | catalog churns every minor | **UNDECIDED (v2.6):** A/C/F/M/E/I via §18 → P0-17 → Gate F; the leading hypothesis is M (mixed Nexus-primary); nothing scheduled |
| C# execution | CONFIRMED `eval`, `eval_file`, `run_script` | — | **AVOID** (already G3) |
| Compile diagnostics | CONFIRMED `unity recompile` lists errors with file/line (beta.11); `console_status` groundTruth | grouping/hints: 12 mo medium | **WRAP.** `nexus_compile` calls `unity recompile` when present; Nexus adds grouping, "did you mean", ±3 lines context, and **Safe-Mode-resilient diagnosis** (reads `Editor.log`/asmdefs when Pipeline cannot load — a gap Unity's own skill documents) |
| Tests | CONFIRMED `run_tests`, `unity test --affected`, `watch test`, JUnit | — | **AVOID** running tests; summarise failures only inside `nexus_diagnose` |
| Build / player / hot reload / runtime connection | CONFIRMED, fastest-moving area | 6 mo high | **AVOID own transport; INTEGRATE.** Backlog `player_check` = same scenario over `unity command --runtime` |
| Jobs | CONFIRMED `--detach`, `unity job`, `/api/job` (jobs do not survive reload) | — | **USE** Unity jobs in pipeline mode; Nexus L-02 job store only for reload-crossing Nexus workflows and legacy mode |
| Editor wake-up / throttling | CONFIRMED `set_autotick` | — | **USE** in pipeline mode; P0-01a/b only for legacy mode |
| Input simulation | Reported: `simulate_key`/`simulate_pointer`, dev Player runtime, Input System, press/click only (F4) | SIGNAL: staff draft "polyspatial-playtest" skill + sub-agent (record → drive → assert on recorded state), XR-scoped; generic input 6 mo ~40%, 12 mo ~60% | **WRAP, design for replacement.** V-03 behind `IUnityPrimitives`; build only hold/text/Editor-Play-Mode gaps that P0-10 confirms. Not a differentiator |
| UI automation (UGUI / UITK) | none (coordinates only; `capture_*_element` in 6000.7+) | 6 mo ~20%, 12 mo ~35% | **BUILD** — main differentiator: UI-aware targeting by name/path, visibility/text/clickability, `NO_EVENTSYSTEM` errors (V-04) |
| Game View / Overlay capture | CONFIRMED `capture_game_view` camera/screen; screen includes Overlay **in Play Mode only**; PNG, `max_resolution`, path-only; desktop fallback in beta.10 | JPEG/crop/overlay fixes: 6 mo ~45%, 12 mo ~70% | **Keep Capture V2 only while it measurably wins** (Edit-Mode Overlay, JPEG, stall). W-03 falls back to Unity `source=screen`. Invest in crop-to-element, numbered marks, token budget — **not** new raw capture |
| Assertions / visual verification | `wait_for` member conditions (equals/greaterThan/contains/changed) + capture on met | pixel asserts 12 mo ~25%; state-recording asserts 12 mo ~40% | **BUILD** state-first asserts (property/log/UI), pixel region second; **WRAP** `wait_for` for property waits (V-05) |
| Scenario runner (verify) | none as a composite | 6 mo ~15%, 12 mo ~30% | **BUILD if Gate A2 allows**; emit **NUnit/JUnit** and offer "export scenario as PlayMode test" so results plug into `unity test` and CI |
| Profiler / perf | `get_performance_stats` snapshot | SIGNAL: staff reply "profiler and frame debugger before GA" (search summary only); 6 mo ~55%, 12 mo ~75% | **AVOID raw profiler capture.** Only later: a perf-budget assert inside verify via `ProfilerRecorder` |
| Context / references | `search` (Unity Search, ≤200 results); `unity vcs affected` GUID impact graph | reverse-reference packs 12 mo ~60% | **BUILD `nexus_context`** (token budget, attached-where, serialized refs); `find_references` only if it beats `search`/`vcs affected` in P0-10 — otherwise **consume** `vcs affected --json` |
| Readiness | settling/503, compilationFailed, groundTruth | — | **WRAP + BUILD** epochs (P0-02); truthful readiness remains a Nexus quality point |
| Skills / plugin | CONFIRMED 33-skill plugin, `unity-cli` + `unity-pipeline` skills, `skill install --local`; public skills repo no longer takes PRs; no QA/playtest skill shipped | QA skills: SIGNAL (playtest draft), 6 mo ~70% | **INTEGRATE.** Nexus skill composes with `unity-cli`, never copies domain skills; same plugin format; ship skill inside the package |
| MCP client setup | CONFIRMED `unity mcp configure` (~16 clients, `--server` family) | — | **AVOID** extending Nexus installers (already frozen) |
| Security / policy | bearer token, Origin refusal, loopback-only (0.8), no per-command policy | per-command policy 12 mo ~60% | **ALIGN, don't compete:** express Nexus gates in Unity's vocabulary (`SECURITY.md`, `RuntimeOnly`, dev-only); G8 policy-layer idea stays conditional |

### 14b.3 Integration hooks to prepare now (added to tasks)
1. **Attributes assembly (P0-06):** reference `Unity.Pipeline.Attributes` (0.8) instead of the full package where possible; consider public `CommandRegistry.RegisterCommand` for state-dependent commands.
2. **`RuntimeOnly` steps (backlog `player_check`):** design verify step executors so they can be registered as runtime commands for dev Players.
3. **Jobs:** map the Nexus job store onto Unity job ids in pipeline mode (V-07).
4. **Identify Nexus to Unity:** pass `--caller nexus --skill <name>` on every CLI call Nexus or its skills make (documented integration flag; costs nothing). Add to S-01 skill recipes.
5. **Capability probing (new rule for L-01/V-07):** at startup read `unity command` listing (`--format json` / tags) and route to Unity's `simulate_*`, `profiler`, `ui_*`, `capture_*` as soon as they exist; the N/N-1 CI job (A-01) keeps a parity table.
6. **Consume, don't rebuild:** `unity vcs affected --json` for impact/reference questions; `unity test --affected` for regression selection inside verify; `unity recompile` for compile truth.
7. **Monthly watch list (owner task, 30 min):** Pipeline and CLI changelogs; Unity-Technologies/skills and unity-agent-plugin PRs (esp. the playtest skill); Unity Discussions CLI thread (profiler/GA). Any hit on input/UI automation/overlay/asserts triggers a re-check of Gate A2/B assumptions (R2).

### 14b.4 Ecosystem notes (reported, not independently verified)
- akiojin/unity-cli is converging on Unity's capture primitives; uLoopMCP's input/replay/record set is the most likely bundle for Unity to absorb.
- CoplayDev (largest open-source Unity MCP) was reported acquired by Ramen/Aura and tied to a commercial assistant (coplay.dev blog) — reduces open-source competition but raises commercial-assistant competition.

### 14b.5 Net effect on this plan
- **Confirms the direction:** live truth + UI-aware targeting + assertions + token-lean context + truthful readiness are the lowest-overlap areas for 6–12 months.
- **Downgrades:** input simulation (wrap, not build), raw capture features, profiler, reference index (must beat `search`/`vcs affected`), `nexus_mutate` (dropped).
- **Upgrades:** P0-06 to urgent (F14); Safe-Mode-resilient diagnosis added to `nexus_diagnose`; JUnit output for verify.

---------------------------------------------------------------------
## 15. ADVERSARIAL REVIEW LOG (v2.0 → v2.1)
---------------------------------------------------------------------

Totals: 52 objections (CH1 technical: 32, CH2 strategy: 20). **ACCEPT 37 · PARTIAL 13 · REJECT 2.**

| id | objection (short) | verdict | reason | change applied |
|---|---|---|---|---|
| CH1-1 | F1–F3 partly unverifiable; `--detach`/`unity job` only in a forum post | PARTIAL | docs.unity.com is not fetchable here; SKILL.md confirms `[CliCommand]`/`MainThreadRequired`/eval but not `--detach` | F1/F3 marked "via search"; `--detach` a hypothesis checked in P0-10; V-07 conditional on it |
| CH1-2 | F5 understated: uLoopMCP already covers most of V-03/V-04 | ACCEPT | github.com/hatayama/unity-cli-loop v3.0.1 lists simulate-keyboard/mouse/ui, replay-input, screenshot over own IPC | F5 rewritten (+akiojin, Funplay); R3; input no longer a differentiator |
| CH1-3 | F6 is a hypothesis (retrospectives, N=4; install unconfirmed) | ACCEPT | `3.md:31` "cannot be confirmed"; report 4 used `mcp__cua_repl` | F6 relabelled hypothesis; §0 and §11 wording |
| CH1-4 | F8 missing half: Pipeline has the same 3×100 ms ticks | ACCEPT | `STABILIZATION_ACCEPTANCE_REPORT.md` §E table: Pipeline and legacy both 3 ticks / ~297 ms | F8 amended; R16 |
| CH1-5 | F9: shipped Pipeline names use underscores | ACCEPT | `Alias = "nexus_*"` in `Editor/Commands/*Command.cs:18-23`; `NexusPipelineCommands.cs:21,31,43` | F9; all public names → `nexus_*` |
| CH1-6 | F10: unconditional flip Blit → D3D likely upside down | ACCEPT | `GameViewCaptureSource.cs:52-55` | F10; L-05 note; R7 |
| CH1-7 | D-1 overstated (token file readable by same user) | ACCEPT | `MCPServer.Identity.cs:122`; Origin/Host checks block browsers | D-1 marked minor; fix kept in P0-05 |
| CH1-8 | D-2 worse: disposed event; running calls still apply | ACCEPT | `MCPServerMethods.cs:215-224`: `signal.Set()` after `using` disposal | D-2 rewritten; P0-05 adds disposal fix + E for running mutation |
| CH1-9 | D-3: cached flags stale while throttled; two readiness predicates | ACCEPT | `Sync.cs:76` vs `Status.cs:22` | D-3 amended; P0-02 "one predicate" |
| CH1-10 | D-8 imprecise; fix is port discovery | ACCEPT | `MCPServer.Networking.cs:105-113` skips bind only for foreign-owned port; selector comment "bound opportunistically" | D-8 rewritten; S-03 port discovery + 2-project test |
| CH1-11 | D-10 partly wrong (line/col are in message strings) | ACCEPT | `GroupCompileErrorsCommand.cs:61-80`: 50-entry ring buffer, substring key | D-10 rewritten |
| CH1-12 | Domain reload is the normal path for verify/compile | ACCEPT | Default Enter Play Mode reloads the domain; v1 C7 listener died on reload (`BENCH_RESULTS.md:117`) | Principle 3 rewritten; job model; L-02 resumable store; new V-02b; R11 |
| CH1-13 | P0-01 is a research spike; E criteria conflict; don't block release | ACCEPT | No public thread-safe wake API; Pipeline shows same cadence (F8) | P0-01 split into P0-01a spike + P0-01b; E scoped to active session; removed from P0-11 deps; §11 conditional |
| CH1-14 | `unity command nexus.*` naming unverified | ACCEPT | F9 + LLM tool-name charset (F13) | P0-10 naming check; G11; rule §6.10; R14 |
| CH1-15 | P0-04 maps `Submitted` to error → retries/duplicates | ACCEPT | `routes_editor.py:54` treats Submitted as legitimate test trigger | P0-04: Submitted → pending, `isError:false`; Principle 5 |
| CH1-16 | Cancel flag doesn't stop double-apply; need idempotency | ACCEPT | Follows from D-2 | `request_id` in §4, P0-04, P0-05, S-04 |
| CH1-17 | Legacy Input Manager fallback not implementable; focus issues | ACCEPT | `UnityEngine.Input` has no injection API; Input System background behaviour is a ProjectSettings concern | V-03 Input System only + `LEGACY_INPUT_UNSUPPORTED`; P0-10 input focus check |
| CH1-18 | V-04/V-06 are 2–3 tasks each; Gate B wk 7 unrealistic | ACCEPT | Critical path ≈15 agent-days | V-04a/b, V-06a/b; Gate B on V-06a at week 9 |
| CH1-19 | Two primitive adapters contradictory; in-process Pipeline = private API | ACCEPT | Public Editor APIs cover play/compile/refresh/tests/console | One `EditorUnityPrimitives`; Pipeline projection only; V-07 reduced to 1d conditional |
| CH1-20 | Python generator cannot reflect C# | ACCEPT | Batchmode generation needs Unity in CI | S-02: hand-written wrappers + EditMode drift test (2d) |
| CH1-21 | `tools/list_changed` unreliable; deferral makes token budget moot | PARTIAL | Startup profiles are safer (accepted); Cursor thread not independently verified, and not every client defers schemas, so the ≤2.5k budget stays | S-03: startup profiles, best-effort `list_changed`, per-client matrix E |
| CH1-22 | Gates underpowered, contaminated, unfair; no agent-run harness | ACCEPT | ≈18 runs/arm → ±23 pp CI; ratio vs ~0 baseline meaningless | Pre-registration, ≥60% absolute + Wilson bounds, N=8–10, S3 pinned to 1.6.0, S1 equivalent snippet, frozen Gate B tasks incl. uLoop-native task; new P0-08b harness; P0-09 deps |
| CH1-23 | Single harness Editor vs 4 PRs in flight | PARTIAL | Contention is real; a full farm is over-scoped for 2 reviewers | P0-08c: lock + 2nd fixture Editor; rule §6.14; R13 |
| CH1-24 | Security regressions from composites (7 points) | ACCEPT | `confirm:true` is agent-set (`Utils.cs:287-291`); scenario/handle/context/ProjectSettings risks are real | D-11; rule §6.8 extended; §4 opaque handles, no write params to Pipeline; `NexusScenarios~/`; V-08 restore+label; Principle 9; R15 |
| CH1-25 | Image cost target wrong for 1024×1024 | ACCEPT | w×h/750 (F12) | Default long side 768 px; S-07 E for 16:9 and 1:1; §11 |
| CH1-26 | Epochs must persist in SessionState; wait can't span reload | ACCEPT | Statics reset on reload | P0-02 text |
| CH1-27 | Out-of-range Pipeline can't be faked in EditMode | ACCEPT | Package version is resolved by UPM | P0-06 E uses a second scratch project + prerelease semantics test |
| CH1-28 | `nexus` shim needs Python (Windows) | PARTIAL | True, but shim is already optional | Principle 6: `unity command` primary, shim optional; L-05 checks Python |
| CH1-29 | S-05 `no_change` <50 ms impossible before wake-up | ACCEPT | Needs a main-thread tick | S-05 E conditional on P0-01b |
| CH1-30 | Reverse index fails on Binary serialization | ACCEPT | YAML parsing requires Force Text | C-02 scoped to Force Text + error code |
| CH1-31 | `nexus_context` is a retrieval research problem | ACCEPT | Ranking from a task string is unproven | C-01 labelled spike; stays out of `core` unless ≥30% |
| CH1-32 | Verify DSL schema may eat the budget | ACCEPT | Unmeasured | §5.1 "measure before promising"; V-06a E measures schema |
| CH2-1 | Flagship is not what evidence asked; "Live truth" wedge first | PARTIAL | Reports 3 (`3.md:130,208-231`), 4 (`4.md:48-65`) and socials support live truth + editor-tool runs; verify kept but conditional. Native dialog interception rejected (not possible without patching) | New Phase 2 Live truth (L-03 `nexus_snapshot`, L-04 `nexus_run` with modal detection); verify → Phase 3 conditional; G4; §0, §2, §5.1 |
| CH2-2 | Drop "hands vs eyes" metaphor | ACCEPT | Unity ships screenshot/hierarchy/component reads (F3); F11 | §2 concrete claim |
| CH2-3 | Alternatives A1–A6; outreach week 1; named gate-failure destinations | PARTIAL | Outreach is cheap and parallel; A1 as main strategy not supported by evidence | P0-12, G9; §9 destinations A1–A4 |
| CH2-4 | No target persona | ACCEPT | Plan never named a user | §2.1, G10; P0-09 tasks from real sessions + third-party project |
| CH2-5 | Add `nexus.capabilities` | PARTIAL | Need is real ("14 of 120"); a separate tool costs a core slot | `nexus_status detail:"capabilities"`; profiles in `instructions` |
| CH2-6 | Ship a thin `nexus.eval` forwarder | REJECT | Nexus floor (6000.0) = Pipeline floor, so any user can add Pipeline; a forwarder adds surface and blurs the R5 boundary | Only documentation (§5.1, G3, R-03) |
| CH2-7 | Scene View capture in `nexus_look` | ACCEPT | Friend's gap list; cheap on Capture V2 | S-07 (3d) Scene View framed on object |
| CH2-8 | Hot-reload awareness in `nexus_compile` | PARTIAL | Verdict can mislead in hot-reload setups; full integration out of scope | S-05: detect + warn only |
| CH2-9 | Verify niche occupied; widen competitor spike; ≥2 detect → not flagship | ACCEPT | Verified uLoop v3.0.1 and akiojin skills on GitHub; F11 Unity demo | P0-10b; Gate A2; R3; verify-lite in §9 |
| CH2-10 | Gate B compares against a strawman | ACCEPT | Official skills lack input (F4) | Gate B arm = best competitor from P0-10b |
| CH2-11 | Gate A cannot fail cleanly; external prompts/tasks/users | PARTIAL | Pre-registration, thresholds, frozen external prompts, third-party tasks accepted; ≥5 external users by week 4 is unrealistic (skill published week 3) | Gate A1 rewrite; external users reported at A, hard criterion at Gate B |
| CH2-12 | Gate B "OR" makes it advisory | ACCEPT | A token win alone must not pass a verify product | Gate B: AND + effect sizes, N≥10 |
| CH2-13 | Compress public loop; Windows before Gate B; drop C-03/C-05/A-03 | PARTIAL | Accepted: OpenUPM wk 2, skill listing wk 3, testers wk 3, Windows wk 5–6, drops to backlog. Rejected: live-truth demo at week 3 (not built until wk 6) | P0-11, S-01, S-08, L-05, L-07; backlog |
| CH2-14 | Keep `nexus doctor` | ACCEPT | First-run friction (C §1.2) | S-01 + `nexus_status detail:"doctor"` |
| CH2-15 | Name "Nexus" is unsearchable | PARTIAL | Brand kept (package id, history); discoverability fixed at skill level | §2 descriptive skill id; S-01 search test |
| CH2-16 | COMPARISON must include Ivan, Coplay, Funplay, akiojin | ACCEPT | These were the questions asked (Socials: James, Abdullah) | R-03 six competitors |
| CH2-17 | No usage measurement | PARTIAL | Public counters accepted; in-product stats export deferred (scope, privacy) | S-08, §11; export in backlog |
| CH2-18 | Monetization: studio policy layer, not "Nexus CI" | PARTIAL | "Nexus CI" crowded (AltTester/GameDriver); policy layer depends on a Pipeline hook nobody has confirmed | G8 rewritten; P0-10 checks hook; backlog item |
| CH2-19 | Demo should replay real failures | ACCEPT | Reports 3–4 are real and more credible than a planted toggle | §2 headline; L-07, R-04 |
| CH2-20 | Funplay already did a small core profile (plan presents it as novel) | REJECT | The plan makes no novelty claim for profiles; they are chosen for tokens, not differentiation | none |

---------------------------------------------------------------------
## 16. BLIND-SPOT REVIEW (v2.2 → v2.3)
---------------------------------------------------------------------

Source: `v2_analysis/E_blind_spots.md` (independent blind-spot agent). Lead verification: F16 confirmed by
several independent sources (Unity Discussions CoreCLR guide, third-party issue trackers, press); F19
confirmed in the repo; F17/F18 reported by E and not re-verified by the lead (docs/legal sites blocked).

| # | Blind spot | Impact | Plan change |
|---|---|---|---|
| E1 | Unity removes classic domain reload (6.6 default, 6.8 only mode, Unity 7 CoreCLR); 6.0 support ends 2026-10-16 | High | F16; Principle 3 rewritten; P0-13 spike; V-08 → static-state check; G12; R19 |
| E2 | Agent hooks let Nexus run without the agent choosing it | High | S-09 hook pack; G13; Gate A reports hooks separately |
| E3 | MCP 2026-07-28 (`server/discover`, tasks extension, new input request) | High for P0-07 | F17; P0-07 dual-protocol; tasks/input request considered in L-02 and for a real human confirm |
| E4 | Cloud/headless agents cannot reach a local Editor | Med-High | P0-14 spike; Editor-less "file truth" CLI decided there |
| E5 | Unity ToS §17.2 agentic-access clause | Med-High | F18; rule §6.8 (no Unity online services); R17 |
| E6 | Privacy/NDA: screenshots and logs go to model providers | Med | `images` setting, redaction, PRIVACY.md (rule §6.8); zero-image metric |
| E7 | Package footprint: hard Input System dependency; Runtime asmdef in release builds | Med | F19; P0-15 |
| E8 | Benchmark as a public "Unity agent eval" | Med-High (opportunity) | R-03 extended |
| E9 | No usage signal; `--caller nexus` informs Unity, not Nexus | Med | Opt-in doctor reports metric |
| E10 | Bus factor, funding, community | Med | G14; R18 |

Internal inconsistencies fixed in v2.3: F3 vs §14b on `unity job` (F3 updated); `nexus_find_references`
in core made conditional on C-02; Phase 0 load note added (weeks 1–3, spikes non-blocking); December
collision recorded as R20. Still open: §14b.2 recommends `set_autotick` in pipeline mode while F8 says
Pipeline shows the same ~100 ms tick cadence — P0-01a must measure `set_autotick` directly before either
claim is used.

Opportunities to evaluate later (not scheduled): Unity AI Assistant tool registration (Nexus tools inside
Unity's own assistant), Tuanjie (China) compatibility check, Asset Store publisher persona (package
validation), MCP Apps / skills-over-MCP delivery.

Open questions for the owners (from E §6): which Unity versions testers run and whether they use Fast
Enter Play Mode; how much of your own agent work now runs in cloud agents; whether hooks are acceptable;
whether any tester is under an NDA forbidding cloud screenshots; whether you can receive Sponsors/grants;
whether Tuanjie is in scope; whether to publish the benchmark even if Nexus loses; whether to avoid the
December window for Gate B / 1.9.0.

---------------------------------------------------------------------
## 17. FAÇADE HYPOTHESIS REVIEW (v2.3 → v2.4)
---------------------------------------------------------------------

Question: should Nexus stop treating Unity CLI as a parallel agent-visible surface and instead use it as a
mostly hidden execution backend behind a smaller, stable, token-efficient Nexus API? Full analysis:
`v2_analysis/F_facade_hypothesis.md`; evidence (scripts, raw captures, client research with a URL per claim,
manager-tool history): `v2_analysis/F_facade_evidence/`. Raw hierarchy/console captures are truncated to
20 KB in the evidence folder; the measurement scripts and token tables are complete.

### 17.1 Findings and decision
- **Measured (F20, F22, F23):** `unity mcp` = 154 tools, one per command, ≈21–25k schema tokens; Unity read
  outputs are unbounded (hierarchy ≈86k tokens at ≈1.1k objects, no depth parameter; truncated at 25k by
  Claude Code); warm Unity call ≈100 ms, cold CLI ≈1.2–2.2 s, normalisation ≈1 ms; Nexus managers ≈5.3k and
  the manager format saves only ≈4% vs atomic tools.
- **Documented (F21):** Claude Code, Codex (tool-search models) and Cursor defer schemas; Gemini CLI and
  non-deferring setups do not. Unity recommends shell over MCP for shell-capable agents.
- **Decision (G15, v2.4 — superseded by §17.3):** option **D → (E, then C)**. No façade now; upstream request (P0-12);
  spike P0-16a–c; Gate F at week 5. v2.4 also said "full façade (B) rejected on current evidence" — **withdrawn in v2.5**:
  the evidence tested an adjacent design (S4 + selective reads), not Nexus-primary. Principle 1 now defines duplication by capability and separates
  duplicate implementation / delegated façade / composite. Gate A A1 is unchanged; façade arms are judged
  only by Gate F.
- **Still requires evidence:** tokens per successful task under each design; wrong-tool rates for managers
  vs named Unity commands; sufficiency of normalised output on real scenes; whether option E is enough;
  whether non-deferring clients matter to the target persona.

### 17.2 Independent review log (challenger agent; the author did not judge its own criteria after results — no results exist yet)

| id | objection (short) | sev | verdict | change applied |
|---|---|---|---|---|
| F-1 | §17 referenced but missing | HIGH | ACCEPT | this section |
| F-2 | 100× compared to a cost the agent never pays (25k MCP cap, filtered calls) | HIGH | ACCEPT | §0, F21, F22, §9 A4, analysis §1/§2.3 reworded; "100×" removed from §0 |
| F-3 | L1 façade contradicts Principle 6 (Python optional) and the §5.1 merge into Core composites | HIGH | ACCEPT | §4: Core `[CliCommand]` normalised reads are the default candidate, L1 adapter the alternative |
| F-4 | `nexus_call` "100% coverage" forwards writes/`eval`, breaks §6.8/G3 | HIGH | ACCEPT | read-only allowlist; coverage = reachable directly via Unity; F7 security criterion; Principle 1 |
| F-5 | Gate F unpassable at N=8–10 (best-of-two comparator, "every task", −10 pp needs ~100+ runs/arm); F3 comparator inconsistent | HIGH | ACCEPT | comparator S4 for all criteria; pooled non-inferiority −15 pp sized to ≈50 runs/arm; per-task rule only for ≥3/10 drops |
| F-6 | Editor time under-counted (+160–220 runs) | HIGH | ACCEPT | high-volume tasks Claude Code only; S5r one task; ≈150 runs, ≈25–60 Editor-hours costed; Gate F moved to week 5, non-blocking |
| F-7 | Tasks/normalisers contaminated (same author, synthetic regular scene) | MED | ACCEPT | task + sufficiency freeze by prereg author before normalisers; ≥1 high-volume task on a third-party scene |
| F-8 | S5 differs from S4 in more than one variable | MED | ACCEPT | S5 = exactly S4 + façade |
| F-9 | Deferral claims overstated (proxies, models; prefixed names ≈1.4k; 24.6k with default JSON) | MED | ACCEPT | F20/F21 caveats; §0; no-deferral cell in P0-16a/c |
| F-10 | Missing cheaper option E (upstream params + skill `eval` recipes); call-path loophole in the distinction | MED | ACCEPT | option E arm S1e, wins ties; P0-12 upstream request; duplication defined by capability + retirement rule |
| F-11 | Stability argument contradicts catalog pass-through; F5 denominator gameable | MED | ACCEPT | analysis argument downgraded; F5 = ≤10 hand normalisers with N/N-1 contract tests, denominator façade-exposed actions |
| F-12 | Buffer took V-08; outcome table incomplete; A4 bypassed F1; high-volume tasks not excluded from A1 | MED | ACCEPT | capacity from C-01/V-04b/V-07, V-08 kept; full outcome table; A4 requires F1; A1 exclusion stated |
| F-13 | G15/R22–R23 ordering; 913 vs 1,133 objects; n=5 latency | LOW | ACCEPT (n=5 kept, labelled) | renumbered; label fixed (≈1.1k); latency n stated in F_facade_hypothesis M8 |

### 17.3 v2.5 corrective pass — Nexus-primary vs selective façade (2026-10-03)

**What v2.4 got right (kept):** 154 tools / 21–25k schema tokens (F20); client deferral (F21, now corrected and deepened);
manager *syntax* ≈4% (F23); unbounded Unity reads (F22); cheap normalisation; evidence-first P0-16 + Gate F; the challenger review.

**What v2.4 misunderstood:** it tested and planned **S5 = S4 + selective normalised reads**, an adjacent design, and then wrote
"full façade rejected". The owner's hypothesis is **S6 = Nexus-primary / Unity-hidden-backend**: the agent sees a compact Nexus
surface only, Nexus delegates every primitive to the *official* Unity command and normalises output, and Unity's command surface is
not a competing decision surface. S5 keeps the Unity-vs-Nexus routing decision; S6 removes it. It also conflated *representation
compression* (managers vs atomic, ≈4%) with *capability-surface reduction* (154 commands vs a task-shaped subset), treated
deferred loading as if it ended the cost question (it moves cost into discovery: F24), made ≥20% mis-routing a hard prerequisite,
and let the read-only *production policy* silently define the *architecture* question.

**New evidence (all in `v2_analysis/G_nexus_primary/`):** A_tool_search (client retrieval mechanics; Codex code mode; offline
BM25 recall; F21/F24), B_catalog_adapter (catalog-driven generation, churn, maintenance; F25), D_security (Pipeline auth, no
pre-dispatch hook, delegated-write matrix; F26), E_skeptic / F_advocate (both directions, with predictions), C_design_review
(26 design objections, 6 blockers fixed before run 1), TASKS_v1 (blind task set), PREREG_PILOT (+A1–A5), PILOT_RESULTS.

**Pilot result:** INSUFFICIENT EVIDENCE for every comparison (Codex only, N=12/arm). Pre-registered labels (S6-domain vs S4
*against*, S6-domain vs S5 *against*, S5 vs S4 *toward*) are **not robust**: each flips to neutral under at least one
defensible variation (PILOT_RESULTS §6). Observed success: S4 9/12, S5 9/12, S6-domain 10/12; 4 of S6-domain's 6
production-policy losses are `eval` penalties, and the remaining gap sits on T3, which S4/S5 solved by shell grep. The mock S6 was
a flat parameter union with `eval` exposed and no composites — not the owner's S6. Robust descriptive findings: normalisation cuts
emitted tool output 20–100×; total tokens and wall time move far less; the flat union schema produced most S6 argument errors; the
current Nexus `unity_dump_scene_graph` is S4's largest output source; Unity CLI is unusable from Codex's sandbox (F27).

**Decision (v2.5):** no architecture change, nothing scheduled. The current design stays active. Nexus-primary (F) is an
**open, untested-at-scale candidate**, neither rejected nor favoured; selective normalisation (C) and skills (E) remain
candidates; mixed outcomes (e.g. Nexus-primary for reads/diagnosis, Unity-direct for writes/`eval`/build) are an explicit
possible result. The full P0-16 decides via the Gate F v2.5 matrix (D1–D10). The pre-registered pilot consequence ("S5 first") rested on labels that
proved non-robust, so — at the final reviewer's recommendation, disclosed here — S5 and a production-shaped S6 (per-action typed
schemas, composites, no `eval` exposed, plus an observed-with-`eval` variant) run in the same wave with the C1/S1e/S6-bypass controls.
**Superseded in v2.6 (§17.5, §18):** instead of one large wave, candidates first go through P0-17 cheap falsification; only 2–3
finalists reach P0-16c / Gate F.

**Still requires evidence:** Claude Code and Gemini behaviour; retrieval correctness inside code mode; whether per-action typed
S6 managers remove the argument-error failure mode; S6 with composites; C1 (Unity's own surface curated) vs S6 attribution; bypass
rates with the official skill installed; option E; third-party tasks that are not ambiguous; delegated writes (P0-16d);
maintenance of hand-written normalisers across Pipeline 0.7 → 0.8.

### 17.4 v2.5 independent review log

Reviewers: Agent C (benchmark-design critic, before the pilot; `C_design_review.md`, 26 objections, 6 BLOCKERs fixed before run 1 —
see PREREG A1) and a fresh final reviewer (after the pilot; `FINAL_REVIEW.md`, 13 objections). Final-review dispositions:

| id | objection (short) | sev | verdict | change applied |
|---|---|---|---|---|
| O1 | "toward-S5" flips under pre-A5 scoring, dead-bridge originals, or without the 0.25 floor | HIGH | ACCEPT | PILOT_RESULTS §6 robustness table; §0/G15/§17.3: labels non-robust, no direction |
| O2 | "against-S6" rests on T3 (grep-solvable) and the eval penalty; flat-schema errors did not decide outcomes | HIGH | ACCEPT | causal wording corrected in PILOT_RESULTS §4/§5, §0, §17.3 |
| O3 | the S6 that ran was a flat lossy union with eval, not the owner's typed S6 | HIGH | ACCEPT | stated in §0/§17.3/PREREG corrections; full spike uses per-action typed S6 + composites (P0-16c, G15) |
| O4 | security policy mixed into the architecture verdict (eval given, then penalised; `batch` counted as eval-like) | HIGH | ACCEPT | full spike: production-shaped S6 without eval **and** observed-with-eval variant; both reported |
| O5 | integrity claims partly wrong (A3 after 0/7; A5 outcome-triggered and mis-timed; missing sensitivity files; Gate F "before any S6 result") | HIGH | ACCEPT | PREREG corrections note; `results/*.pre_A5`, `results/deadbridge/` committed; Gate F wording fixed |
| O6 | S4's T7 used legacy `unity_dump_scene_graph` to find the field path | MED | ACCEPT | PILOT_RESULTS §6 capability-equivalence note |
| O7 | wall-time drop came from two outliers; medians show S6-domain slower | MED | ACCEPT | PILOT_RESULTS §4/§5 corrected to medians |
| O8 | §4/§5.1/§11/§14b/R23 and Editor-hour budgets conflict with Gate F | MED | ACCEPT | §4 scoped to S5; §5.1 budget scoped; §11 "open"; §14b writes = production policy + P0-16d; R23 trigger pooled; budgets unified (≈130–320 h) |
| O9 | C exempt from D7/D8/D10; D6 counts typed describe/invoke as escapes | MED | ACCEPT | C now needs D7/D8/D10; D6 redefined |
| O10 | "S5 first" risks S6 never being tested properly | MED | ACCEPT (disclosed deviation from the pilot consequence) | same-wave S5 + S6 in G15/§17.3 |
| O11 | third-party requirement unmet (T3★ infeasible, T9★ ambiguous) | MED | ACCEPT | PILOT_RESULTS §6; full spike requires reason-unique third-party tasks |
| O12 | "0 discovery calls" means not observable in code mode | LOW | ACCEPT | PILOT_RESULTS §6 |
| O13 | PREREG §2 network statement stale; uncached-tokens lens post-hoc | LOW | ACCEPT | PREREG corrections note; PILOT_RESULTS §6 |

### 17.5 v2.6 — architecture theory validation (2026-10-04, planning only)

No new empirical evidence was generated for v2.6; no code, benchmark, experiment or Gate F was run. Changes: header; §0 (RU
summary of the pipeline); §3 Principle 6 (distribution ≠ transport; shell-first no longer assumed); §4 (security-boundary
policy bullet; transport bullet); §5.1 (legacy-noise note; S6-P ≈ the core profile with Unity hidden); §7 (P0-16 re-staged:
a/b/d are inputs, c = Stage B confirmatory for finalists only; work classification; new P0-17 Stage A cheap falsification with
third-party task checklist and budget; new S-10 legacy noise cleanup and S-11 discovery metadata; Gate F rewritten as the final
confirmatory gate with outcomes A/C/F/**M**/E/I, paired design, primary metrics, exploratory vs confirmatory arms, sequential
stopping; façade backlog; capacity); §9 A4; §10 G15–G17; §11; §12 R27–R30; §14b; new §18 Theory Register.

Evidence preserved and how it is read: the pilot's S6-domain was a flat regrouping of all 154 actions with `eval` and no
composites, so it neither supports nor rejects Nexus-primary (H2 stays UNTESTED); manager grouping ≈4% (F23) is representation
compression, distinct from capability-surface reduction (H1, H2); deferred loading moves cost into discovery (F21, F24);
normalisation cuts emitted output 20–100× but whole-task value is unconfirmed (H6); the current Nexus `unity_dump_scene_graph`
is the noisiest default output (S-10); catalog-driven routing is feasible but outputs stay Nexus-maintained (F25, H7); shell
`unity` is unreliable from Codex's sandbox (F27, H5).

**Gate F threshold changes v2.5 → v2.6 (disclosed per the "tighten, never loosen" rule; no Stage B run exists yet):**

| Item | v2.5 | v2.6 | Direction / justification |
|---|---|---|---|
| Scope | S4/S5/S6 all arms, all clients | ≤3 challengers + permanent S4 and C1, primary client first | Budget/power (PLAN_REVIEW_v2.6_B); C1 made mandatory = tighter |
| D1 | production-policy success, −10 pp | unchanged (production-policy primary; observed reported) | Restored after review A |
| D3 | geometric-mean cost ratio ≤ 0.8, CI < 1.0 | same + single decider (total tokens) + uncached must not point the other way | Tighter |
| D6 | no escapes to raw output / direct Unity | same, **except** M's pre-declared Unity-direct categories | **Loosened for M only**, by explicit owner requirement (an escape path is a designed feature); reported per call |
| D7 | ≥80% generated, ≤600 hand lines | unchanged, measured on real Pipeline versions | Restored after review A; measurement tighter |
| D10 | advantage in ≥2 of 3 clients, not worse on D1 in any | same, applied to the selected outcome only | Scope narrowed (fewer arms), threshold unchanged |
| Outcomes | A, C, F, E, mixed, I | A, C, F, **M**, E, I; I checked before A | M made first-class (owner); C now also needs D7/D8/D10 = tighter |
| Pilot consequence "S5 first" / v2.5 §17.4 O10 "same wave" | — | superseded by P0-17 → challengers → Stage B | Process change, no threshold loosened |

### 17.6 v2.6 plan-review log (document review only; no experiments)

Two independent reviewers: A (owner's 10 questions; `G_nexus_primary/PLAN_REVIEW_v2.6_A.md`, 19 objections) and B (Stage A/B
methodology with offline simulation of the rules on the pilot's variance; `PLAN_REVIEW_v2.6_B.md`, 15 objections).

| id | objection (short) | sev | verdict | change applied |
|---|---|---|---|---|
| A-H1 / B | Finalist rule could drop C1 (condition backwards); S4 + C1 consumed finalist slots | HIGH | ACCEPT | S4 and C1 are permanent Stage B comparators, not finalists; ≤3 challengers (P0-17f, Gate F, §18) |
| A-H2 | Gate F thresholds loosened without disclosure (D7 cap, D10, D1, D6) | HIGH | ACCEPT | D1, D7, D10 restored; D6 loosened only for M's declared categories, disclosed; diff table in §17.5 |
| A-H3 | No route for ordinary writes in S6-P/M; results would depend on task mix | HIGH | ACCEPT | S6-P: ≤3 typed bounded-write actions in a separate tool (research observation); M: writes Unity-direct by declaration; they differ exactly there |
| B-H | Stage A thresholds at the effect sizes they target → most real effects INSUFFICIENT; drop-one-task too strict | HIGH | ACCEPT | Rules recalibrated (falsify on cost when success ≤ +5 pp; survive at −10 pp; cost-only decider; relaxed robustness band); ≈8 tasks × N≈6 |
| B-H | H1 unpaired (C1 in A1, S6-P/M in A2), "best Nexus arm" undefined, vacuous falsification | HIGH | ACCEPT | C1/S4 re-run in A2 paired; H1 defined only if a Nexus arm survived; evaluation order fixed |
| B-H | H4 "≥70% of improvement" ill-defined | HIGH | ACCEPT | H4 redefined with the generic SURVIVED rule (S4+ vs S4; challengers vs S4+) |
| B-H | Stage B 15–40 h underpowered | HIGH | ACCEPT | Stage B ≈900–1,500 runs ≈30–55 h stated; I is the expected outcome for small effects |
| A/B-M | Budget/run counts inconsistent (290 vs 336–384; §0 "5–15 runs") | MED | ACCEPT | ≈336–384 runs, ≈12–25 h incl. voids; §0 aligned |
| A-M | Stale v2.5 text (§0 decision, P0-16 lead, S1e/S6-bypass, R24/R25, Phase 0 note, §17.4 O10) | MED | ACCEPT | Pointers/notes added; S1e folded into S4+; S6-bypass runs with any F/M challenger |
| A-M | Timing: A1 before Gate A; P0-17 under Phase 0; Stage B collides with Gate B | MED | ACCEPT | Separate "Architecture research track"; A1 weeks 5–6; Stage B after Gate B runs |
| A-M | Capacity not funded | MED | ACCEPT | ≈13.5 agent-days funded by postponing C-01, V-04b, V-07 |
| A-M | "Bench-quality" composites ambiguous (prototype drift risk) | MED | ACCEPT | A2 waits for merged S-05/S-06/L-03/L-04; no bench re-implementation |
| A-M | S-10/S-11 silently change the S4 baseline | MED | ACCEPT | S4 = frozen 1.7.0 release; S4+ carries S-10/S-11 |
| A-M | §4 approval rule loophole ("where that would weaken") | MED | ACCEPT | Unconditional rule |
| A-M | Principle 6 made MCP "first-class" ahead of evidence | MED | ACCEPT | Both entries supported, neither assumed primary |
| B-M | Multiple comparisons / selection by noise | MED | ACCEPT | Stage A never pooled into Stage B; ≈15% chance noted |
| B-M | Total vs uncached tokens both primary without a decider | MED | ACCEPT | Total = decider; uncached reported, must not contradict |
| A-L / B-L | "INCONCLUSIVE" not an allowed state; H1–H4 vs H5–H7 not labelled; S5 without a theory; "leading hypothesis" overstated; H4 pilot signal offline-only | LOW | ACCEPT | INSUFFICIENT EVIDENCE used; primary/secondary tags; H8 added; "owner's prior, not evidence"; "offline only" |
| A-L | Duplicated PHASE 2 heading | LOW | ACCEPT | fixed |

---------------------------------------------------------------------
## 18. ARCHITECTURE THEORY REGISTER (v2.6)
---------------------------------------------------------------------

Purpose: every architecture hypothesis gets a cheapest falsification test, pre-registered rules and a state, so theories are
killed, not accumulated (R27). States: **UNTESTED · PILOT SIGNAL · FALSIFIED · SURVIVED CHEAP TEST · CONFIRMATORY CANDIDATE ·
CONFIRMED · INSUFFICIENT EVIDENCE.** Pipeline: existing evidence → this register → P0-17 (Stage A) → ≤3 challengers
(CONFIRMATORY CANDIDATE) + permanent comparators S4 and C1 → P0-16c (Stage B) → Gate F (CONFIRMED / FALSIFIED / INSUFFICIENT EVIDENCE) → implementation decision.

**Stage A rule framework** (exact numbers frozen in `PREREG_P0-17.md` before any run; may be tightened, never loosened;
calibrated by offline simulation on the pilot's variance in `PLAN_REVIEW_v2.6_B.md`: ≈6–10% false calls on equal arms, ≈85–90%
power on large effects, ≈57% on −15 pp). Paired by task and seed, ≈8 tasks × N≈6 per arm (more tasks beat more repeats), one
primary client, decider = total tokens per success. For "challenger X vs comparator Y":
- **FALSIFIED** — paired success difference ≤ −15 pp, **or** cost-per-success ratio ≥ 1.25 with success difference ≤ +5 pp;
  and every drop-one-task subset stays on the failing side of a relaxed band (≤ −10 pp, or ratio ≥ 1.15).
- **SURVIVED CHEAP TEST** — success difference ≥ −10 pp **and** cost-per-success ratio ≤ 0.85 (calls reported, not decisive); and
  every drop-one-task subset stays within ≥ −15 pp and ratio ≤ 0.95.
- **INSUFFICIENT EVIDENCE** — anything else (the allowed state for "inconclusive"). Such a theory may still proceed only if fewer
  than two challengers survive.
Stage A data are never pooled into Stage B. Every result is reported as observed and production-policy (§7 Gate F); security is
never scored inside these rules. Primary theories (Stage A agent cells): H1–H4. Secondary (desk/offline first): H5–H7. Tracked
arm theory: H8.

**Arm ↔ theory map:** S4 (frozen 1.7.0 baseline, permanent comparator) · C1 (permanent comparator; H1 control) · S4+ = H4 · S5 =
H8 (outcome C) · S6-P = H2 (outcome F) · M = H3 (outcome M) · transports = H5 (no agent arms) · normaliser levels = H6 (offline
first) · Pipeline versions = H7.

| ID | Hypothesis | Why it matters | Cheapest falsification | Required evidence | Primary metrics | Pass / fail / inconclusive | Status | Next action |
|---|---|---|---|---|---|---|---|---|
| **H1** (primary) capability reduction vs Nexus value | A compact Nexus surface's benefit is **not** explained just by exposing fewer official Unity tools | If curated Unity (C1) is as good, a façade is not justified (→ E) | P0-17d: C1 re-run paired with S6-P and M on the same tasks/seeds | C1 built from the same task-relevant command subset, official names | success, cost/success (total; uncached reported), calls/success, discovery calls | Defined only if some Nexus-primary arm (S6-P or M) SURVIVED vs S4; "best Nexus arm" = the surviving one with the lower cost per success. SURVIVED if the best Nexus arm passes the SURVIVED rule vs C1; FALSIFIED if C1 is within −5 pp success of it and its C1/Nexus cost ratio ≤ 1.10 (C1 matches it); evaluated in that order; otherwise — including when no Nexus arm survived vs S4 — INSUFFICIENT EVIDENCE | UNTESTED | P0-17a/d |
| **H2** (primary) production-shaped Nexus-primary (S6-P) | A compact typed Nexus surface (`nexus_status/snapshot/diagnose/compile/look/run/get` + ≤1 bounded query; real merged composites; no `eval` through Nexus; Unity hidden; ≤3 typed bounded-write actions in a separate tool, research observation only; no generic invoke) beats the current design on whole-task economics without losing success | It is the owner's intended architecture; the pilot's S6-domain (flat 154-action regrouping) does **not** count | P0-17d: S6-P vs S4 (re-baseline) and vs S5 | Bench-quality composites (L-03, S-05, S-06, L-04); capability check per task | success (observed + production policy), cost/success, calls/success, invalid-argument calls, escape usage, coverage | Stage A survive / falsify rules vs S4; also reported vs S5 | UNTESTED | P0-17b/d (after composites exist) |
| **H3** (primary) mixed Nexus-primary boundary (M) | Nexus as the preferred path for inspection, hierarchy/component understanding, diagnostics, compile verification, console interpretation, references/search where useful, capture/look, Editor tool runs and bounded common workflows; Unity-direct only for `eval`, `eval_file`, `run_script`, free-form `batch`, packages, builds/build targets, risky settings and rare capabilities | Probably the most plausible final architecture; an escape path is a designed feature, not a failure | P0-17d: M vs S4, S6-P and C1 (M and S6-P differ exactly in the write/exception boundary) | Declared Unity-direct categories (writes + §4 exceptions) frozen in the prereg; escape-call classification | as H2 + share of Unity-direct calls inside the declared categories | SURVIVED if the Stage A survive rule holds vs S4 and ≥90% of Unity-direct calls fall in declared categories; FALSIFIED by the Stage A falsify rule or if <70% are declared-category; else INSUFFICIENT EVIDENCE | UNTESTED | P0-17b/d |
| **H4** (primary) current architecture + improved discovery (S4+) | Cheap metadata/discovery changes (action vocabulary in top-level descriptions, search hints, better names, profiles/allowlists, skill routing incl. option-E recipes, hiding legacy dumps) close most of the gap without a larger façade | If true, it weakens the case for a rewrite (→ A or E) | S-11 offline retrieval re-run first; then P0-17c S4+ vs S4, and P0-17d challengers vs S4+ | S-10/S-11 drafts; offline retrieval set | offline retrieval recall, success, cost/success, discovery and wrong-tool calls | SURVIVED if S4+ passes the SURVIVED rule vs S4 **and** no challenger passes the SURVIVED rule vs S4+; FALSIFIED if some challenger passes SURVIVED vs S4+ while S4+ does not pass SURVIVED vs S4; else INSUFFICIENT EVIDENCE | PILOT SIGNAL (offline retrieval only: action lists in Nexus descriptions raised recall@8 .55 → .88, A_tool_search) | S-11, then P0-17c/d |
| **H5** (secondary) transport | One warm backend (persistent MCP, Pipeline HTTP, persistent shell, other supported transport) gives the most reliable, lowest-overhead official Unity access, independent of the façade shape | Transport failures must not be read as façade failures | P0-17e micro-tests (no agent runs) | Latency, reconnect after domain reload, sandbox behaviour, port-file side effects per transport | p95 per-call overhead, reload recovery rate, side effects | SURVIVED (per transport): overhead ≤ 50 ms p95 over a warm baseline, 20/20 reload recoveries, no Editor-discovery side effects under default Codex/Claude Code sandboxes; FALSIFIED by any discovery-breaking side effect; else INSUFFICIENT EVIDENCE | PILOT SIGNAL (warm `unity mcp` ≈100 ms/call; shell CLI fails in Codex sandbox and deletes the port file, F27) | P0-17e |
| **H6** (secondary) normaliser sufficiency | Outputs can be compressed to a stated detail level without agents losing required information | Compression without sufficiency is a token win and a success loss (R23) | P0-16b offline sufficiency questions per output class (hierarchy, console, object/component inspection, references, compile diagnostics, tests, import state) at `summary` / `normal` / `full-bounded` | Frozen sufficiency questions written blind to the normalisers | sufficiency rate, handle fetches, omitted critical data; task success when used in Stage A | per class and level: SURVIVED if ≥90% of questions answered from output + ≤1 handle fetch; FALSIFIED if <70%; else INSUFFICIENT EVIDENCE | PILOT SIGNAL (emitted output −20–100×; sufficiency untested; contamination-exposed normalisers) | P0-16b |
| **H7** (secondary) Pipeline version maintenance | Catalog-generated routing + hand-written normalisers survive Pipeline minor upgrades at bounded cost | Maintenance can sink any façade (R22) | P0-17e: run the existing generator and normalisers against Pipeline N / N-1 / N+1 where available | Real package versions; contract tests | breakage count, repair time, LOC changed, contract-test failures | SURVIVED if each version step is repaired in ≤1 day and ≤200 LOC; FALSIFIED if >3 days or >50% of normalisers break; else INSUFFICIENT EVIDENCE | UNTESTED (only estimates and a reconstructed 0.3→0.7 churn, F25) | P0-17e |
| **H8** (tracked arm) selective façade (S5) | Adding normalised Nexus reads next to visible Unity lowers cost per success without losing success | Outcome C; cheapest façade with Unity still visible | P0-17c: S5 vs S4 (and vs S4+) | Existing S5 mock normalisers (contamination-exposed; P0-16b sufficiency first) | success, cost/success, tool-output tokens, sufficiency | Stage A generic rules vs S4 | PILOT SIGNAL (pilot: toward-S5 label, non-robust — flips to neutral without T3/T5/T9 effects; PILOT_RESULTS §6) | P0-16b, then P0-17c |

Retired framing (kept for the record): the v2.4 "full façade rejected" verdict and the v2.5 pilot labels are not theory states;
the pilot is recorded only as PILOT SIGNAL where noted above.
