from __future__ import annotations

import copy
import json
import sys
from pathlib import Path


ROOT = Path("/Users/deniz/Desktop/IK")
SKILL_ROOT = Path("/Users/deniz/.codex/skills/bet-task")
CONTRACT_DIR = ROOT / ".bet-task/contracts/BET-LEAVE-AUTOMATION-AUDIT"
SOURCE_PATH = CONTRACT_DIR / "source-r2.json"
OUTPUT_PATH = CONTRACT_DIR / "source-r3.json"
CONTRACT_PATH = CONTRACT_DIR / "contract.json"

sys.dont_write_bytecode = True
sys.path.insert(0, str(SKILL_ROOT / "scripts"))

from bet_contract import api as contract_api  # noqa: E402
from bet_contract.decisions import enrich_decision  # noqa: E402


source = contract_api.loads_json(SOURCE_PATH.read_text(encoding="utf-8"))
contract = contract_api.loads_json(CONTRACT_PATH.read_text(encoding="utf-8"))
overrides = source["overrides"]

revision_path = CONTRACT_DIR / "revisions/2.json"
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

decision = {
    "id": "D-LEAVE-POLICY-DEFAULT-QUOTAS-R3",
    "kind": "PRODUCT_BEHAVIOR",
    "decision": (
        "The initial AnnualQuota for each of the five default leave types is "
        "30 days; administrators may change those persisted leave-type quotas later."
    ),
    "user_confirmed": True,
    "subject_ids": [
        "leave-policy.default-quotas",
        "leave-policy.quota-editability",
    ],
    "evidence": (
        "FOLLOWUP_USER_APPROVAL: günlerin hepsini 30 gir biz daha sonra değişiriz"
    ),
    "confirmation_source": "FOLLOWUP_USER_APPROVAL",
    "confirmation_artifact": None,
}
enrich_decision(decision, revision=3)
overrides["confirmed_decisions"] = [
    item
    for item in overrides["confirmed_decisions"]
    if item["id"] != decision["id"]
] + [decision]
overrides["contract_revision"] = 3
overrides["status"] = "BLOCKED"
overrides.setdefault("material_deltas", []).append(
    {
        "revision": 3,
        "change": (
            "Set all five default leave-type quotas to 30 days and preserve later "
            "database-backed quota editing; carry-over choices remain blocked."
        ),
        "evidence": decision["evidence"],
    }
)
overrides["invariants"] = [
    item
    for item in overrides["invariants"]
    if not item.startswith("Beş varsayılan izin türünün başlangıç kotası")
]
overrides["invariants"].append(
    "Beş varsayılan izin türünün başlangıç kotası 30 gündür; sonraki değişiklikler kalıcı LeaveTypes veri otoritesinden yapılır."
)

coverage = source["decision_control"].setdefault("documentation_coverage", [])
coverage.append(
    {
        "path": "Docs/leave-entitlement-policy.md",
        "subject_ids": [
            "leave-policy.default-quotas",
            "leave-policy.quota-editability",
        ],
        "result": "CREATE_REQUIRED",
        "sha256": "PLANNED",
        "evidence": (
            "The operator policy document will identify the initial 30-day values "
            "and the /LeaveTypes database-backed editing authority."
        ),
    }
)
source["checkpoint"] = {
    "phase": "DISCOVERY",
    "completed_step_ids": [],
    "pending_step_ids": [
        "DISCOVERY",
        "IMPLEMENTATION",
        "VALIDATION",
        "REVIEW",
        "FINAL_CLOSEOUT",
    ],
    "invalidated_validation_ids": [],
    "active_remediation_batch_id": None,
}

contract_api.atomic_write_json(OUTPUT_PATH, source, compact=False)
print(json.dumps({"result": "WRITTEN", "path": str(OUTPUT_PATH)}, ensure_ascii=False))
