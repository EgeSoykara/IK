from __future__ import annotations

import copy
import json
import sys
from pathlib import Path


ROOT = Path("/Users/deniz/Desktop/IK")
SKILL_ROOT = Path("/Users/deniz/.codex/skills/bet-task")
CONTRACT_DIR = ROOT / ".bet-task/contracts/BET-LEAVE-AUTOMATION-AUDIT"
SOURCE_PATH = CONTRACT_DIR / "source.json"
OUTPUT_PATH = CONTRACT_DIR / "source-r2.json"
CONTRACT_PATH = CONTRACT_DIR / "contract.json"

sys.dont_write_bytecode = True
sys.path.insert(0, str(SKILL_ROOT / "scripts"))

from bet_contract import api as contract_api  # noqa: E402
from bet_contract.decisions import enrich_decision  # noqa: E402


source = contract_api.loads_json(SOURCE_PATH.read_text(encoding="utf-8"))
contract = contract_api.loads_json(CONTRACT_PATH.read_text(encoding="utf-8"))
overrides = source["overrides"]

revision_path = CONTRACT_DIR / "revisions/1.json"
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
    "id": "D-LEAVE-POLICY-30PLUS-MAX50-R2",
    "kind": "PRODUCT_BEHAVIOR",
    "decision": (
        "Employees with 30 or more completed service years continue to use the "
        "20-30-year employee leave tier, and the existing 50-day maximum-accrual "
        "warning applies to every default leave type."
    ),
    "user_confirmed": True,
    "subject_ids": [
        "leave-policy.30-plus-tier",
        "leave-policy.maximum-accrual-warning",
    ],
    "evidence": (
        "FOLLOWUP_USER_APPROVAL: 30 yıl ve üzerindeki çalışanlar 20–30 "
        "kademesinden devam etsin; Mevcut 50 günlük azami birikim uyarısı tüm türlerde korunsun"
    ),
    "confirmation_source": "FOLLOWUP_USER_APPROVAL",
    "confirmation_artifact": None,
}
enrich_decision(decision, revision=2)
overrides["confirmed_decisions"] = [
    item
    for item in overrides["confirmed_decisions"]
    if item["id"] != decision["id"]
] + [decision]
overrides["contract_revision"] = 2
overrides["status"] = "BLOCKED"
overrides["material_deltas"] = [
    {
        "revision": 2,
        "change": (
            "Confirm the 30+ service-year tier and retain the existing 50-day "
            "maximum-accrual warning for all default leave types; numeric annual "
            "quotas and carry-over choices remain blocked."
        ),
        "evidence": decision["evidence"],
    }
]

overrides["task"]["explicit_exclusions"] = [
    item
    for item in overrides["task"]["explicit_exclusions"]
    if "30 yıl üstü" not in item
]
overrides["task"]["explicit_exclusions"].append(
    "Kullanıcının vermediği yıllık izin kota değerlerini veya devir kurallarını tahmin etmek"
)
overrides["invariants"] = [
    item
    for item in overrides["invariants"]
    if not item.startswith("30 yıl ve üzeri")
    and not item.startswith("Tüm varsayılan izin türleri")
]
overrides["invariants"].extend(
    [
        "30 yıl ve üzeri çalışanlar 20-30 yıllık çalışan izin kademesini kullanmaya devam eder.",
        "Tüm varsayılan izin türleri mevcut 50 günlük azami birikim uyarısını korur; bu uyarı hard cap değildir.",
    ]
)

coverage = source["decision_control"].setdefault("documentation_coverage", [])
coverage = [
    item
    for item in coverage
    if not set(item.get("subject_ids", []))
    & {
        "leave-policy.30-plus-tier",
        "leave-policy.maximum-accrual-warning",
    }
]
coverage.append(
    {
        "path": "Docs/leave-entitlement-policy.md",
        "subject_ids": [
            "leave-policy.30-plus-tier",
            "leave-policy.maximum-accrual-warning",
        ],
        "result": "CREATE_REQUIRED",
        "sha256": "PLANNED",
        "evidence": (
            "The new canonical operator policy document will record the confirmed "
            "30+ tier and 50-day warning together with the still-pending quotas."
        ),
    }
)
source["decision_control"]["documentation_coverage"] = coverage
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
