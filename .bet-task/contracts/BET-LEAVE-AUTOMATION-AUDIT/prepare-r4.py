from __future__ import annotations

import copy
import json
import sys
from pathlib import Path


ROOT = Path("/Users/deniz/Desktop/IK")
SKILL_ROOT = Path("/Users/deniz/.codex/skills/bet-task")
CONTRACT_DIR = ROOT / ".bet-task/contracts/BET-LEAVE-AUTOMATION-AUDIT"
SOURCE_PATH = CONTRACT_DIR / "source-r3.json"
OUTPUT_PATH = CONTRACT_DIR / "source-r4.json"
CONTRACT_PATH = CONTRACT_DIR / "contract.json"

sys.dont_write_bytecode = True
sys.path.insert(0, str(SKILL_ROOT / "scripts"))

from bet_contract import api as contract_api  # noqa: E402
from bet_contract.decisions import enrich_decision  # noqa: E402


source = contract_api.loads_json(SOURCE_PATH.read_text(encoding="utf-8"))
contract = contract_api.loads_json(CONTRACT_PATH.read_text(encoding="utf-8"))
overrides = source["overrides"]

revision_path = CONTRACT_DIR / "revisions/3.json"
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
    "id": "D-LEAVE-POLICY-CARRYOVER-R4",
    "kind": "PRODUCT_BEHAVIOR",
    "decision": (
        "The 0-10-year, 10-20-year and 20-30-plus-year employee leave types "
        "carry remaining days into the next year; sickness and pregnancy leave "
        "types do not carry remaining days into the next year."
    ),
    "user_confirmed": True,
    "subject_ids": ["leave-policy.carry-over"],
    "evidence": "FOLLOWUP_USER_APPROVAL: olur",
    "confirmation_source": "FOLLOWUP_USER_APPROVAL",
    "confirmation_artifact": None,
}
enrich_decision(decision, revision=4)
overrides["confirmed_decisions"] = [
    item
    for item in overrides["confirmed_decisions"]
    if item["id"] != decision["id"]
] + [decision]
overrides["blockers"] = [
    {
        **item,
        "status": "RESOLVED",
        "resolved_revision": 4,
        "resolution_decision_id": decision["id"],
    }
    if item["id"] == "B-LEAVE-POLICY-NUMBERS"
    else item
    for item in overrides["blockers"]
]
overrides["contract_revision"] = 4
overrides["status"] = "READY"
overrides.setdefault("material_deltas", []).append(
    {
        "revision": 4,
        "change": (
            "Resolve the final leave-policy blocker: service-tier annual leave "
            "carries over, while sickness and pregnancy leave do not; authorize implementation."
        ),
        "evidence": decision["evidence"],
    }
)
overrides["task"]["explicit_exclusions"] = [
    item
    for item in overrides["task"]["explicit_exclusions"]
    if "yıllık izin kota değerlerini" not in item
]
overrides["invariants"] = [
    item
    for item in overrides["invariants"]
    if not item.startswith("Kıdem kademesi yıllık izinleri devreder")
]
overrides["invariants"].append(
    "Kıdem kademesi yıllık izinleri devreder; hastalık ve hamilelik izinleri devretmez."
)

coverage = source["decision_control"].setdefault("documentation_coverage", [])
coverage.append(
    {
        "path": "Docs/leave-entitlement-policy.md",
        "subject_ids": ["leave-policy.carry-over"],
        "result": "CREATE_REQUIRED",
        "sha256": "PLANNED",
        "evidence": (
            "The canonical operator policy will record which default leave types "
            "carry remaining balances."
        ),
    }
)

for gate in overrides["validation"]["fast_gates"]:
    if "Sayısal politika kararı" in gate.get("evidence", ""):
        gate["evidence"] = "Implementation sonrası uygulanacak."
for scenario in overrides["validation"]["comprehensive_scenarios"]:
    if "Sayısal politika kararı" in scenario.get("evidence", ""):
        scenario["evidence"] = "Implementation sonrası uygulanacak."
for acceptance in overrides["outcome_closure"]["acceptance"]:
    if "Sayısal izin politikası" in acceptance.get("evidence", ""):
        acceptance["evidence"] = "Implementation sonrası uygulanacak."
overrides["outcome_closure"]["evidence"] = (
    "Discovery graph/code/docs evidence and all numeric/carry-over product "
    "decisions are confirmed; implementation is authorized."
)
for check in overrides["security_closeout"]["checks"]:
    check["evidence"] = "Implementation ve validation sonrası kapatılacak."
overrides["entity_mapping"]["verification"]["evidence"] = (
    "Confirmed leave policy authorizes mapping implementation and migration verification."
)

source["checkpoint"] = {
    "phase": "READY",
    "completed_step_ids": ["DISCOVERY"],
    "pending_step_ids": ["IMPLEMENTATION", "VALIDATION", "REVIEW", "FINAL_CLOSEOUT"],
    "invalidated_validation_ids": [],
    "active_remediation_batch_id": None,
}

contract_api.atomic_write_json(OUTPUT_PATH, source, compact=False)
print(json.dumps({"result": "WRITTEN", "path": str(OUTPUT_PATH)}, ensure_ascii=False))
