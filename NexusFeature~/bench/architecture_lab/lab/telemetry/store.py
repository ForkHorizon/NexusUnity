"""SQLite run database. Markdown reports are generated from this, not the reverse."""

from __future__ import annotations

import json
import sqlite3
from pathlib import Path
from typing import Any

from lab.telemetry.redact import redact

SCHEMA = """
CREATE TABLE IF NOT EXISTS experiments (
    experiment_id TEXT PRIMARY KEY,
    label TEXT NOT NULL,
    identity_json TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS candidates (
    architecture_hash TEXT PRIMARY KEY,
    candidate_id TEXT NOT NULL,
    parent_hash TEXT,
    generation INTEGER NOT NULL,
    state TEXT NOT NULL,
    manifest_json TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS runs (
    run_id TEXT PRIMARY KEY,
    experiment_id TEXT NOT NULL,
    candidate_hash TEXT NOT NULL,
    task_id TEXT NOT NULL,
    split TEXT NOT NULL,
    seed INTEGER NOT NULL,
    status TEXT NOT NULL,
    oracle_success INTEGER,
    model_claimed_success INTEGER,
    false_success INTEGER,
    usage_json TEXT,
    wall_ms INTEGER,
    void_reason TEXT,
    label TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS tool_calls (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    run_id TEXT NOT NULL,
    ts TEXT,
    exposed_name TEXT,
    capability_id TEXT,
    backend_command TEXT,
    arguments_json TEXT,
    result_bytes INTEGER,
    visible_bytes INTEGER,
    duration_ms INTEGER,
    ok INTEGER,
    retry INTEGER,
    error TEXT
);
CREATE TABLE IF NOT EXISTS events (
    run_id TEXT NOT NULL,
    seq INTEGER NOT NULL,
    event_json TEXT NOT NULL,
    PRIMARY KEY (run_id, seq)
);
"""


class RunStore:
    def __init__(self, path: Path):
        self.path = path
        self.path.parent.mkdir(parents=True, exist_ok=True)
        self._db = sqlite3.connect(path)
        self._db.executescript(SCHEMA)

    def close(self) -> None:
        self._db.close()

    def add_experiment(self, experiment_id: str, label: str, identity: dict[str, Any]) -> None:
        self._db.execute(
            "INSERT OR REPLACE INTO experiments VALUES (?, ?, ?)",
            (experiment_id, label, json.dumps(redact(identity), sort_keys=True)),
        )
        self._db.commit()

    def add_candidate(self, candidate: dict[str, Any], state: str = "GENERATED") -> None:
        self._db.execute(
            "INSERT OR REPLACE INTO candidates VALUES (?, ?, ?, ?, ?, ?)",
            (
                candidate["architecture_hash"],
                candidate["candidate_id"],
                candidate.get("parent_hash"),
                int(candidate.get("generation") or 0),
                state,
                json.dumps(redact(candidate), sort_keys=True),
            ),
        )
        self._db.commit()

    def add_run(self, row: dict[str, Any]) -> None:
        self._db.execute(
            """INSERT OR REPLACE INTO runs VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)""",
            (
                row["run_id"],
                row["experiment_id"],
                row["candidate_hash"],
                row["task_id"],
                row["split"],
                row["seed"],
                row["status"],
                _bit(row.get("oracle_success")),
                _bit(row.get("model_claimed_success")),
                _bit(row.get("false_success")),
                json.dumps(redact(row.get("usage") or {})),
                row.get("wall_ms"),
                row.get("void_reason"),
                row["label"],
            ),
        )
        for seq, event in enumerate(row.get("events") or []):
            self._db.execute(
                "INSERT OR REPLACE INTO events VALUES (?, ?, ?)",
                (row["run_id"], seq, json.dumps(redact(event), sort_keys=True)),
            )
        for call in row.get("tool_calls") or []:
            self._db.execute(
                """INSERT INTO tool_calls (
                    run_id, ts, exposed_name, capability_id, backend_command,
                    arguments_json, result_bytes, visible_bytes, duration_ms, ok, retry, error
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)""",
                (
                    row["run_id"],
                    call.get("ts"),
                    call.get("exposed_name"),
                    call.get("capability_id"),
                    call.get("backend_command"),
                    json.dumps(redact(call.get("arguments") or {})),
                    call.get("result_bytes"),
                    call.get("visible_bytes"),
                    call.get("duration_ms"),
                    _bit(call.get("ok")),
                    _bit(call.get("retry")),
                    call.get("error"),
                ),
            )
        self._db.commit()

    def runs(self) -> list[dict[str, Any]]:
        cursor = self._db.execute(
            """SELECT run_id, experiment_id, candidate_hash, task_id, split, seed, status,
                      oracle_success, model_claimed_success, false_success, usage_json,
                      wall_ms, void_reason, label FROM runs ORDER BY run_id"""
        )
        columns = [item[0] for item in cursor.description]
        return [dict(zip(columns, row)) for row in cursor.fetchall()]

    def event_count(self, run_id: str) -> int:
        row = self._db.execute("SELECT COUNT(*) FROM events WHERE run_id = ?", (run_id,)).fetchone()
        return int(row[0])

    def tool_calls(self, run_id: str) -> list[dict[str, Any]]:
        cursor = self._db.execute(
            "SELECT exposed_name, capability_id, backend_command, ok, retry, duration_ms FROM tool_calls WHERE run_id = ? ORDER BY id",
            (run_id,),
        )
        columns = [item[0] for item in cursor.description]
        return [dict(zip(columns, row)) for row in cursor.fetchall()]


def _bit(value: Any) -> int | None:
    if value is None:
        return None
    return 1 if value else 0
