from __future__ import annotations

import copy
import hashlib
import json
import subprocess
import sys
from pathlib import Path


ROOT = Path("/Users/egesoykara/Desktop/IK")
SKILL_ROOT = Path("/Users/egesoykara/.codex/skills/bet-task")
DOTNET = "/Users/egesoykara/usr/local/share/dotnet/dotnet"
CONTRACT_DIR = ROOT / ".bet-task/contracts/BET-LEAVE-AUTOMATION-AUDIT"
SOURCE_PATH = CONTRACT_DIR / "source-r10.json"
OUTPUT_PATH = CONTRACT_DIR / "source-r11.json"
CONTRACT_PATH = CONTRACT_DIR / "contract.json"

sys.dont_write_bytecode = True
sys.path.insert(0, str(SKILL_ROOT / "scripts"))

from bet_contract import api as contract_api  # noqa: E402
from bet_contract.change_rules import live_change_state  # noqa: E402
from bet_contract.decisions import enrich_conflict, enrich_decision  # noqa: E402
from bet_contract.review import review_pass_digest  # noqa: E402
from build_task_contract import build_contract  # noqa: E402


source = contract_api.loads_json(SOURCE_PATH.read_text(encoding="utf-8"))
contract = contract_api.loads_json(CONTRACT_PATH.read_text(encoding="utf-8"))
overrides = source["overrides"]

if contract["contract_revision"] == 10:
    contract_api.atomic_write_json(CONTRACT_DIR / "revisions/10.json", contract, compact=False)
else:
    contract = contract_api.loads_json(
        (CONTRACT_DIR / "revisions/10.json").read_text(encoding="utf-8")
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

overrides["contract_revision"] = 11
overrides["status"] = "READY"
overrides["repository"]["root"] = str(ROOT)
overrides["repository"]["git_head"] = subprocess.check_output(
    ["git", "rev-parse", "HEAD"], cwd=ROOT, text=True
).strip()
source["repo_policy"] = {
    "path": str(ROOT / "AGENTS.md"),
    "sha256": "sha256:" + hashlib.sha256((ROOT / "AGENTS.md").read_bytes()).hexdigest(),
}

overrides["task"]["outcome"] = (
    "Bakiyesi bulunan kalıcı manuel izin türlerini yıllık izinle birlikte talep "
    "seçimine açmak; yıllık iznin kıdem bakiyesi toplama/düşüm otoritesini korumak; "
    "Seferberlik İzni'ni devretmeyen, yıllık toplam 2 günle sınırlı ve yalnız aktif "
    "erkek çalışanlara günlük worker tarafından otomatik atanan bir varsayılan tür yapmak."
)

scope_files = set(overrides["scope"]["files"])
scope_files.update(
    {
        "Components/Pages/Home.razor",
        "Components/Pages/LeaveApprovals.razor",
        "Components/Pages/LeaveRequests.razor",
        "Database/001_create_human_resources_schema.sql",
        "Database/HumanResourcesDbContext.cs",
        "Docs/leave-entitlement-policy.md",
        "Docs/phase-1-architecture.md",
        "Migrations/20260803102721_AddBalanceBackedLeaveRequestsAndMobilization.Designer.cs",
        "Migrations/20260803102721_AddBalanceBackedLeaveRequestsAndMobilization.cs",
        "Migrations/HumanResourcesDbContextModelSnapshot.cs",
        "Models/LeaveEntitlementKind.cs",
        "Models/DomainConstants.cs",
        "Models/LeaveRequest.cs",
        "Models/LeaveRequestCategory.cs",
        "Models/LeaveType.cs",
        "Services/LeaveApprovalFilterOptionQuery.cs",
        "Services/LeaveBalanceService.cs",
        "Services/LeaveEntitlementService.cs",
        "Services/LeaveRequestService.cs",
        "Services/LeaveTrackingService.cs",
        "Tests/LeaveApprovalVisibilityQueryTests.cs",
        "Tests/LeaveEntitlementServiceTests.cs",
        "Tests/LeaveRequestServiceTests.cs",
        "Tests/LeaveRequestSchemaContractTests.cs",
        "Tests/LeaveTrackingTests.cs",
        "Tests/ManagementUiContractTests.cs",
    }
)
planned_files = set()
missing = sorted(path for path in scope_files if not (ROOT / path).is_file())
if missing:
    raise RuntimeError(f"Revision-11 scope contains missing files: {missing}")
overrides["scope"]["diff_base"] = overrides["repository"]["git_head"]
overrides["scope"]["files"] = sorted(scope_files)
overrides["scope"]["contracts"] = sorted(
    (set(overrides["scope"]["contracts"])
    - {"Annual/sickness leave-request category and allocation contract"})
    | {
        "Balance-backed persisted leave-type request contract",
        "Male-only two-day mobilization entitlement contract",
    }
)
overrides["scope"]["unrelated_dirty_files"] = ["appsettings.json"]

old_decision = next(
    item
    for item in contract["confirmed_decisions"]
    if item["id"] == "D-LEAVE-REQUEST-CATEGORIES-R9"
)
old_decision = copy.deepcopy(old_decision)
old_decision["status"] = "SUPERSEDED"
old_decision["superseded_by_decision_id"] = "D-LEAVE-BALANCE-BACKED-REQUESTS-R11"
enrich_decision(old_decision, revision=11)

request_decision_text = (
    "Leave-request selection is balance-backed: Annual Leave aggregates all service-tier "
    "balances and keeps its carry-over-first allocation order; every persisted non-service "
    "leave type, including manually created types, is selectable only when the employee has "
    "a positive balance for the request year, and approval deducts only that selected balance."
)
request_evidence = (
    "CURRENT_USER_REQUEST: izin talebi oluştururken yıllık izin ve hastalık izni diye "
    "seçilebiliyor ancak yeni oluşturulan manuel türler seçilemiyor,otomatik oluşturduğumuz "
    "izin türleri dışında eğer manuel olarak da izin oluşturursak, bu yeni manuel oluşturulan "
    "izinler de seçilebilsin talep oluştururken ve tabi eğer o kişinin o izin için bakiyesi varsa"
)
request_decision = {
    "id": "D-LEAVE-BALANCE-BACKED-REQUESTS-R11",
    "kind": "PRODUCT_BEHAVIOR",
    "decision": request_decision_text,
    "user_confirmed": True,
    "subject_ids": ["leave.request.categories", "leave.request.deduction-order"],
    "evidence": request_evidence,
    "created_revision": 11,
    "status": "ACTIVE",
    "supersedes_decision_id": "D-LEAVE-REQUEST-CATEGORIES-R9",
    "superseded_by_decision_id": None,
}
enrich_decision(request_decision, revision=11)

mobilization_decision = {
    "id": "D-LEAVE-MOBILIZATION-R11",
    "kind": "PRODUCT_BEHAVIOR",
    "decision": (
        "Mobilization Leave is a default, non-carrying leave type whose year-scoped balance "
        "is capped at 2 total days; the daily entitlement worker assigns it automatically "
        "only to active male employees, and request-time backend validation enforces the "
        "same male-only eligibility."
    ),
    "user_confirmed": True,
    "subject_ids": ["leave.mobilization.male-auto-two-day"],
    "evidence": (
        "CURRENT_USER_REQUEST: seferberlik adlı yeni bir izin türü oluştur ve bunu sadece "
        "erkekler kullanabilsin, atama otomatik olsun ve sadece erkekler için olsun, miktar "
        "olarak da en fazla 2 gün olsun toplam"
    ),
}
enrich_decision(mobilization_decision, revision=11)

decisions = []
for item in contract["confirmed_decisions"]:
    decisions.append(old_decision if item["id"] == old_decision["id"] else copy.deepcopy(item))
decisions.extend([request_decision, mobilization_decision])
overrides["confirmed_decisions"] = decisions

material_impact = (
    "Replaces the fixed two-category request contract with a nullable persisted leave-type "
    "selection, changes request schema/service/UI/filter/display behavior, and extends tests "
    "and canonical leave documentation while preserving annual aggregate allocation ordering."
)
conflict = {
    "id": "C-LEAVE-REQUEST-CATEGORIES-R11",
    "created_revision": 11,
    "subject_ids": ["leave.request.categories", "leave.request.deduction-order"],
    "requested_change": request_decision_text,
    "material_impact": material_impact,
    "conflicts_with_decision_ids": ["D-LEAVE-REQUEST-CATEGORIES-R9"],
    "user_notification": (
        f"Confirmed decision D-LEAVE-REQUEST-CATEGORIES-R9 ({contract['confirmed_decisions'][6]['decision']}) "
        f"conflicts with requested change: {request_decision_text} Material impact: {material_impact}"
    ),
    "status": "APPROVED",
    "resolved_revision": 11,
    "resolution_decision_id": request_decision["id"],
    "approval_evidence": request_evidence,
}
enrich_conflict(conflict, revision=11)
source["decision_control"]["conflicts"] = copy.deepcopy(
    contract["decision_control"]["conflicts"]
) + [conflict]

overrides["invariants"] = [
    item
    for item in overrides["invariants"]
    if "yalnız Yıllık İzin veya Hastalık İzni" not in item
    and "beş varsayılan izin tür" not in item.lower()
] + [
    "Yıllık İzin seçimi kıdem bakiyelerini tek kategori olarak toplar; diğer her talep tek pozitif LeaveTypeId bakiyesine bağlıdır.",
    "Talep ekranı ve backend yalnız talep yılı için pozitif bakiyesi bulunan kalıcı non-service izin türlerini kabul eder.",
    "Seferberlik İzni devretmez, yıllık toplam 2 günü aşmaz ve yalnız aktif erkek çalışanlara otomatik atanır; cinsiyet talep anında yeniden doğrulanır.",
    "Temiz kurulumun altı varsayılan izin türü vardır; Seferberlik İzni pozitif identity zincirinde 6 numaradır.",
    "DEVELOPMENT leave-domain cutover aktif vekâlet kaynaklı yönetici bağlarını izin verisini silmeden önce asıl departman yöneticisi otoritesine geri döndürür.",
]

for requirement in overrides["outcome_closure"]["requirements"]:
    if requirement["id"] == "R-REQUEST-CATEGORIES":
        requirement.update(
            decision_id=request_decision["id"],
            statement=(
                "Talep ekranında Yıllık İzin ile talep yılı için pozitif bakiyesi bulunan "
                "tüm kalıcı non-service izin türlerini sun; manuel oluşturulan türleri bakiye "
                "yoksa gösterme ve yıllık devirden-başlayan düşüm sırasını koru."
            ),
            evidence=request_evidence,
        )
overrides["outcome_closure"]["requirements"].append(
    {
        "id": "R-MOBILIZATION",
        "source": "USER",
        "statement": (
            "Seferberlik İzni'ni devretmeyen ve yıllık toplam 2 günle sınırlı varsayılan "
            "tür olarak yalnız aktif erkek çalışanlara otomatik ata ve yalnız onların kullanmasına izin ver."
        ),
        "beneficiaries": ["erkek çalışan", "İK operatörü"],
        "disposition": "IN_SCOPE",
        "decision_id": mobilization_decision["id"],
        "evidence": mobilization_decision["evidence"],
    }
)
for acceptance in overrides["outcome_closure"]["acceptance"]:
    if acceptance["id"] in {
        "A-FUNCTIONAL",
        "A-COVERAGE",
        "A-REGRESSION",
        "A-DOCS",
    }:
        acceptance["requirement_ids"] = sorted(
            set(acceptance["requirement_ids"]) | {"R-MOBILIZATION"}
        )
    acceptance["evidence"] = "Revision-11 implementation and validation pending."

overrides["applicability"]["background_worker"]["evidence"] = (
    "Günlük worker yeni erkek-özel Seferberlik türünü de uygun çalışanlara uzlaştırır."
)
overrides["applicability"]["database"]["evidence"] = (
    "LeaveRequests specific LeaveType FK/check contract and the default Mobilization seed "
    "require an incremental EF/MSSQL migration."
)
overrides["applicability"]["database_mapping"]["evidence"] = (
    "Request category-to-nullable LeaveType relationship requires attributes plus one "
    "cross-column SQL check that is not expressible by attributes."
)
overrides["applicability"]["security_boundary"]["evidence"] = (
    "The backend must reject zero-balance manual types and non-male Mobilization requests "
    "independently of UI visibility."
)
overrides["applicability"]["ui_live_browser"]["evidence"] = (
    "The actual LeaveRequests dialog must prove balance-filtered manual types, Mobilization "
    "visibility, responsive behavior and backend-negative paths."
)

for gate in overrides["validation"]["fast_gates"]:
    gate["result"] = "PLANNED"
    gate["evidence"] = "Revision-11 changes invalidate the prior receipt."
    if gate["id"] == "FG-LEAVE-TESTS":
        gate["command"] = (
            f"{DOTNET} test Tests/IK.Web.Tests.csproj -c Release --no-restore --filter "
            '"FullyQualifiedName~LeaveEntitlementServiceTests|FullyQualifiedName~LeaveRequestServiceTests|'
            'FullyQualifiedName~LeaveRequestSchemaContractTests|FullyQualifiedName~LeaveApprovalVisibilityQueryTests|'
            'FullyQualifiedName~LeaveTrackingTests|FullyQualifiedName~LeaveCarryOverWarningServiceTests|'
            'FullyQualifiedName~PersonnelInformationUiContractTests|FullyQualifiedName~ManagementUiContractTests" '
            "--disable-build-servers"
        )
        gate["covers"] = (
            "Balance-backed manual request selection; annual aggregate allocation; "
            "male-only automatic two-day Mobilization; request-time eligibility; UI contract"
        )
    if gate["id"] == "FG-MIGRATION":
        gate["command"] = (
            f"env PATH=/Users/egesoykara/usr/local/share/dotnet:/Users/egesoykara/.dotnet/tools:/usr/bin:/bin "
            f"{DOTNET} ef migrations has-pending-model-changes --project IK.Web.csproj "
            "--configuration Release --no-build"
        )
    if gate["id"] == "FG-ALL-TESTS":
        gate["command"] = (
            f"{DOTNET} test IKSolution.slnx -c Release --no-restore --disable-build-servers"
        )
    if gate["id"] == "FG-BUILD":
        gate["command"] = (
            f"{DOTNET} build IKSolution.slnx -c Release --no-restore --disable-build-servers"
        )
scenario = overrides["validation"]["comprehensive_scenarios"][0]
scenario.update(
    coverage=(
        "Authenticated real MSSQL browser flow proves a manually created balance-backed "
        "leave type and male Mobilization are selectable, zero-balance/female paths are "
        "absent/rejected, annual requests remain available, and responsive/error states pass."
    ),
    evidence="Revision-11 changes invalidate the prior receipt.",
    result="PLANNED",
)
scenario["proof_artifact"]["scenario_sha256"] = "PLANNED"

authority_by_path = {item["path"]: copy.deepcopy(item) for item in overrides["authority_map"]}
for path, symbol, reason in [
    ("Migrations/20260803102721_AddBalanceBackedLeaveRequestsAndMobilization.cs", "AddBalanceBackedLeaveRequestsAndMobilization.Up", "DEVELOPMENT leave-domain clean cutover with canonical manager restoration, request FK, hard cap and Mobilization seed migration"),
    ("Database/HumanResourcesDbContext.cs", "HumanResourcesDbContext.OnModelCreating", "Seed and request selection constraint authority"),
    ("Models/DomainConstants.cs", "DomainConstants.MobilizationLeaveMaximumDays", "Single application constant for the hard two-day cap"),
    ("Models/LeaveRequest.cs", "LeaveRequest.Category and LeaveTypeId", "Annual aggregate versus specific persisted leave-type request schema authority"),
    ("Models/LeaveEntitlementKind.cs", "LeaveEntitlementKind.MaleEmployees", "Male-only entitlement discriminator"),
    ("Services/LeaveRequestService.cs", "GetRequestBalancesAsync", "Balance-backed selection and request-time eligibility authority"),
    ("Services/LeaveBalanceService.cs", "ReconcileAutomaticEntitlementsAsync", "Automatic male Mobilization assignment authority"),
    ("Components/Pages/LeaveRequests.razor", "AvailableLeaveRequestOptions", "Balance-filtered request selection UI"),
]:
    authority_by_path[path] = {
        "path": path,
        "symbol": symbol,
        "reason": reason,
        "sha256": "PLANNED" if path in planned_files else (
            "sha256:" + hashlib.sha256((ROOT / path).read_bytes()).hexdigest()
        ),
    }
for item in authority_by_path.values():
    if item["path"] not in planned_files and (ROOT / item["path"]).is_file():
        item["sha256"] = "sha256:" + hashlib.sha256(
            (ROOT / item["path"]).read_bytes()
        ).hexdigest()
overrides["authority_map"] = sorted(authority_by_path.values(), key=lambda item: item["path"])

coverage = []
active_leave_subjects = sorted(
    {
        subject
        for item in decisions
        if item["status"] == "ACTIVE"
        for subject in item["subject_ids"]
        if subject.startswith("leave")
    }
)
coverage.append(
    {
        "path": "Docs/leave-entitlement-policy.md",
        "subject_ids": active_leave_subjects,
        "result": "CONSISTENT",
        "sha256": "sha256:" + hashlib.sha256(
            (ROOT / "Docs/leave-entitlement-policy.md").read_bytes()
        ).hexdigest(),
        "evidence": (
            "Canonical leave policy documents balance-backed manual selection plus "
            "male-only two-day Mobilization."
        ),
    }
)
architecture_coverage = copy.deepcopy(source["decision_control"]["documentation_coverage"][1])
architecture_coverage["sha256"] = "sha256:" + hashlib.sha256(
    (ROOT / "Docs/phase-1-architecture.md").read_bytes()
).hexdigest()
architecture_coverage["evidence"] = (
    "Canonical architecture documents the balance-backed request and Mobilization authorities."
)
coverage.append(architecture_coverage)
source["decision_control"]["documentation_coverage"] = coverage

overrides["entity_mapping"]["evidence"] = (
    "The nullable LeaveType relationship is attribute-expressible; one minimal Fluent SQL "
    "check binds AnnualLeave to null LeaveTypeId and SpecificLeaveType to positive LeaveTypeId."
)
overrides["entity_mapping"]["fluent_exceptions"][0]["mapping"] = (
    "Half-day amounts, positive LeaveType identity, request category/LeaveType pairing, "
    "Mobilization policy/cap, allocation enums, and balance/warning derived-total check constraints"
)
overrides["entity_mapping"]["fluent_exceptions"][0]["minimal_fluent_scope"] = (
    "Only SQL check expressions for 0.5 increments, positive generated IDs, cross-column "
    "request selection pairing, enum ranges, and persisted derived-total equality."
)

overrides["review"]["focus"] = sorted(
    set(overrides["review"]["focus"])
    | {
        "Balance-backed manual type option and exact selected-balance deduction",
        "Male-only automatic two-day Mobilization with backend revalidation",
        "Complete removal of the fixed sickness category as request authority",
        "Active delegation manager-authority restoration during DEVELOPMENT cutover",
    }
)
overrides["review"]["lifecycle"]["state"] = "DELTA_REVIEW_IN_PROGRESS"
overrides["review"]["lifecycle"]["evidence"] = (
    "Revision-11 continues with the same independent reviewer identity after current "
    "validation receipts and canonical evidence are attached."
)

review_pass = {
    "id": "REVIEW-4",
    "kind": "DELTA_REVIEW",
    "packet_mode": "DELTA_ONLY",
    "contract_revision": 11,
    "reviewer_identity": "/root/leave_automation_reviewer",
    "reviewer_model": "gpt-5.6-sol",
    "independence": "INDEPENDENT",
    "policy_fingerprint": contract_api.policy_fingerprint(
        SKILL_ROOT, contract_api.policy_source_ids_from_raw(contract, SKILL_ROOT)
    ),
    "score": 7,
    "open_findings": {"P0": 0, "P1": 2, "P2": 0, "P3": 0},
    "blocking_finding_ids": ["P1-DEVELOPMENT-CUTOVER", "P1-MOBILIZATION-HARD-CAP"],
    "scenario_evidence_sha256": "sha256:320724935f2adefcc6e156fe7de8770c2025aa29cd6cf93fc1e3721fa1a2b093",
    "attestation_surface_sha256": "sha256:f9d9e7de86d2759cc7b2630f3f5e5174a1e649301cacf839211fa681801f3b23",
    "evidence": (
        "Independent reviewer found that the general over-limit path could exceed the "
        "two-day Mobilization requirement and that a manual ID-6 type or legacy Category-2 "
        "request could block the incremental DEVELOPMENT migration."
    ),
    "digest": "PLANNED",
}
review_pass["digest"] = review_pass_digest(review_pass)
review_pass_5 = {
    "id": "REVIEW-5",
    "kind": "DELTA_REVIEW",
    "packet_mode": "DELTA_ONLY",
    "contract_revision": 11,
    "reviewer_identity": "/root/leave_automation_reviewer",
    "reviewer_model": "gpt-5.6-sol",
    "independence": "INDEPENDENT",
    "policy_fingerprint": contract_api.policy_fingerprint(
        SKILL_ROOT, contract_api.policy_source_ids_from_raw(contract, SKILL_ROOT)
    ),
    "score": 8,
    "open_findings": {"P0": 0, "P1": 1, "P2": 0, "P3": 0},
    "blocking_finding_ids": ["P1-DEVELOPMENT-CUTOVER-AUTHORITY"],
    "scenario_evidence_sha256": "sha256:e306b77b5e3c52329eda063d19f48ef4b70988c1c7a03597b81dc7e217ba9041",
    "attestation_surface_sha256": "sha256:963ec04c753802bd00db7e5d0a4272826802400296e52afcdd5927bca777df26",
    "evidence": (
        "Independent reviewer confirmed the hard-cap and original ID/category collision "
        "findings resolved, then found that the DEVELOPMENT reset removed active delegation "
        "state without restoring affected employee ManagerId values to canonical department authority."
    ),
    "digest": "PLANNED",
}
review_pass_5["digest"] = review_pass_digest(review_pass_5)
overrides["review"]["lifecycle"]["passes"] = [
    item
    for item in overrides["review"]["lifecycle"]["passes"]
    if item["id"] not in {"REVIEW-4", "REVIEW-5"}
] + [review_pass, review_pass_5]
overrides["review"]["lifecycle"]["no_progress_rounds"] = 0

overrides["validation"]["remediation"] = {
    "performed": True,
    "batches": [
        {
            "id": "REVIEW-REMEDIATION-R11",
            "revision": 11,
            "change": (
                "Make the Mobilization two-day/no-carry policy non-overridable and replace "
                "the collision-prone incremental migration with a DEVELOPMENT leave-domain clean cutover "
                "that restores active delegation manager authority before deleting leave-derived state."
            ),
            "changed_surfaces": [
                ".bet-task/e2e/FixtureTool/Program.cs",
                "Components/Pages/LeaveTypes.razor",
                "Database/001_create_human_resources_schema.sql",
                "Database/HumanResourcesDbContext.cs",
                "Docs/leave-entitlement-policy.md",
                "Docs/phase-1-architecture.md",
                "Migrations/20260803102721_AddBalanceBackedLeaveRequestsAndMobilization.cs",
                "Migrations/20260803102721_AddBalanceBackedLeaveRequestsAndMobilization.Designer.cs",
                "Migrations/HumanResourcesDbContextModelSnapshot.cs",
                "Models/DomainConstants.cs",
                "Services/LeaveBalanceService.cs",
                "Services/LeaveEntitlementService.cs",
                "Tests/LeaveEntitlementServiceTests.cs",
                "Tests/LeaveRequestSchemaContractTests.cs",
                "Tests/ManagementUiContractTests.cs",
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
                "Shared database schema/seed, balance authority, leave-type mutation and the "
                "real migration fixture changed, including an active delegation/direct-report restoration "
                "case; all fast gates and the combined browser baseline must rerun."
            ),
            "result": "PLANNED",
        }
    ],
}

overrides.setdefault("material_deltas", []).append(
    {
        "revision": 11,
        "change": (
            "Supersede the fixed Annual/Sickness request contract with balance-backed "
            "persisted leave-type selection and add male-only automatic non-carrying "
            "two-day Mobilization Leave."
        ),
        "evidence": (
            "CURRENT_USER_REQUEST explicitly requires manually created types with balances "
            "to be requestable and Mobilization to be automatic, male-only and capped at 2 days."
        ),
    }
)

validation_ids = sorted(source["change_control"]["receipt_sha256"])
source["checkpoint"] = {
    "phase": "VALIDATION",
    "completed_step_ids": ["DISCOVERY", "IMPLEMENTATION"],
    "pending_step_ids": ["VALIDATION", "REVIEW", "FINAL_CLOSEOUT"],
    "invalidated_validation_ids": validation_ids,
    "active_remediation_batch_id": "REVIEW-REMEDIATION-R11",
}

compiled = build_contract(source, SKILL_ROOT, base_contract=contract)
_, _, live_snapshot, unrelated_baseline, _ = live_change_state(compiled)
source["change_control"]["current_snapshot_sha256"] = live_snapshot
source["change_control"]["unrelated_baseline_sha256"] = unrelated_baseline

contract_api.atomic_write_json(OUTPUT_PATH, source, compact=False)
compiled = build_contract(source, SKILL_ROOT, base_contract=contract)
errors = contract_api.validate_contract(
    compiled,
    skill_root=SKILL_ROOT,
    contract_path=CONTRACT_PATH,
    allow_blocked=False,
    final=False,
)
if errors:
    raise RuntimeError("Revision-11 contract invalid:\n" + "\n".join(errors))
contract_api.atomic_write_json(CONTRACT_PATH, compiled, compact=False)
print(json.dumps(
    {"result": "WRITTEN", "source": str(OUTPUT_PATH), "contract": str(CONTRACT_PATH)},
    ensure_ascii=False,
))
