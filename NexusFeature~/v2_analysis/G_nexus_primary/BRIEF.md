# Brief for research agents — v2.5 corrective pass (2026-10-03)

Repo root: /Users/daliys/Daliys/UnityProjects/UnityTestForNexus/Assets/NexusUnity
Active plan: NexusFeature~/MASTER_PLAN_V2.md (v2.4). First-pass analysis: NexusFeature~/v2_analysis/F_facade_hypothesis.md;
evidence: NexusFeature~/v2_analysis/F_facade_evidence/ (unity_list.json = live typed catalog of 154 Unity Pipeline 0.7 commands;
unity_mcp_tools_list.jsonl = `unity mcp` tools/list; R_clients.md = client tool-loading research; R_repo.md = Nexus manager history).

## Central question
Should Nexus remain a small set of tools living BESIDE Unity CLI (agent sees Unity's ~154 commands + Nexus), or can Nexus become the
PRIMARY compact AI-facing interface while Unity CLI/Pipeline is the HIDDEN official execution backend? If only partially, where is the boundary?

## Arms (names are fixed)
- S4 — current design: agent sees Unity CLI/`unity mcp` (154 tools) + current Nexus + routing instructions.
- S5 — selective façade: S4 + Nexus-normalised versions of high-volume reads (hierarchy/console/find) and bounded snapshot/diagnose. Unity stays direct.
- S6 — Nexus-primary / Unity-hidden-backend: agent sees only a compact Nexus surface; Nexus internally delegates to OFFICIAL Unity commands
  (no reimplementation), normalises output. Variants: S6-domain (~6–15 typed domain managers + composites), S6-curated (very small set +
  discovery/escape), S6-router (one generic tool + discovery; a minimal-schema control only).
- E — skill recipes + `unity eval` snippets + upstream request (no shipped code).

## Forbidden vs allowed
Forbidden: Nexus primitive → own duplicate implementation → Editor API. Allowed (hypothesis): Nexus action → official Unity command → optional
normalisation → agent. Composite: several Unity sources → orchestration → compact semantic result.

## Known (keep)
154 tools; full schema ~21–25k tokens; Claude Code/Codex/Cursor defer schemas (names only); Gemini CLI does not; manager SYNTAX saved only ~4%
vs atomic for the SAME 71 actions (representation compression) — capability-surface reduction (154 → curated subset) is a separate, untested
question; raw hierarchy ~86k tokens on ~1.1k objects (no depth param); console ~29k; warm Unity call ~100 ms; cold `unity command` 1.2–2.2 s;
normalisation ~1 ms.

## Rules for all agents
Research only. No production code. Prototype/bench code only under NexusFeature~/bench/facade_mock/ (never shipped). Do not edit
MASTER_PLAN_V2.md. Label claims MEASURED / REPO / DOC (URL) / REPORTED / HYPOTHESIS / UNKNOWN. No speculation as fact.
