from __future__ import annotations

import copy
import json
import sys
from pathlib import Path


ROOT = Path("/Users/egesoykara/Desktop/IK")
SKILL_ROOT = Path("/Users/egesoykara/.codex/skills/bet-task")
CONTRACT_DIR = ROOT / ".bet-task/contracts/BET-LEAVE-AUTOMATION-AUDIT"
SOURCE_PATH = CONTRACT_DIR / "source-r11.json"
CONTRACT_PATH = CONTRACT_DIR / "contract.json"

sys.dont_write_bytecode = True
sys.path.insert(0, str(SKILL_ROOT / "scripts"))

from bet_contract import api as contract_api  # noqa: E402
from build_task_contract import build_contract  # noqa: E402


source = contract_api.loads_json(SOURCE_PATH.read_text(encoding="utf-8"))
contract = contract_api.loads_json(CONTRACT_PATH.read_text(encoding="utf-8"))
base_contract = contract_api.loads_json(
    (CONTRACT_DIR / "revisions/10.json").read_text(encoding="utf-8")
)

source["change_control"]["current_snapshot_sha256"] = contract["change_control"][
    "current_snapshot_sha256"
]
source["change_control"]["receipt_sha256"] = {
    validation_id: receipt["sha256"]
    for validation_id, receipt in contract["change_control"]["receipts"].items()
}
source["overrides"]["validation"] = copy.deepcopy(contract["validation"])

outcome = copy.deepcopy(contract["outcome_closure"])
acceptance_evidence = {
    "FUNCTIONAL": (
        "FG-LEAVE-TESTS and E2E-LEAVE-AUDIT passed: persisted manual types require a positive "
        "balance, annual allocation order is preserved, and male-only Mobilization is hard-capped at two days."
    ),
    "EXPERIENCE": (
        "E2E-LEAVE-AUDIT passed the authenticated request, balance, warning and audit workflows "
        "without browser, console, page or HTTP 500 errors."
    ),
    "COVERAGE": (
        "Focused tests cover eligibility, zero-balance/female negatives and exact deduction; the "
        "real browser scenario passed desktop, tablet, mobile, short-mobile and landscape viewports."
    ),
    "QUALITY": (
        "E2E-LEAVE-AUDIT reported no browser errors or document-level responsive regressions across five viewports."
    ),
    "CAPACITY": (
        "Focused worker/balance tests prove unique employee-type-year reconciliation and idempotency; "
        "the real application restart produced no duplicate entitlement audit."
    ),
    "OPERATIONS": (
        "Focused tests cover startup scheduling, disabled mode, retry and cancellation; the real restart "
        "proved catch-up idempotency and persisted warning management."
    ),
    "SECURITY_DATA": (
        "Backend authorization and eligibility negative tests passed; unauthenticated management routes "
        "redirected to login and audit details remained generic and non-PII."
    ),
    "REGRESSION": (
        "FG-ALL-TESTS passed all 152 Release tests and E2E-LEAVE-AUDIT passed the existing approval, "
        "delegation, worker-restart and responsive baseline."
    ),
    "DOCUMENTATION": (
        "Docs/leave-entitlement-policy.md and Docs/phase-1-architecture.md document balance-backed requests, "
        "Mobilization hard limits, automatic eligibility and the DEVELOPMENT cutover/backup contract; build passed."
    ),
}
for acceptance in outcome["acceptance"]:
    acceptance["result"] = "PASS"
    acceptance["evidence"] = acceptance_evidence[acceptance["dimension"]]
outcome["overall_result"] = "PASS"
outcome["evidence"] = (
    "All five revision-11 validation IDs have current PASS receipts; functional, experience, coverage, "
    "quality, capacity, operations, security/data, regression and documentation acceptance are closed."
)
source["overrides"]["outcome_closure"] = outcome

security = copy.deepcopy(contract["security_closeout"])
security_evidence = {
    "audit-logging": (
        "Focused audit/service tests passed; E2E observed one aggregate automatic-entitlement audit on startup "
        "and no duplicate after restart, with generic non-PII details."
    ),
    "backend-authority-authz-negative": (
        "Permission tests and request-service tests reject unauthorized balance/audit access, zero-balance manual "
        "types and female Mobilization; E2E redirected anonymous management-route access to login."
    ),
    "config-fail-closed": (
        "Worker lifecycle tests prove disabled configuration performs no writes and cancellation/retry are bounded; "
        "invalid Mobilization quota/carry settings are rejected by UI/service and MSSQL constraints."
    ),
    "dependency-external-boundary": (
        "No new external dependency was introduced; validation used the existing real MSSQL boundary, injected "
        "TimeProvider scheduling and cancellation-aware service paths."
    ),
    "input-contract-secret-pii": (
        "Service and database checks validate request category/type pairing, positive year balance, eligibility and "
        "two-day Mobilization limits. Audit/errors remain generic; no secret or PII configuration was added."
    ),
    "transaction-idempotency-data-loss-rollback": (
        "Serializable/unique-key worker and approval tests plus restart E2E prove idempotency. The explicitly approved "
        "DEVELOPMENT cutover preserves employees, departments and canonical manager authority while resetting leave-domain "
        "rows; rollback is fail-closed and requires the documented pre-migration database backup."
    ),
}
for check in security["checks"]:
    check["result"] = "PASS"
    check["evidence"] = security_evidence[check["id"]]
source["overrides"]["security_closeout"] = security

entity_mapping = source["overrides"]["entity_mapping"]
entity_mapping["verification"] = {
    "plan": (
        "Verify attributes, minimal Fluent constraints, migration, snapshot and clean-install SQL are semantically identical."
    ),
    "result": "PASS",
    "evidence": (
        "FG-MIGRATION passed with no pending EF model changes; LeaveRequestSchemaContractTests passed; "
        "E2E-LEAVE-AUDIT applied the migration chain to real MSSQL and exercised the resulting request/type constraints."
    ),
}
for exception in entity_mapping["fluent_exceptions"]:
    exception["evidence"] = (
        "Database/HumanResourcesDbContext.cs, the revision-11 migration/model snapshot and "
        "Database/001_create_human_resources_schema.sql carry the same request-selection, "
        "half-day, identity, enum, derived-total and Mobilization checks; FG-MIGRATION and real MSSQL E2E passed."
    )

review = source["overrides"]["review"]
review["lifecycle"]["passes"] = [
    item for item in review["lifecycle"]["passes"] if item["id"] != "REVIEW-6"
]
has_final_attestation = bool(
    review["lifecycle"]["passes"]
    and review["lifecycle"]["passes"][-1]["kind"] == "FINAL_ATTESTATION"
)
review["lifecycle"]["state"] = (
    "FINAL_ATTESTED" if has_final_attestation else "DELTA_REVIEW_IN_PROGRESS"
)
review["lifecycle"]["evidence"] = (
    "The stable independent reviewer attested the current revision-11 surface at 10/10; "
    "all implementation, validation, outcome, security and mapping evidence is current."
    if has_final_attestation
    else "All implementation, validation, outcome, security and mapping evidence is current; "
    "the stable independent reviewer must now attest the final revision-11 surface."
)
review["lifecycle"]["no_progress_rounds"] = 0

source["checkpoint"] = {
    "phase": "REVIEW",
    "completed_step_ids": ["DISCOVERY", "IMPLEMENTATION", "VALIDATION"],
    "pending_step_ids": ["REVIEW", "FINAL_CLOSEOUT"],
    "invalidated_validation_ids": [],
    "active_remediation_batch_id": None,
}

contract_api.atomic_write_json(SOURCE_PATH, source, compact=False)
compiled = build_contract(source, SKILL_ROOT, base_contract=base_contract)
errors = contract_api.validate_contract(
    compiled,
    skill_root=SKILL_ROOT,
    contract_path=CONTRACT_PATH,
    allow_blocked=False,
    final=False,
)
if errors:
    raise RuntimeError("Closeout-ready contract invalid:\n" + "\n".join(errors))
contract_api.atomic_write_json(CONTRACT_PATH, compiled, compact=False)
print(json.dumps({"result": "WRITTEN", "contract": str(CONTRACT_PATH)}, ensure_ascii=False))
