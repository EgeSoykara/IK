from __future__ import annotations

import copy
import hashlib
import json
import sys
from pathlib import Path


ROOT = Path("/Users/deniz/Desktop/IK")
SKILL_ROOT = Path("/Users/deniz/.codex/skills/bet-task")
CONTRACT_DIR = ROOT / ".bet-task/contracts/BET-LEAVE-AUTOMATION-AUDIT"
SOURCE_PATH = CONTRACT_DIR / "source-r7.json"
OUTPUT_PATH = CONTRACT_DIR / "source-r8.json"
CONTRACT_PATH = CONTRACT_DIR / "contract.json"

sys.dont_write_bytecode = True
sys.path.insert(0, str(SKILL_ROOT / "scripts"))

from bet_contract import api as contract_api  # noqa: E402


source = contract_api.loads_json(SOURCE_PATH.read_text(encoding="utf-8"))
contract = contract_api.loads_json(CONTRACT_PATH.read_text(encoding="utf-8"))
overrides = source["overrides"]

revision_path = CONTRACT_DIR / "revisions/7.json"
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
source["change_control"]["receipt_sha256"] = {
    validation_id: receipt["sha256"]
    for validation_id, receipt in contract["change_control"]["receipts"].items()
}
overrides["validation"] = copy.deepcopy(contract["validation"])
overrides["contract_revision"] = 8
overrides.setdefault("material_deltas", []).append(
    {
        "revision": 8,
        "change": (
            "Bind current PASS receipts to all outcome, security, entity-mapping "
            "and canonical-documentation closeout records."
        ),
        "evidence": (
            "Revision-7 focused/full/build/migration/real-runtime E2E receipts are "
            "current after all REVIEW-1 remediations."
        ),
    }
)

outcome = copy.deepcopy(contract["outcome_closure"])
outcome_evidence = {
    "A-FUNCTIONAL": (
        "FG-LEAVE-TESTS proves eligibility, proration, carry-over, authorization "
        "and worker lifecycle; E2E-LEAVE-AUDIT proves real UI/worker/persistence."
    ),
    "A-EXPERIENCE": (
        "E2E-LEAVE-AUDIT and the 2026-07-31 UI checklist evidence prove the "
        "operator-focused audit table and person/department/all assignment flow."
    ),
    "A-COVERAGE": (
        "Focused tests cover 0/10/20/30+ boundaries, transition months, gender, "
        "missing dates and bulk scope; E2E covers five responsive viewports."
    ),
    "A-QUALITY": (
        "Real Chrome reported Turkish month text, no document overflow, no "
        "page/console/HTTP500 errors and no raw zero placeholder."
    ),
    "A-CAPACITY": (
        "Serializable worker/bulk transactions processed the disposable fixture's "
        "active employee matrix; restart and repeated assignment produced no duplicates."
    ),
    "A-OPERATIONS": (
        "Worker tests prove disabled configuration, cancellation, transient retry "
        "and exact next January 1 schedule; E2E proves startup catch-up/restart idempotency."
    ),
    "A-SECURITY": (
        "Focused negative tests reject unauthorized audit reads and balance writes; "
        "targets are resolved server-side and aggregate audits omit employee PII."
    ),
    "A-REGRESSION": (
        "FG-ALL-TESTS and the combined E2E pass after remediation; existing workday, "
        "public-holiday, approval and delegation authorities remain green."
    ),
    "A-DOCS": (
        "Docs/leave-entitlement-policy.md, Docs/phase-1-architecture.md, README.md "
        "and the UI checklist document the policy/editing/worker/operator authorities."
    ),
}
for acceptance in outcome["acceptance"]:
    acceptance["result"] = "PASS"
    acceptance["evidence"] = outcome_evidence[acceptance["id"]]
outcome["overall_result"] = "PASS"
outcome["evidence"] = (
    "All nine required dimensions have current focused, full-suite, migration, "
    "real-runtime browser or canonical-documentation evidence."
)
overrides["outcome_closure"] = outcome

security = copy.deepcopy(contract["security_closeout"])
security_evidence = {
    "audit-logging": (
        "Worker and bulk writes emit one actor-bound aggregate audit without employee "
        "names or leave reasons; audit reading is permission-checked in AuditLogPageService."
    ),
    "backend-authority-authz-negative": (
        "LeaveBalanceService and AuditLogPageService both enforce PageAccessService "
        "permissions; focused negative tests and anonymous E2E redirects pass."
    ),
    "config-fail-closed": (
        "Program binds options with ValidateDataAnnotations/ValidateOnStart; the "
        "disabled worker test proves zero assignment calls and invalid retry values fail startup."
    ),
    "dependency-external-boundary": (
        "No external dependency was added. TimeProvider delays propagate cancellation "
        "and bounded retry; disposable MSSQL and local Chrome E2E isolate failures."
    ),
    "input-contract-secret-pii": (
        "Scope/year/type/day inputs are validated server-side, entitlement eligibility "
        "is recalculated from persisted employee data, and audit/browser evidence leaks no secrets or PII."
    ),
    "transaction-idempotency-data-loss-rollback": (
        "Serializable transactions, the unique employee/type/year index, rowversion "
        "updates, single-tier carry invariant and restart/idempotency tests prevent partial or duplicate writes."
    ),
}
for check in security["checks"]:
    check["result"] = "PASS"
    check["evidence"] = security_evidence[check["id"]]
overrides["security_closeout"] = security

overrides["entity_mapping"] = {
    "touched": True,
    "attribute_first": True,
    "semantic_duplication": False,
    "evidence": (
        "LeaveType scalar validation and LeaveBalance unique/precision/concurrency "
        "mapping remain attribute-first; only the enum database range requires Fluent API."
    ),
    "fluent_exceptions": [
        {
            "mapping": "LeaveType EntitlementKind persisted enum range check",
            "attribute_support": "NOT_EXPRESSIBLE",
            "limitation": (
                "DataAnnotations Range validates models but does not generate an "
                "EF Core SQL Server check constraint."
            ),
            "minimal_fluent_scope": (
                "ToTable HasCheckConstraint for EntitlementKind BETWEEN 0 AND 5 only"
            ),
            "evidence": (
                "HumanResourcesDbContext, migration, designer, snapshot and clean-install "
                "SQL contain the same CK_LeaveTypes_EntitlementKind expression."
            ),
        }
    ],
    "verification": {
        "plan": "Verify attribute-first mapping, minimal check constraint, migration and snapshot.",
        "result": "PASS",
        "evidence": (
            "FG-MIGRATION reports no pending model changes; FG-BUILD and the real "
            "MSSQL E2E migration path pass with the synchronized constraint."
        ),
    },
}

policy_doc = ROOT / "Docs/leave-entitlement-policy.md"
policy_doc_sha = "sha256:" + hashlib.sha256(policy_doc.read_bytes()).hexdigest()
source["decision_control"]["documentation_coverage"] = [
    {
        "path": "Docs/leave-entitlement-policy.md",
        "subject_ids": item["subject_ids"],
        "result": "CONSISTENT",
        "sha256": policy_doc_sha,
        "evidence": (
            "Canonical operator policy records the confirmed default quota, 30+ tier, "
            "50-day warning, carry-over rules and database-backed /LeaveTypes edit authority."
        ),
    }
    for item in contract["decision_control"]["documentation_coverage"]
]

review = copy.deepcopy(contract["review"])
review["lifecycle"]["state"] = "DELTA_REVIEW_IN_PROGRESS"
review["lifecycle"]["evidence"] = (
    "All REVIEW-1 remediations and final closeout evidence are current; same reviewer "
    "must inspect the revision-8 DELTA_ONLY packet."
)
overrides["review"] = review
source["checkpoint"] = {
    "phase": "REVIEW",
    "completed_step_ids": ["DISCOVERY", "IMPLEMENTATION", "VALIDATION"],
    "pending_step_ids": ["REVIEW", "FINAL_CLOSEOUT"],
    "invalidated_validation_ids": [],
    "active_remediation_batch_id": None,
}

contract_api.atomic_write_json(OUTPUT_PATH, source, compact=False)
print(json.dumps({"result": "WRITTEN", "path": str(OUTPUT_PATH)}, ensure_ascii=False))
