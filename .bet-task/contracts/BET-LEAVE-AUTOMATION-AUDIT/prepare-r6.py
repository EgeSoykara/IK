from __future__ import annotations

import copy
import json
import sys
from pathlib import Path


ROOT = Path("/Users/deniz/Desktop/IK")
SKILL_ROOT = Path("/Users/deniz/.codex/skills/bet-task")
CONTRACT_DIR = ROOT / ".bet-task/contracts/BET-LEAVE-AUTOMATION-AUDIT"
SOURCE_PATH = CONTRACT_DIR / "source-r5.json"
OUTPUT_PATH = CONTRACT_DIR / "source-r6.json"
CONTRACT_PATH = CONTRACT_DIR / "contract.json"

sys.dont_write_bytecode = True
sys.path.insert(0, str(SKILL_ROOT / "scripts"))

from bet_contract import api as contract_api  # noqa: E402


source = contract_api.loads_json(SOURCE_PATH.read_text(encoding="utf-8"))
contract = contract_api.loads_json(CONTRACT_PATH.read_text(encoding="utf-8"))
overrides = source["overrides"]

revision_path = CONTRACT_DIR / "revisions/5.json"
revision_path.parent.mkdir(parents=True, exist_ok=True)
contract_api.atomic_write_json(revision_path, contract, compact=False)

base_binding = {
    "contract_id": contract["contract_id"],
    "revision": contract["contract_revision"],
    "contract_definition_sha256": contract["workflow_checkpoint"]["contract_definition_sha256"],
    "decision_ledger_sha256": contract["decision_control"]["decision_ledger_sha256"],
    "blocker_history_sha256": contract["decision_control"]["blocker_history_sha256"],
    "conflict_ledger_sha256": contract["decision_control"]["conflict_ledger_sha256"],
}
source["base_contract"] = base_binding
source["decision_control"]["base_contract"] = copy.deepcopy(base_binding)
source["decision_control"]["base_records"] = {
    "decisions": [
        {
            "id": item["id"],
            "status": item["status"],
            "record_sha256": item["record_sha256"],
            "superseded_by_decision_id": item["superseded_by_decision_id"],
        }
        for item in contract["confirmed_decisions"]
    ],
    "blockers": [
        {
            "id": item["id"],
            "status": item["status"],
            "record_sha256": item["record_sha256"],
            "resolution_decision_id": item["resolution_decision_id"],
            "resolved_revision": item["resolved_revision"],
        }
        for item in contract["blockers"]
    ],
    "conflicts": [
        {
            "id": item["id"],
            "status": item["status"],
            "record_sha256": item["record_sha256"],
        }
        for item in contract["decision_control"]["conflicts"]
    ],
}
overrides["authority_map"] = copy.deepcopy(contract["authority_map"])
source["change_control"]["current_snapshot_sha256"] = contract["change_control"][
    "current_snapshot_sha256"
]

for gate in overrides["validation"]["fast_gates"]:
    if gate["id"] in {"FG-LEAVE-TESTS", "FG-ALL-TESTS", "FG-BUILD"}:
        gate["command"] += " --disable-build-servers"
overrides["contract_revision"] = 6
overrides.setdefault("material_deltas", []).append(
    {
        "revision": 6,
        "change": (
            "Disable persistent dotnet build servers in test/build validation "
            "commands so each gate leaves no process-group residue."
        ),
        "evidence": (
            "FG-LEAVE-TESTS passed all 29 tests, but the receipt runner correctly "
            "rejected the gate because a dotnet build server remained alive."
        ),
    }
)
source["checkpoint"] = {
    "phase": "READY",
    "completed_step_ids": ["DISCOVERY"],
    "pending_step_ids": ["IMPLEMENTATION", "VALIDATION", "REVIEW", "FINAL_CLOSEOUT"],
    "invalidated_validation_ids": [
        "FG-LEAVE-TESTS",
        "FG-ALL-TESTS",
        "FG-BUILD",
    ],
    "active_remediation_batch_id": None,
}

contract_api.atomic_write_json(OUTPUT_PATH, source, compact=False)
print(json.dumps({"result": "WRITTEN", "path": str(OUTPUT_PATH)}, ensure_ascii=False))
