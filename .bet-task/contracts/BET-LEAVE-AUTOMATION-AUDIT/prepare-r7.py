from __future__ import annotations

import copy
import hashlib
import json
import sys
from pathlib import Path


ROOT = Path("/Users/deniz/Desktop/IK")
SKILL_ROOT = Path("/Users/deniz/.codex/skills/bet-task")
CONTRACT_DIR = ROOT / ".bet-task/contracts/BET-LEAVE-AUTOMATION-AUDIT"
SOURCE_PATH = CONTRACT_DIR / "source-r6.json"
OUTPUT_PATH = CONTRACT_DIR / "source-r7.json"
CONTRACT_PATH = CONTRACT_DIR / "contract.json"

sys.dont_write_bytecode = True
sys.path.insert(0, str(SKILL_ROOT / "scripts"))

from bet_contract import api as contract_api  # noqa: E402
from bet_contract.change_rules import live_change_state  # noqa: E402
from bet_contract.review_model import review_pass_digest  # noqa: E402


source = contract_api.loads_json(SOURCE_PATH.read_text(encoding="utf-8"))
contract = contract_api.loads_json(CONTRACT_PATH.read_text(encoding="utf-8"))
overrides = source["overrides"]

revision_path = CONTRACT_DIR / "revisions/6.json"
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
for authority in overrides["authority_map"]:
    authority_path = ROOT / authority["path"]
    if authority_path.is_file():
        authority["sha256"] = "sha256:" + hashlib.sha256(
            authority_path.read_bytes()
        ).hexdigest()
snapshot_candidate = copy.deepcopy(contract)
snapshot_candidate["scope"]["files"] = sorted(
    set(snapshot_candidate["scope"]["files"] + ["Services/AuditLogPageService.cs"])
)
_, _, live_snapshot, _, _ = live_change_state(snapshot_candidate)
source["change_control"]["current_snapshot_sha256"] = live_snapshot

overrides["scope"]["files"] = sorted(
    set(overrides["scope"]["files"] + ["Services/AuditLogPageService.cs"])
)
overrides["contract_revision"] = 7
overrides.setdefault("material_deltas", []).append(
    {
        "revision": 7,
        "change": (
            "Record the independent static review and remediate backend audit "
            "authorization, balance-update invariants, worker lifecycle evidence "
            "and the persisted entitlement discriminator constraint."
        ),
        "evidence": (
            "Reviewer /root/leave_automation_reviewer returned four P1 and one "
            "P2 finding at 7/10 against revision-6 attestation surface."
        ),
    }
)

review = copy.deepcopy(contract["review"])
review["lifecycle"] = copy.deepcopy(review["lifecycle"])
review["lifecycle"]["reviewer_identity"] = "/root/leave_automation_reviewer"
review["lifecycle"]["reviewer_model"] = "gpt-5.6-sol"
review["lifecycle"]["independence"] = "INDEPENDENT"
review["lifecycle"]["state"] = "REMEDIATION_REQUIRED"
review["lifecycle"]["evidence"] = (
    "Revision-6 FULL_INITIAL static review found four P1 and one P2 remediation items."
)
review["lifecycle"]["passes"] = copy.deepcopy(
    contract["review"]["lifecycle"].get("passes", [])
)
static_review_pass = {
        "id": "REVIEW-1",
        "kind": "STATIC_REVIEW",
        "packet_mode": "FULL_INITIAL",
        "contract_revision": 6,
        "reviewer_identity": "/root/leave_automation_reviewer",
        "reviewer_model": "gpt-5.6-sol",
        "independence": "INDEPENDENT",
        "policy_fingerprint": contract["policy"]["applicable_policy_fingerprint"],
        "scenario_evidence_sha256": (
            "sha256:83d9d9943740350e5d02eed1a5ca2fc09b159bfae2fc5a8ef7dd0109af0dcd71"
        ),
        "attestation_surface_sha256": (
            "sha256:5bd21a8d4f1ae7ec1ec00e161318b42b8067adafd4b90ac8dfa5c3452095be58"
        ),
        "score": 7,
        "open_findings": {"P0": 0, "P1": 4, "P2": 1, "P3": 0},
        "blocking_finding_ids": [
            "P1-AUDIT-BACKEND-AUTHORITY",
            "P1-BALANCE-UPDATE-INVARIANTS",
            "P1-FINAL-CLOSEOUT-PLANNED",
            "P1-WORKER-OPERATIONS-EVIDENCE",
            "P2-ENTITLEMENT-KIND-CONSTRAINT",
        ],
        "evidence": (
            "Independent reviewer found missing update eligibility/single-carry "
            "invariants, backend audit-read authorization, worker lifecycle tests, "
            "final closeout state and the MSSQL entitlement-kind constraint."
        ),
    }
static_review_pass["digest"] = review_pass_digest(static_review_pass)
review["lifecycle"]["passes"].append(static_review_pass)
overrides["review"] = review

overrides["validation"]["remediation"] = {
    "performed": True,
    "batches": [
        {
            "id": "REVIEW-REMEDIATION-R7",
            "revision": 7,
            "change": "Close all REVIEW-1 code, schema, test and final-evidence findings.",
            "changed_surfaces": [
                "Components/Pages/AuditLogs.razor",
                "Components/Pages/LeaveBalances.razor",
                "Database/001_create_human_resources_schema.sql",
                "Database/HumanResourcesDbContext.cs",
                "Migrations/20260731113043_AddAutomaticLeaveEntitlements.cs",
                "Migrations/20260731113043_AddAutomaticLeaveEntitlements.Designer.cs",
                "Migrations/HumanResourcesDbContextModelSnapshot.cs",
                "Services/AnnualLeaveEntitlementWorker.cs",
                "Services/AuditLogPageService.cs",
                "Services/LeaveBalanceService.cs",
                "Tests/AuditLogPageServiceTests.cs",
                "Tests/LeaveEntitlementServiceTests.cs",
            ],
            "invalidated_validation_ids": [
                "E2E-LEAVE-AUDIT",
                "FG-ALL-TESTS",
                "FG-BUILD",
                "FG-LEAVE-TESTS",
                "FG-MIGRATION",
            ],
            "rerun_validation_ids": [
                "E2E-LEAVE-AUDIT",
                "FG-ALL-TESTS",
                "FG-BUILD",
                "FG-LEAVE-TESTS",
                "FG-MIGRATION",
            ],
            "broad_rerun_required": True,
            "broad_rerun_validation_ids": ["E2E-LEAVE-AUDIT"],
            "evidence": (
                "Authorization, shared schema, worker orchestration and management "
                "UI changed; all fast gates and the combined real-runtime baseline "
                "must be rerun."
            ),
            "result": "PLANNED",
        }
    ],
}
source["checkpoint"] = {
    "phase": "IMPLEMENTATION",
    "completed_step_ids": ["DISCOVERY"],
    "pending_step_ids": ["IMPLEMENTATION", "VALIDATION", "REVIEW", "FINAL_CLOSEOUT"],
    "invalidated_validation_ids": [
        "E2E-LEAVE-AUDIT",
        "FG-ALL-TESTS",
        "FG-BUILD",
        "FG-LEAVE-TESTS",
        "FG-MIGRATION",
    ],
    "active_remediation_batch_id": "REVIEW-REMEDIATION-R7",
}

contract_api.atomic_write_json(OUTPUT_PATH, source, compact=False)
print(json.dumps({"result": "WRITTEN", "path": str(OUTPUT_PATH)}, ensure_ascii=False))
