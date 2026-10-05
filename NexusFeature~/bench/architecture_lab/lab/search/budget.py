"""Hard stop checks. A budget that is only printed does not protect a study."""

from __future__ import annotations

STOP_MAX_RUNS = "STOP_MAX_RUNS"
STOP_TOKEN_BUDGET = "STOP_TOKEN_BUDGET"
STOP_TOKEN_UNKNOWN = "STOP_TOKEN_UNKNOWN"
STOP_GENERATION_LIMIT = "STOP_GENERATION_LIMIT"
STOP_NO_NEW_CANDIDATES = "STOP_NO_NEW_CANDIDATES"
STOP_NO_FEASIBLE_PARENTS = "STOP_NO_FEASIBLE_PARENTS"

TERMINAL_STOPS = {
    STOP_GENERATION_LIMIT,
    STOP_NO_NEW_CANDIDATES,
    STOP_NO_FEASIBLE_PARENTS,
}


def spend_so_far(store, experiment_id: str) -> tuple[int, int | None]:
    """Count model runs already stored. Unknown token counts stay unknown.

    A missing uncached total is not zero. Callers must stop rather than
    schedule another model run on top of an unmeasured spend.
    """
    runs = 0
    tokens = 0
    for run_id in store.started_run_ids(experiment_id):
        record = store.load_record(run_id)
        if not record:
            continue
        runs += 1
        value = (record.get("usage") or {}).get("uncached_input_tokens")
        if isinstance(value, bool) or not isinstance(value, int):
            return runs, None
        tokens += value
    return runs, tokens


def next_run_allowed(*, runs_used: int, max_runs: int, uncached_used: int, max_uncached_tokens: int) -> str | None:
    """Return a stop code when the next model run must not start.

    The check uses runs and tokens already finished. It does not guess the
    cost of the run that has not happened yet.
    """
    if runs_used >= max_runs:
        return STOP_MAX_RUNS
    if uncached_used >= max_uncached_tokens:
        return STOP_TOKEN_BUDGET
    return None
