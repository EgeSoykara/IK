from __future__ import annotations

import copy
import hashlib
import json
import subprocess
import sys
from pathlib import Path


ROOT = Path("/Users/deniz/Desktop/IK")
SKILL_ROOT = Path("/Users/deniz/.codex/skills/bet-task")
CONTRACT_DIR = ROOT / ".bet-task/contracts/BET-LEAVE-AUTOMATION-AUDIT"
SOURCE_PATH = CONTRACT_DIR / "source-r9.json"
OUTPUT_PATH = CONTRACT_DIR / "source-r10.json"
CONTRACT_PATH = CONTRACT_DIR / "contract.json"

sys.dont_write_bytecode = True
sys.path.insert(0, str(SKILL_ROOT / "scripts"))

from bet_contract import api as contract_api  # noqa: E402
from bet_contract.change_rules import live_change_state  # noqa: E402
from build_task_contract import build_contract  # noqa: E402


source = contract_api.loads_json(SOURCE_PATH.read_text(encoding="utf-8"))
contract = contract_api.loads_json(CONTRACT_PATH.read_text(encoding="utf-8"))
overrides = source["overrides"]

if contract["contract_revision"] == 9:
    contract_api.atomic_write_json(CONTRACT_DIR / "revisions/9.json", contract, compact=False)
else:
    contract = contract_api.loads_json(
        (CONTRACT_DIR / "revisions/9.json").read_text(encoding="utf-8")
    )

base_binding = {
    "contract_id": contract["contract_id"],
    "revision": contract["contract_revision"],
    "contract_definition_sha256": contract["workflow_checkpoint"][
        "contract_definition_sha256"
    ],
    "decision_ledger_sha256": contract["decision_control"][
        "decision_ledger_sha256"
    ],
    "blocker_history_sha256": contract["decision_control"][
        "blocker_history_sha256"
    ],
    "conflict_ledger_sha256": contract["decision_control"][
        "conflict_ledger_sha256"
    ],
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

overrides["contract_revision"] = 10
overrides["status"] = "READY"
overrides["repository"]["git_head"] = subprocess.check_output(
    ["git", "rev-parse", "HEAD"], cwd=ROOT, text=True
).strip()

overrides["delivery"]["cutover"]["data_reset"] = {
    "planned": True,
    "scope": (
        "Legacy leave requests, approvals, balances, leave-driven manager delegations, "
        "and their leave-domain audit rows only"
    ),
    "target_environment": "NON_PRODUCTION",
    "environment_evidence": (
        "D-PHASE-DEVELOPMENT-LEAVE-R1 selects DEVELOPMENT; BetTask phase policy "
        "requires clean removal and forbids data-preservation conversion/backfill."
    ),
    "related_data_handling": (
        "Employees and departments remain; ActiveDelegateEmployeeId is cleared before "
        "leave-driven delegation rows are deleted. Rollback is restore of the pre-migration backup."
    ),
}
overrides["delivery"]["cutover"]["compatibility_evidence"] = (
    "CURRENT_USER_REQUEST: aşama: DEVELOPMENT; the migration performs a direct clean reset "
    "of obsolete leave-domain state and contains no converter, backfill, dual path or fallback."
)

scope_files = {
    "BetSolution/Docs/UX/ui-ux-regression-checklist.md",
    "Components/Layout/NavMenu.razor",
    "Components/Pages/EmployeePersonnelInformation.razor",
    "Components/Pages/Home.razor",
    "Components/Pages/LeaveApprovals.razor",
    "Components/Pages/LeaveBalances.razor",
    "Components/Pages/LeaveCarryOverWarnings.razor",
    "Components/Pages/LeaveRequests.razor",
    "Components/Pages/LeaveTracking.razor",
    "Components/Pages/LeaveTypes.razor",
    "Database/001_create_human_resources_schema.sql",
    "Database/HumanResourcesDbContext.cs",
    "Docs/leave-entitlement-policy.md",
    "Docs/archive/leave-entitlement-policy-r8.md",
    "Docs/phase-1-architecture.md",
    "Migrations/20260803072742_DailyLeaveEntitlementsAndCategories.Designer.cs",
    "Migrations/20260803072742_DailyLeaveEntitlementsAndCategories.cs",
    "Migrations/HumanResourcesDbContextModelSnapshot.cs",
    "Models/LeaveBalance.cs",
    "Models/LeaveCarryOverWarning.cs",
    "Models/LeaveDayAmountAttribute.cs",
    "Models/LeaveRequest.cs",
    "Models/LeaveRequestBalanceAllocation.cs",
    "Models/LeaveRequestCategory.cs",
    "Models/LeaveType.cs",
    "Program.cs",
    "README.md",
    "Services/DailyLeaveEntitlementWorker.cs",
    "Services/DailyLeaveEntitlementWorkerOptions.cs",
    "Services/LeaveApprovalFilterOptionQuery.cs",
    "Services/LeaveBalanceService.cs",
    "Services/LeaveCarryOverWarningService.cs",
    "Services/LeaveEntitlementService.cs",
    "Services/LeaveRequestService.cs",
    "Services/LeaveTrackingService.cs",
    "Tests/DepartmentManagerAndDelegationTests.cs",
    "Tests/LeaveApprovalVisibilityQueryTests.cs",
    "Tests/LeaveCarryOverWarningServiceTests.cs",
    "Tests/LeaveEntitlementServiceTests.cs",
    "Tests/LeaveRequestServiceTests.cs",
    "Tests/LeaveTrackingTests.cs",
    "Tests/ManagementUiContractTests.cs",
    "Tests/PersonnelInformationUiContractTests.cs",
}
missing = sorted(path for path in scope_files if not (ROOT / path).is_file())
if missing:
    raise RuntimeError(f"Revision-10 scope contains missing files: {missing}")
overrides["scope"]["files"] = sorted(scope_files)
overrides["scope"]["docs"] = [
    "BetSolution/Docs/UX/ui-ux-regression-checklist.md",
    "Docs/archive/leave-entitlement-policy-r8.md",
    "Docs/leave-entitlement-policy.md",
    "Docs/phase-1-architecture.md",
    "README.md",
]
overrides["scope"]["surfaces"] = [
    "/AuditLogs",
    "/EmployeePersonnelInformation",
    "/LeaveApprovals",
    "/LeaveBalances",
    "/LeaveCarryOverWarnings",
    "/LeaveRequests",
    "/LeaveTracking",
    "/LeaveTypes",
]

focused_gate = next(
    item for item in overrides["validation"]["fast_gates"]
    if item["id"] == "FG-LEAVE-TESTS"
)
focused_gate["command"] = (
    "dotnet test Tests/IK.Web.Tests.csproj -c Release --no-restore --filter "
    '"FullyQualifiedName~LeaveEntitlementServiceTests|FullyQualifiedName~LeaveRequestServiceTests|'
    'FullyQualifiedName~LeaveCarryOverWarningServiceTests|FullyQualifiedName~PersonnelInformationUiContractTests|'
    'FullyQualifiedName~ManagementUiContractTests" --disable-build-servers'
)

operations = next(
    item for item in overrides["outcome_closure"]["acceptance"]
    if item["id"] == "A-OPERATIONS"
)
operations["criterion"] = operations["target"] = (
    "Günlük yerel schedule, startup catch-up, disable, retry, cancellation, "
    "özet log ve devir uyarısı yönetimi kanıtlanır."
)

source["decision_control"]["documentation_coverage"] = [
    {
        "path": "Docs/leave-entitlement-policy.md",
        "subject_ids": sorted(
            {
                subject
                for item in contract["confirmed_decisions"]
                for subject in item["subject_ids"]
                if subject.startswith("leave")
            }
        ),
        "result": "CONSISTENT",
        "sha256": "sha256:" + hashlib.sha256(
            (ROOT / "Docs/leave-entitlement-policy.md").read_bytes()
        ).hexdigest(),
        "evidence": (
            "Canonical policy now documents daily reconciliation, half-day constraints, "
            "category allocations, manual pregnancy, warning management and DEVELOPMENT reset."
        ),
    },
    {
        "path": "Docs/phase-1-architecture.md",
        "subject_ids": ["employee.self-service.editable-fields"],
        "result": "CONSISTENT",
        "sha256": "sha256:" + hashlib.sha256(
            (ROOT / "Docs/phase-1-architecture.md").read_bytes()
        ).hexdigest(),
        "evidence": (
            "Canonical architecture now binds the expanded fields to the unchanged "
            "self/elevated backend scope authority."
        ),
    },
]

overrides["entity_mapping"]["evidence"] = (
    "Data annotations remain scalar/relationship authority; minimal Fluent SQL checks enforce "
    "half-day, positive-key, category, source and derived-total invariants not emitted by annotations."
)
overrides["entity_mapping"]["fluent_exceptions"][0]["mapping"] = (
    "Half-day amounts, positive LeaveType identity, request/allocation enum ranges, "
    "and balance/warning derived-total check constraints"
)
overrides["entity_mapping"]["fluent_exceptions"][0]["minimal_fluent_scope"] = (
    "Only SQL check expressions for 0.5 increments, positive generated IDs, enum ranges, "
    "and persisted derived-total equality."
)

overrides["review"]["focus"] = [
    item.replace("Positive identity data-preserving cutover", "Positive identity DEVELOPMENT clean reset")
    for item in overrides["review"]["focus"]
]
overrides["review"]["lifecycle"]["state"] = "DELTA_REVIEW_IN_PROGRESS"
overrides["review"]["lifecycle"]["evidence"] = (
    "Revision-10 corrects the phase cutover and requires the same independent reviewer "
    "after current validation evidence is attached."
)

overrides.setdefault("material_deltas", []).append(
    {
        "revision": 10,
        "change": (
            "Replace the revision-9 data-preservation conversion/backfill with a DEVELOPMENT "
            "clean reset of obsolete leave-domain state; retain employees/departments and "
            "document backup restore as the rollback authority."
        ),
        "evidence": (
            "D-PHASE-DEVELOPMENT-LEAVE-R1 plus active BetTask DEVELOPMENT policy forbids "
            "compatibility/data-preservation migrations and requires direct clean removal."
        ),
    }
)

authority_map = []
for authority in overrides["authority_map"]:
    path = authority["path"]
    if (ROOT / path).is_file() and path not in {
        "Migrations/20260731113043_AddAutomaticLeaveEntitlements.cs",
        "Migrations/20260731113043_AddAutomaticLeaveEntitlements.Designer.cs",
    }:
        authority_map.append(authority)
for path, symbol, reason in [
    (
        "Migrations/20260803072742_DailyLeaveEntitlementsAndCategories.cs",
        "DailyLeaveEntitlementsAndCategories.Up",
        "DEVELOPMENT leave-domain reset and positive-ID/category/allocation schema cutover",
    ),
    (
        "Services/DailyLeaveEntitlementWorker.cs",
        "DailyLeaveEntitlementWorker.RunAsync",
        "Startup and next-local-day entitlement scheduling authority",
    ),
    (
        "Models/LeaveDayAmountAttribute.cs",
        "LeaveDayAmountAttribute",
        "Application data-annotation authority for whole/half-day values",
    ),
]:
    if all(item["path"] != path for item in authority_map):
        authority_map.append({"path": path, "symbol": symbol, "reason": reason, "sha256": "PLANNED"})
for authority in authority_map:
    authority["sha256"] = "sha256:" + hashlib.sha256(
        (ROOT / authority["path"]).read_bytes()
    ).hexdigest()
overrides["authority_map"] = authority_map

validation_ids = sorted(source["change_control"]["receipt_sha256"])
source["checkpoint"] = {
    "phase": "IMPLEMENTATION",
    "completed_step_ids": ["DISCOVERY"],
    "pending_step_ids": ["IMPLEMENTATION", "VALIDATION", "REVIEW", "FINAL_CLOSEOUT"],
    "invalidated_validation_ids": validation_ids,
    "active_remediation_batch_id": None,
}

compiled = build_contract(source, SKILL_ROOT, base_contract=contract)
_, _, live_snapshot, unrelated_baseline, _ = live_change_state(compiled)
source["change_control"]["current_snapshot_sha256"] = live_snapshot
source["change_control"]["unrelated_baseline_sha256"] = unrelated_baseline

contract_api.atomic_write_json(OUTPUT_PATH, source, compact=False)
print(json.dumps({"result": "WRITTEN", "path": str(OUTPUT_PATH)}, ensure_ascii=False))
