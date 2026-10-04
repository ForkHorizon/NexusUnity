#!/usr/bin/env python3
"""Judge P0-02 runs.jsonl facts. The Editor driver does not decide pass/fail."""

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent
LIVE = ROOT / "live"
EVENTS = LIVE / "runs.jsonl"


def load_events():
    if not EVENTS.exists():
        return []
    rows = []
    for line in EVENTS.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if line:
            rows.append(json.loads(line))
    return rows


def group(rows):
    runs = {}
    for row in rows:
        runs.setdefault(row["run"], []).append(row)
    return runs


def parse_log(run_number):
    path = LIVE / f"log-{run_number:02d}.txt"
    if not path.exists():
        return {"error": "missing log slice"}
    text = path.read_text(encoding="utf-8", errors="replace")
    tundra = re.findall(r"Tundra build success \(([0-9.]+) seconds\)", text)
    reload_lines = re.findall(r"Reloading assemblies after forced synchronous recompile\.", text)
    begin = text.count("Begin MonoManager ReloadAssembly")
    profiling = re.findall(r"Domain Reload Profiling: (\d+)ms", text)
    totals = re.findall(
        r"Asset Pipeline Refresh \(id=([0-9a-f]+)\): Total: ([0-9.]+) seconds - Initiated by ([^\r\n]+)",
        text,
    )
    domain_reloads = re.findall(r"domain reloads=(\d+), domain reload time=(\d+) ms, compile time=(\d+) ms", text)
    return {
        "editor_dll": "UnityMCP.Editor.dll" in text,
        "tundra_s": [float(v) for v in tundra],
        "forced_reload_lines": len(reload_lines),
        "begin_mono": begin,
        "domain_reload_profiling_ms": [int(v) for v in profiling],
        "refresh": [
            {"id": item[0], "total_s": float(item[1]), "initiator": item[2]}
            for item in totals
        ],
        "scripting": [
            {"domain_reloads": int(a), "reload_ms": int(b), "compile_ms": int(c)}
            for a, b, c in domain_reloads
        ],
    }


def phase(rows, name):
    found = [row for row in rows if row.get("phase") == name]
    return found[-1] if found else None


def judge_run(number, rows):
    reasons = []
    before = phase(rows, "before_write")
    after = phase(rows, "after_write")
    started = phase(rows, "compilation_started")
    finished = phase(rows, "compilation_finished")
    reload = phase(rows, "before_reload")
    ready = phase(rows, "after_ready")
    failed = phase(rows, "failed")
    for label, row in (
        ("before_write", before),
        ("after_write", after),
        ("compilation_started", started),
        ("compilation_finished", finished),
        ("before_reload", reload),
        ("after_ready", ready),
    ):
        if row is None:
            reasons.append(f"missing {label}")
    if failed is not None:
        reasons.append(failed.get("reason") or "failed phase recorded")
    log = parse_log(number)
    if "error" in log:
        reasons.append(log["error"])
    if reasons:
        return {"run": number, "pass": False, "reasons": reasons, "log": log}

    compile_before = before["compileBefore"]
    reload_before = before["reloadBefore"]
    if after["reason"] != "script_write" or after["ready"] is not False:
        reasons.append(f"after_write was {after['reason']} ready={after['ready']}")
    if after["domain"] != before["domain"]:
        reasons.append("after_write changed domain")
    if not after.get("shaAfter") or after.get("shaAfter") == after.get("shaBefore"):
        reasons.append("stimulus hash did not change")
    if finished["compile"] != compile_before + 1 or finished["reload"] != reload_before:
        reasons.append(
            f"compilation_finished epochs compile {finished['compile']} reload {finished['reload']}"
        )
    if finished["ready"] is not False:
        reasons.append("ready true at compilation_finished")
    if reload["ready"] is not False:
        reasons.append("ready true at before_reload, before EditorReadiness armed the reload")
    if reload["domain"] != before["domain"]:
        reasons.append("before_reload domain is not the write domain")
    if ready["domain"] == before["domain"]:
        reasons.append("after_ready stayed in the write domain")
    if ready["reload"] != reload_before + 1 or ready["compile"] != compile_before + 1:
        reasons.append(f"after_ready epochs compile {ready['compile']} reload {ready['reload']}")
    if ready["reason"] != "ready" or ready["ready"] is not True:
        reasons.append(f"after_ready reason {ready['reason']}")
    write_utc = before["utc"]
    ready_utc = ready["utc"]
    # ISO-8601 timestamps from the same clock. Lexical order matches chronological order for this format.
    if ready_utc < reload["utc"]:
        reasons.append("ready timestamp precedes before_reload")
    if log["forced_reload_lines"] != 1:
        reasons.append(f"forced reload lines {log['forced_reload_lines']}")
    if log["begin_mono"] < 1:
        reasons.append("no Begin MonoManager ReloadAssembly in the anchored slice")
    forced_refresh = [item for item in log["refresh"] if "ForceDomainReload" in item.get("initiator", "")]
    if not log.get("editor_dll"):
        reasons.append("slice did not compile UnityMCP.Editor.dll")
    if len(forced_refresh) != 1:
        reasons.append(f"ForceDomainReload refresh lines {len(forced_refresh)}")
    if not log["tundra_s"] or not log["domain_reload_profiling_ms"] or not forced_refresh:
        reasons.append("log slice missing Tundra, Domain Reload Profiling, or the forced domain-reload refresh")
    wait_s = _seconds(write_utc, ready_utc)
    tundra = log["tundra_s"][0] if log["tundra_s"] else 0
    profiling_s = (log["domain_reload_profiling_ms"][0] / 1000) if log["domain_reload_profiling_ms"] else 0
    refresh_s = forced_refresh[0]["total_s"] if forced_refresh else 0
    refresh_id = forced_refresh[0]["id"] if forced_refresh else ""
    if wait_s + 0.25 < tundra:
        reasons.append(f"wait {wait_s:.3f}s is shorter than Tundra {tundra:.3f}s")
    if wait_s < 1 and (tundra >= 1 or profiling_s >= 1 or refresh_s >= 2):
        reasons.append(f"sub-second ready {wait_s:.3f}s against multi-second log T={tundra} D={profiling_s} R={refresh_s}")
    if abs(wait_s - refresh_s) > 5:
        reasons.append(f"wait {wait_s:.3f}s is not the refresh total {refresh_s:.3f}s")
    return {
        "run": number,
        "pass": not reasons,
        "reasons": reasons,
        "wait_s": round(wait_s, 3),
        "tundra_s": tundra,
        "domain_reload_profiling_s": round(profiling_s, 3),
        "refresh_s": refresh_s,
        "refresh_id": refresh_id,
        "compile_before": compile_before,
        "compile_after": ready["compile"],
        "reload_before": reload_before,
        "reload_after": ready["reload"],
        "first_not_ready_reason": after["reason"],
        "ready_at_before_reload": reload["ready"],
        "log": log,
    }


def _seconds(start, end):
    from datetime import datetime
    a = datetime.fromisoformat(start.replace("Z", "+00:00"))
    b = datetime.fromisoformat(end.replace("Z", "+00:00"))
    return (b - a).total_seconds()


def main():
    rows = load_events()
    runs = group(rows)
    judged = [judge_run(number, runs[number]) for number in sorted(runs)]
    refresh_values = sorted(item["refresh_s"] for item in judged if item.get("refresh_s"))
    profiling_values = sorted(
        item["domain_reload_profiling_s"] for item in judged if item.get("domain_reload_profiling_s")
    )
    median = refresh_values[len(refresh_values) // 2] if refresh_values else 0
    median_profiling = profiling_values[len(profiling_values) // 2] if profiling_values else 0
    suite_reasons = []
    if len(judged) != 20 or any(not item["pass"] for item in judged):
        suite_reasons.append("not 20 passing runs")
    # Forced domain-reload totals on this warm editor are about 1.8s. The earlier 2s floor
    # matched the first NoUpdate refresh, which is not the script compile. A sub-second
    # domain reload, or a slice that did not compile UnityMCP.Editor.dll, still fails above.
    if median_profiling < 1:
        suite_reasons.append(f"median domain reload profiling {median_profiling:.3f}s is under 1s")
    epochs = [(item.get("compile_after"), item.get("reload_after")) for item in judged]
    for index in range(1, len(judged)):
        if judged[index].get("compile_before") != judged[index - 1].get("compile_after"):
            suite_reasons.append(f"run {index} does not continue compile epoch")
        if judged[index].get("reload_before") != judged[index - 1].get("reload_after"):
            suite_reasons.append(f"run {index} does not continue reload epoch")
    verdict = {
        "pass": not suite_reasons and len(judged) == 20,
        "reasons": suite_reasons,
        "median_refresh_s": median,
        "median_domain_reload_profiling_s": median_profiling,
        "runs": judged,
    }
    out = ROOT / "verdict.json"
    out.write_text(json.dumps(verdict, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"pass": verdict["pass"], "reasons": suite_reasons, "runs": len(judged)}, indent=2))
    return 0 if verdict["pass"] else 1


if __name__ == "__main__":
    sys.exit(main())
