"""Command line for the architecture lab.

`evolve` and `confirm` launch a real loop only with the explicit confirm flags
and budgets. Without those flags they still refuse before any model call.
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from lab.candidates.baselines import (
    all_baselines,
    smoke_hidden_gateway,
    smoke_official_subset,
    zero_capability,
)
from lab.catalog.extract import load_catalog
from lab.reports.render import render_comparison, render_lineage
from lab.runners.scratch import prepare_project, start_editor, stop_editor
from lab.search.engine import evolve_request, plan_generation
from lab.search.feasibility import classify_candidate, per_success
from lab.search.pareto import pareto_frontier
from lab.search.promote import freeze, mutate_frozen, transition
from lab.surfaces.generate import generate_surface, surfaces_differ

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_CATALOG = ROOT / "catalog" / "snapshots" / "pipeline-0.8.0-exp.1.json"


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(prog="architecture-lab")
    parser.add_argument("--catalog", default=str(DEFAULT_CATALOG))
    sub = parser.add_subparsers(dest="command", required=True)

    sub.add_parser("baselines")
    dry = sub.add_parser("mutate-dry")
    dry.add_argument("--seed", type=int, default=17)
    dry.add_argument("--population", type=int, default=5)

    sub.add_parser("promote-dry")
    sub.add_parser("degeneracy")

    evolve = sub.add_parser("evolve")
    evolve.add_argument("--model", default="gpt-6-luna")
    evolve.add_argument("--reasoning", default="low")
    evolve.add_argument("--population", type=int, default=8)
    evolve.add_argument("--generations", type=int, default=None)
    evolve.add_argument("--max-runs", type=int, default=None)
    evolve.add_argument("--max-uncached-tokens", type=int, default=None)
    evolve.add_argument("--seed", type=int, default=17)
    evolve.add_argument("--confirm-search", action="store_true")
    evolve.add_argument("--candidates", choices=("baselines", "smoke"), default="baselines")
    evolve.add_argument("--tasks", choices=("search", "smoke"), default="search")
    evolve.add_argument("--label", default="SEARCH")
    evolve.add_argument("--resume", action="store_true")
    evolve.add_argument("--experiment", default=None)

    confirm = sub.add_parser("confirm")
    confirm.add_argument("--candidate", required=True)
    confirm.add_argument("--confirm-final", action="store_true")
    confirm.add_argument("--include-holdout", action="store_true")
    confirm.add_argument("--integration", action="store_true")
    confirm.add_argument("--seed", type=int, default=101)
    confirm.add_argument("--max-runs", type=int, default=40)
    confirm.add_argument("--max-uncached-tokens", type=int, default=2_000_000)
    confirm.add_argument("--model", default="gpt-6-luna")
    confirm.add_argument("--reasoning", default="low")

    sub.add_parser("surfaces")
    sub.add_parser("prepare-project")
    sub.add_parser("editor-build")
    sub.add_parser("editor-start")
    sub.add_parser("editor-stop")
    sub.add_parser("self-check")
    smoke = sub.add_parser("smoke")
    smoke.add_argument("--seed", type=int, default=1)
    args = parser.parse_args(argv)
    catalog = load_catalog(Path(args.catalog))

    if args.command == "baselines":
        rows = []
        for candidate in all_baselines(catalog):
            rows.append(
                {
                    "candidate_id": candidate["candidate_id"],
                    "architecture_hash": candidate["architecture_hash"],
                    "strategy": candidate["architecture"]["surface_strategy"],
                    "discovery": candidate["architecture"]["discovery_mechanism"],
                }
            )
        print(json.dumps(rows, indent=2))
        return 0

    if args.command == "surfaces":
        left = generate_surface(smoke_official_subset(catalog), catalog)
        right = generate_surface(smoke_hidden_gateway(catalog), catalog)
        print(json.dumps(
            {
                "differ": surfaces_differ(left, right),
                "official": left["model_visible_names"],
                "hidden": right["model_visible_names"],
                "official_hash": left["candidate_hash"],
                "hidden_hash": right["candidate_hash"],
            },
            indent=2,
        ))
        return 0

    if args.command == "mutate-dry":
        population = all_baselines(catalog)[: args.population]
        plan = plan_generation(population, catalog, seed=args.seed, task_root=ROOT / "tasks")
        text = render_lineage(plan["children"])
        print(text)
        print(json.dumps({"children": len(plan["children"]), "rejected": plan["duplicates_or_rejected"], "executed": False}))
        return 0

    if args.command == "promote-dry":
        print(json.dumps(_promote_dry(), indent=2))
        return 0

    if args.command == "degeneracy":
        print(json.dumps(_degeneracy(catalog), indent=2))
        return 0

    if args.command == "evolve":
        try:
            decision = evolve_request(
                {
                    "confirm_search": args.confirm_search,
                    "max_runs": args.max_runs,
                    "max_generations": args.generations,
                    "max_uncached_tokens": args.max_uncached_tokens,
                    "model": args.model,
                }
            )
        except Exception as exc:
            print(f"REFUSED: {exc}", file=sys.stderr)
            print("Dry plan only. No model runs were started.")
            plan = plan_generation(all_baselines(catalog), catalog, seed=args.seed, task_root=ROOT / "tasks")
            print(render_lineage(plan["children"]))
            return 2
        from lab.runners.live import execute_evolution

        experiment = args.experiment or f"evolve-{args.candidates}-s{args.seed}"
        try:
            payload = execute_evolution(
                population_limit=args.population,
                generations=args.generations,
                max_runs=args.max_runs,
                max_uncached_tokens=args.max_uncached_tokens,
                seed=args.seed,
                label=args.label,
                candidates_name=args.candidates,
                tasks_name=args.tasks,
                resume=args.resume,
                experiment_id=experiment,
                model=args.model,
                reasoning=args.reasoning,
            )
        except Exception as exc:
            print(f"REFUSED: {exc}", file=sys.stderr)
            return 2
        print(payload["report"])
        summary = payload["result"]
        print(json.dumps(
            {
                "started": True,
                "executed": summary.get("executed"),
                "stop_reason": summary.get("stop_reason"),
                "generation": summary.get("generation"),
                "runs_used": summary.get("runs_used"),
                "uncached_used": summary.get("uncached_used"),
                "label": summary.get("label"),
                "experiment_id": experiment,
                "budget": decision,
            },
            indent=2,
        ))
        return 0

    if args.command == "prepare-project":
        print(json.dumps(prepare_project(), indent=2))
        return 0

    if args.command == "editor-build":
        pid = start_editor(execute_method="LabFixtureBuilder.Build", quit_after=True)
        print(json.dumps({"pid": pid, "note": "builder calls EditorApplication.Exit"}, indent=2))
        return 0

    if args.command == "editor-start":
        pid = start_editor()
        print(json.dumps({"pid": pid, "port": 7811, "project": "/tmp/nexus-arch-lab"}, indent=2))
        return 0

    if args.command == "editor-stop":
        stop_editor()
        print("SIGTERM sent to the recorded lab editor pid")
        return 0

    if args.command == "self-check":
        from lab.runners.live import execute_self_check

        payload = execute_self_check()
        print(json.dumps({"valid": payload["valid"], "invalid": payload["invalid"], "tasks": payload["tasks"]}, indent=2))
        return 0

    if args.command == "smoke":
        from lab.runners.live import execute_smoke

        payload = execute_smoke(seed=args.seed)
        print(payload["report"])
        print(json.dumps(payload["acceptance"], indent=2))
        return 0 if payload["acceptance"]["ok"] else 2

    if args.command == "confirm":
        if not args.confirm_final:
            print("REFUSED: confirm requires --confirm-final. Holdout was not read.", file=sys.stderr)
            return 2
        from lab.runners.live import execute_confirmation

        try:
            payload = execute_confirmation(
                candidate_hash=args.candidate,
                include_holdout=args.include_holdout,
                integration=args.integration,
                seed=args.seed,
                max_runs=args.max_runs,
                max_uncached_tokens=args.max_uncached_tokens,
                model=args.model,
                reasoning=args.reasoning,
            )
        except Exception as exc:
            print(f"REFUSED: {exc}", file=sys.stderr)
            return 2
        print(payload["report"])
        public = payload["result"]
        print(json.dumps(
            {
                "status": public.get("status"),
                "holdout_executed": public.get("holdout_executed"),
                "promotion": public.get("promotion"),
                "label": public.get("label"),
                "candidate_hash": public.get("optimizer", {}).get("candidate_hash"),
            },
            indent=2,
        ))
        return 0 if public.get("status") != "CONFIRMATION_VOID" else 2
    return 2


def _promote_dry() -> dict:
    """Synthetic records only. Demonstrates the gates without a model."""
    state = "GENERATED"
    state = transition(state, "SCREENING", source="search")
    try:
        transition(state, "SCREENING_PASSED", source="search", feasible=False)
        infeasible_blocked = False
    except Exception:
        infeasible_blocked = True
    passed = transition(state, "SCREENING_PASSED", source="search", feasible=True)
    validating = transition(passed, "VALIDATING", source="validation")
    try:
        transition(validating, "FINALIST", source="search")
        search_cannot_finalize = False
    except Exception:
        search_cannot_finalize = True
    finalist = transition(validating, "FINALIST", source="validation")
    frozen = {"candidate_id": "cand_y", "architecture_hash": "hash_y", "generation": 2, "state": finalist}
    frozen = freeze(frozen)
    try:
        transition("SCREENING", "CONFIRMED", source="search")
        search_confirmed = True
    except Exception:
        search_confirmed = False
    confirmed = transition(frozen["state"], "CONFIRMED", source="holdout")
    child = mutate_frozen(frozen, {"candidate_id": "ignored", "architecture_hash": "hash_z", "generation": 0})
    overfit = {
        "x_validation_rejected": True,
        "y_can_be_finalist": finalist == "FINALIST",
        "note": "Candidate X with a poor validation result stays VALIDATION_REJECTED and never reaches FINALIST.",
    }
    rejected = transition(validating, "VALIDATION_REJECTED", source="validation")
    return {
        "infeasible_blocked": infeasible_blocked,
        "search_cannot_finalize": search_cannot_finalize,
        "search_cannot_confirm": not search_confirmed,
        "confirmed_state": confirmed,
        "frozen_mutation_is_new": child["state"] == "GENERATED" and child["architecture_hash"] == "hash_z" and child["candidate_id"] != "cand_y",
        "validation_rejection": rejected,
        "overfit_rule": overfit,
    }


def _degeneracy(catalog) -> dict:
    candidate = zero_capability(catalog)
    metrics = {
        "coverage": 0.0,
        "attempted": 4,
        "oracle_successes": 0,
        "category_rates": {"read": 0.0, "write": 0.0},
        "usage_status": "MEASURED",
        "uncached_input_tokens": 0,
    }
    floors = {
        "min_coverage": 1.0,
        "min_semantic_success": 0.5,
        "category_floors": {"read": 0.5, "write": 0.5},
        "require_measured_usage": True,
    }
    verdict = classify_candidate(metrics, floors)
    row = {
        "candidate_id": candidate["candidate_id"],
        "candidate_hash": candidate["architecture_hash"],
        "feasible": verdict["feasible"],
        "uncached_tokens_per_success": per_success(0, 0),
        "total_tokens_per_success": per_success(0, 0),
        "wrong_tool_calls": 0,
        "tool_calls_per_success": per_success(0, 0),
        "wall_ms_per_success": per_success(0, 0),
    }
    frontier = pareto_frontier([row])
    report = render_comparison(
        [
            {
                **row,
                "semantic_success_rate": verdict["semantic_success_rate"],
                "false_successes": 0,
            }
        ],
        label="SMOKE_ONLY",
    )
    return {
        "status": verdict["status"],
        "reasons": verdict["reasons"],
        "on_pareto_frontier": row in frontier,
        "report_mentions_infeasible": "False" in report,
    }
