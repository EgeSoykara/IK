import copy
import hashlib
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parent
TEMPLATE_PATH = ROOT / "source-r16.json"
SOURCE_PATH = ROOT / "source-r17.json"
CONTRACT_PATH = ROOT / "contract.json"


def sha256_record(value):
    payload = {key: item for key, item in value.items() if key != "record_sha256"}
    encoded = json.dumps(payload, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode()
    return f"sha256:{hashlib.sha256(encoded).hexdigest()}"


source = json.loads(TEMPLATE_PATH.read_text())
contract = json.loads(CONTRACT_PATH.read_text())
(ROOT / "revisions" / "16.json").write_text(
    json.dumps(contract, ensure_ascii=False, indent=2) + "\n"
)
overrides = source["overrides"]

source["base_contract"] = {
    "contract_id": contract["contract_id"],
    "revision": contract["contract_revision"],
    "contract_definition_sha256": contract["workflow_checkpoint"]["contract_definition_sha256"],
    "decision_ledger_sha256": contract["decision_control"]["decision_ledger_sha256"],
    "blocker_history_sha256": contract["decision_control"]["blocker_history_sha256"],
    "conflict_ledger_sha256": contract["decision_control"]["conflict_ledger_sha256"],
}
source["decision_control"]["base_contract"] = copy.deepcopy(source["base_contract"])
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
    "confirmation_artifact": None,
    "confirmation_source": "FOLLOWUP_USER_APPROVAL",
    "created_revision": 15,
    "decision": (
        "Add atomic insert-only XLSX import and re-importable XLSX export to Public Holidays, "
        "Employees, Bank Accounts, Identity/Documents, Phones, Addresses, Educations and "
        "Course/Certificates; reject the whole import on any invalid or duplicate row. "
        "Activate same-department manager delegation only for HR-approved leave on its start "
        "date, restore it after the inclusive end date, reconcile at startup and daily, and "
        "move pending manager approvals to the delegate. Department manager is an active "
        "employee of that department, manages its other employees, and reports to the parent "
        "department manager; a child manager requires the parent manager to be configured."
    ),
    "evidence": "FOLLOWUP_USER_APPROVAL: her şeye evet ve onaylıyorum",
    "id": "D-EXCEL-MANAGER-DELEGATION-R15",
    "kind": "PRODUCT_BEHAVIOR",
    "status": "ACTIVE",
    "subject_ids": [
        "excel.atomic-import-export",
        "employee.required-fields",
        "department.manager-authority",
        "department.manager-hierarchy",
        "leave.manager-delegation",
    ],
    "superseded_by_decision_id": None,
    "supersedes_decision_id": None,
    "user_confirmed": True,
}
decision["record_sha256"] = sha256_record(decision)
overrides["confirmed_decisions"] = [
    item for item in overrides["confirmed_decisions"]
    if item["id"] != decision["id"]
]
overrides["confirmed_decisions"].append(decision)
overrides["contract_revision"] = 17
overrides["status"] = "READY"
overrides["repository"]["git_head"] = "8689abfcbc22fd575bf67a91a08be4235522f738"
overrides["task"] = {
    "outcome": (
        "Provide safe XLSX bulk import/export on approved personnel and holiday pages, enforce "
        "complete personnel data entry, replace manual employee manager assignment with the "
        "department hierarchy, and automatically delegate/restore management during approved leave."
    ),
    "explicit_exclusions": [
        "Remove or replace existing single-record CRUD",
        "Excel support on General Information and Employee Termination pages",
        "Silent spreadsheet update/upsert or partial import",
        "Cross-department or inactive delegate selection",
        "Unrelated UI redesign or refactor",
        "Compatibility adapters, dual manager authority, or legacy manager fallback",
    ],
}
overrides["classification"] = {
    "classes": [
        "api-data-contract",
        "backend-domain",
        "background-worker",
        "database-persistence",
        "documentation-operator",
        "product-ui",
        "security-boundary",
        "ui-ux",
    ],
    "validation_rows": [
        "api-data-contract",
        "backend-domain",
        "background-worker",
        "database-persistence",
        "documentation-operator",
        "product-ui",
        "security-boundary",
        "ui-ux",
    ],
    "integrated_module": {
        "required": True,
        "evidence": (
            "Blazor Server pages, direct EF persistence, XLSX file parsing, department/employee "
            "manager authority, leave approval snapshots and a recurring reconciliation worker "
            "participate in one runtime workflow."
        ),
    },
}

scope_files = set(overrides["scope"]["files"])
scope_files.update(
    {
        "Components/Pages/Departments.razor",
        "Components/Pages/PublicHolidays.razor",
        "Models/Department.cs",
        "Models/LeaveRequest.cs",
        "Models/ManagerDelegation.cs",
        "Services/DepartmentManagerService.cs",
        "Services/ManagerDelegationService.cs",
        "Services/ManagerDelegationWorker.cs",
        "Services/PersonnelExcelService.cs",
        "Services/PersonnelRecordValidator.cs",
        "Migrations/20260728000000_AddDepartmentManagerDelegation.cs",
        "Tests/DepartmentManagerAndDelegationTests.cs",
        "Tests/PersonnelExcelAndValidationTests.cs",
        "Docs/personnel-excel-import-export.md",
        "Components/ExcelImportExportActions.razor",
        "Program.cs",
        "Models/AuditActionType.cs",
        "Migrations/20260728132817_AddDepartmentManagerDelegation.cs",
        "Migrations/20260728132817_AddDepartmentManagerDelegation.Designer.cs",
        "Tests/LeaveRequestServiceTests.cs",
        "Tests/EmployeeFileSchemaContractTests.cs",
        "Migrations/20260728144637_BackfillDepartmentManagerAuthority.cs",
        "Migrations/20260728144637_BackfillDepartmentManagerAuthority.Designer.cs",
    }
)
scope_files.discard("Migrations/20260728000000_AddDepartmentManagerDelegation.cs")
overrides["scope"]["files"] = sorted(scope_files)
overrides["scope"]["docs"] = sorted(
    set(overrides["scope"]["docs"])
    | {
        "Docs/personnel-excel-import-export.md",
        "Docs/phase-1-architecture.md",
        "BetSolution/Docs/UX/ui-ux-regression-checklist.md",
    }
)
overrides["scope"]["surfaces"] = sorted(
    set(overrides["scope"]["surfaces"])
    | {"/PublicHolidays", "/Departments"}
)
overrides["scope"]["contracts"] = [
    "Atomic insert-only XLSX schema and authorization contract",
    "Personnel required-field validation contract",
    "Department manager and parent-manager hierarchy contract",
    "Approved-leave manager delegation lifecycle contract",
    "EF Core model, SQL migration and hosted-worker contract",
]
overrides["scope"]["diff_base"] = "8689abfcbc22fd575bf67a91a08be4235522f738"

overrides["invariants"] = [
    "Existing manual CRUD remains available on every touched page.",
    "XLSX import is server-authorized, insert-only, all-or-nothing, rejects duplicate or invalid rows, and never logs PII.",
    "Exports contain only records visible to the current authorized principal and are valid re-import templates.",
    "DepartmentId zero is rejected before persistence with a Turkish department-required validation message.",
    "Required personnel rules are shared by manual save and XLSX import; historical nullable schema is not a second validation authority.",
    "Employee.ManagerId is derived only from department manager hierarchy or an active approved-leave delegation; manual manager editing is removed.",
    "A department manager is an active employee in the same department; a child department manager reports to the configured parent manager.",
    "A department with a parent cannot receive a manager until its parent manager exists; department cycles are rejected.",
    "A delegate is active, in the same department and different from the manager; only one active delegation exists per department.",
    "Delegation starts for an HR-approved leave on StartDate, ends after inclusive EndDate, moves pending manager approvals, and is idempotently reconciled at startup and daily.",
    "Changing a department manager or parent atomically recomputes direct employee and child-manager relationships without a legacy fallback.",
    "Existing employee self-service ownership and elevated cross-employee authorization remain backend enforced.",
    "Attribute-expressible mapping semantics are not duplicated in Fluent API.",
]

for authority in overrides["authority_map"]:
    path = Path("/Users/egesoykara/Desktop/IK") / authority["path"]
    if path.is_file():
        authority["sha256"] = f"sha256:{hashlib.sha256(path.read_bytes()).hexdigest()}"
overrides["authority_map"].extend(
    [
        {
            "path": "Components/Pages/Departments.razor",
            "symbol": "Department form and persistence actions",
            "reason": "Current parent hierarchy UI and department mutation authority.",
            "sha256": f"sha256:{hashlib.sha256((Path('/Users/egesoykara/Desktop/IK') / 'Components/Pages/Departments.razor').read_bytes()).hexdigest()}",
        },
        {
            "path": "Components/Pages/PublicHolidays.razor",
            "symbol": "Public holiday CRUD",
            "reason": "Current holiday manual-entry and authorization authority.",
            "sha256": f"sha256:{hashlib.sha256((Path('/Users/egesoykara/Desktop/IK') / 'Components/Pages/PublicHolidays.razor').read_bytes()).hexdigest()}",
        },
        {
            "path": "Services/LeaveRequestService.cs",
            "symbol": "CreateAsync, UpdateAsync and approval snapshots",
            "reason": "Current leave manager-approver snapshot and lifecycle authority.",
            "sha256": f"sha256:{hashlib.sha256((Path('/Users/egesoykara/Desktop/IK') / 'Services/LeaveRequestService.cs').read_bytes()).hexdigest()}",
        },
        {
            "path": "Models/Department.cs",
            "symbol": "Department",
            "reason": "Current department parent and regional-manager persistence model.",
            "sha256": f"sha256:{hashlib.sha256((Path('/Users/egesoykara/Desktop/IK') / 'Models/Department.cs').read_bytes()).hexdigest()}",
        },
        {
            "path": "Models/Employee.cs",
            "symbol": "Employee.ManagerId",
            "reason": "Current employee manager relationship that will be cut over to department authority.",
            "sha256": f"sha256:{hashlib.sha256((Path('/Users/egesoykara/Desktop/IK') / 'Models/Employee.cs').read_bytes()).hexdigest()}",
        },
    ]
)

overrides["applicability"]["background_worker"] = {
    "required": True,
    "evidence": "Approved leave delegation must activate and restore on DateOnly boundaries even without a user request.",
}
overrides["applicability"]["external_integration"] = {
    "evidence": "XLSX is an uploaded local file format; no remote provider or callback is introduced."
}
overrides["applicability"]["database"] = {
    "required": True,
    "evidence": "Department manager and delegation history require relational persistence and migration."
}
overrides["applicability"]["database_mapping"] = {
    "required": True,
    "evidence": "New manager/delegation foreign keys, uniqueness and delete behaviors require verified mapping."
}
overrides["applicability"]["operational_readiness"] = {
    "required": True,
    "evidence": "The daily worker requires startup recovery, idempotency, failure logging and cancellation behavior."
}
overrides["applicability"]["security_boundary"] = {
    "required": True,
    "evidence": "Excel files contain PII and manager changes alter approval authority."
}
overrides["applicability"]["ui_live_browser"] = {
    "required": True,
    "evidence": "Import/export, validation, department manager and leave delegate flows require real browser proof."
}
overrides["applicability"]["ui_product_consistency"] = {
    "required": True,
    "evidence": "New buttons and selectors must reuse existing MudBlazor page actions and form language."
}
overrides["applicability"]["ui_design_stitch"] = {
    "evidence": "Routine additions to existing management pages do not establish a new visual direction."
}

requirements = [
    ("R15-1", "Add atomic XLSX import and re-importable export to the approved holiday and personnel pages while preserving manual CRUD.", "D-EXCEL-MANAGER-DELEGATION-R15"),
    ("R15-2", "Reject employee creation with DepartmentId zero before database access using a clear Turkish department-required message.", "NOT_APPLICABLE"),
    ("R15-3", "Enforce logically required, cross-field personnel form values consistently for manual entry and import.", "NOT_APPLICABLE"),
    ("R15-4", "Make Department.ManagerEmployeeId the sole employee manager authority and recompute the department on manager changes.", "D-EXCEL-MANAGER-DELEGATION-R15"),
    ("R15-5", "Allow an approved-leave manager to select a same-department delegate and automatically activate, reassign pending approvals, and restore management by date.", "D-EXCEL-MANAGER-DELEGATION-R15"),
    ("R15-6", "Make the parent department manager the manager of each direct child department manager and reject incomplete or cyclic hierarchy.", "D-EXCEL-MANAGER-DELEGATION-R15"),
    ("R15-7", "Synchronize canonical architecture, Excel operator instructions and UI/UX regression evidence.", "NOT_APPLICABLE"),
]
beneficiaries = ["çalışan", "İK/admin operatörü", "yönetici", "bakım geliştiricisi"]
req_items = [
    {
        "id": rid,
        "source": "REPOSITORY_POLICY" if rid == "R15-7" else "USER",
        "statement": statement,
        "beneficiaries": beneficiaries,
        "decision_id": decision_id,
        "disposition": "IN_SCOPE",
        "evidence": "User task and explicit follow-up approval" if rid != "R15-7" else "AGENTS.md documentation and UI evidence rules",
    }
    for rid, statement, decision_id in requirements
]

dims = {
    "FUNCTIONAL": (True, "All six requested behaviors change runtime business rules."),
    "EXPERIENCE": (True, "Validation, import/export and delegation are interactive workflows."),
    "COVERAGE": (True, "Eight approved pages, roles, invalid files, date boundaries and hierarchy states are in scope."),
    "QUALITY": (True, "The user requested user-friendly UI and complete validation feedback."),
    "CAPACITY": (True, "Spreadsheet batching introduces bounded file size, row count and memory use."),
    "OPERATIONS": (True, "Delegation uses startup and daily reconciliation with recovery."),
    "SECURITY_DATA": (True, "PII file export/import and approval authority require backend enforcement."),
    "REGRESSION": (True, "Manual CRUD and existing leave/employee workflows must remain intact."),
    "DOCUMENTATION": (True, "Operator Excel format and changed authority require canonical documentation."),
}
dimension_items = {
    key: {"required": required, "evidence": evidence}
    for key, (required, evidence) in dims.items()
}
acceptance = []
dimension_requirements = {
    "FUNCTIONAL": ["R15-1", "R15-2", "R15-3", "R15-4", "R15-5", "R15-6"],
    "EXPERIENCE": ["R15-1", "R15-2", "R15-3", "R15-4", "R15-5"],
    "COVERAGE": ["R15-1", "R15-3", "R15-4", "R15-5", "R15-6"],
    "QUALITY": ["R15-1", "R15-2", "R15-3", "R15-5"],
    "CAPACITY": ["R15-1"],
    "OPERATIONS": ["R15-1", "R15-5"],
    "SECURITY_DATA": ["R15-1", "R15-4", "R15-5", "R15-6"],
    "REGRESSION": ["R15-1", "R15-2", "R15-3", "R15-4", "R15-5", "R15-6"],
    "DOCUMENTATION": ["R15-7"],
}
for dimension, req_ids in dimension_requirements.items():
    acceptance.append(
        {
            "id": f"A15-{dimension}",
            "dimension": dimension,
            "requirement_ids": req_ids,
            "baseline": "Revision-14 runtime has manual personnel CRUD, manual employee manager selection, no XLSX service and no delegation worker.",
            "target": "The approved revision-15 behavior is complete with one authority and no regression.",
            "criterion": f"Focused gates and the combined live runtime scenario prove {dimension.lower()} acceptance for the mapped requirements.",
            "validation_ids": ["FG1", "FG2", "FG3", "E2E1"] if dimension != "DOCUMENTATION" else ["FG1", "FG2"],
            "result": "PLANNED",
            "evidence": "Planned revision-15 validation.",
        }
    )
overrides["outcome_closure"] = {
    "requirements": req_items,
    "dimensions": dimension_items,
    "acceptance": acceptance,
    "demand_envelope": {
        "applicable": True,
        "normal_load": "Up to 500 XLSX data rows in one authorized request.",
        "peak_load": "One 5 MiB workbook with 2,000 data rows.",
        "failure_recovery_load": "Repeated identical import and worker reconciliation remain idempotent and create no partial writes.",
        "evidence": "Explicit bounded import contract and idempotent worker acceptance.",
    },
    "evidence": "User approved the exact scope and lifecycle in the follow-up response.",
    "overall_result": "PLANNED",
}

for gate in overrides["validation"]["fast_gates"]:
    gate["result"] = "PLANNED"
    gate["evidence"] = "Planned revision-15 validation."
    if gate["id"] == "FG1":
        gate["covers"] = "XLSX schemas and limits, required fields, department hierarchy, delegation lifecycle, authorization and regressions"
    elif gate["id"] == "FG2":
        gate["covers"] = "Full application and Razor integration compile"
    elif gate["id"] == "FG3":
        gate["covers"] = "EF migration/model alignment for department manager and delegation"
scenario = overrides["validation"]["comprehensive_scenarios"][0]
scenario.update(
    {
        "coverage": (
            "Authorized XLSX import/export on all approved routes; duplicate/invalid atomic rejection; "
            "department-required employee validation; required personnel forms; department manager "
            "hierarchy; approved-leave delegation activation, pending approval reassignment and restoration; "
            "manual CRUD; responsive and browser-error coverage."
        ),
        "evidence_plan": "Run all revision-15 workflows on the live Blazor Server application backed by isolated MSSQL.",
        "result": "PLANNED",
        "evidence": "Planned revision-15 combined E2E/live-browser validation.",
        "proof_artifact": {
            "base": "REPOSITORY",
            "path": ".bet-task/evidence/employee-information-r15-e2e.json",
            "scenario_sha256": "PLANNED",
        },
    }
)
overrides["validation"]["remediation"] = {"performed": False, "batches": []}
for policy in overrides["validation"]["policy_dependencies"].values():
    policy["validation_ids"] = ["E2E1", "FG1", "FG2", "FG3"]

overrides["entity_mapping"] = {
    "touched": True,
    "evidence": "Department manager and delegation relationships require new attribute-first FK/navigation mapping and minimal Fluent index/delete configuration.",
    "fluent_exceptions": [],
    "verification": {
        "plan": "Verify FK scalars/navigations, uniqueness, delete behavior, migration and snapshot without duplicated mapping.",
        "result": "PLANNED",
        "evidence": "Planned FG1 and FG3.",
    },
}
for check in overrides["security_closeout"]["checks"]:
    check["result"] = "PLANNED"
    check["evidence"] = "Planned revision-17 security validation."
    check["applicable"] = True

overrides["material_deltas"] = [
    item for item in overrides["material_deltas"] if item["revision"] not in (15, 16, 17)
]
overrides["material_deltas"].append(
    {
        "revision": 15,
        "change": (
            "Add bounded atomic XLSX import/export, shared required-field validation, department-owned "
            "manager hierarchy and approved-leave delegation with startup/daily reconciliation."
        ),
        "evidence": "FOLLOWUP_USER_APPROVAL: her şeye evet ve onaylıyorum",
    }
)
overrides["material_deltas"].append(
    {
        "revision": 16,
        "change": (
            "Close the implementation scope against the exact generated migration, shared Excel "
            "component, runtime registration/endpoint, audit enum and regression test paths; add "
            "visible required-field indicators discovered by live responsive UI validation."
        ),
        "evidence": "IMPLEMENTATION_DISCOVERY: exact changed-path closure and live UI regression evidence.",
    }
)
overrides["material_deltas"].append(
    {
        "revision": 17,
        "change": (
            "Remediate independent-review findings: require delegation for every manager leave "
            "including elevated entry, route top-level managers directly to HR review, protect "
            "parent hierarchy and assigned delegates, fail-closed/backfill the legacy manager "
            "cutover, reject external relationships across all XLSX parts, and extend real E2E."
        ),
        "evidence": "INDEPENDENT_REVIEW: /root/personnel_info_review initial score 3/10 with six P1 and one P2 findings.",
    }
)

source["checkpoint"] = {
    "phase": "IMPLEMENTATION",
    "completed_step_ids": ["DISCOVERY"],
    "pending_step_ids": ["IMPLEMENTATION", "VALIDATION", "REVIEW", "FINAL_CLOSEOUT"],
    "invalidated_validation_ids": ["E2E1", "FG1", "FG2", "FG3"],
    "active_remediation_batch_id": None,
}
overrides["review"]["focus"] = [
    "Atomic and bounded XLSX parsing without partial writes or PII leakage",
    "One department-owned manager authority with correct parent hierarchy",
    "Idempotent approved-leave delegation activation and restoration",
    "Pending approval reassignment and negative authorization paths",
    "Shared required-field validation for manual entry and import",
    "Responsive, consistent import/export and validation UI",
]
overrides["review"]["lifecycle"]["state"] = "E2E_EVIDENCE_PENDING"
overrides["review"]["lifecycle"]["evidence"] = (
    "Revision 15 changes the attestation surface and comprehensive scenario; the established "
    "reviewer lifecycle is retained while current runtime evidence is pending."
)

new_subjects = [
    "department.manager-authority",
    "department.manager-hierarchy",
    "employee.required-fields",
    "excel.atomic-import-export",
    "leave.manager-delegation",
]
source["decision_control"]["documentation_coverage"] = [
    item for item in source["decision_control"]["documentation_coverage"]
    if not set(item["subject_ids"]).intersection(new_subjects)
]
source["decision_control"]["documentation_coverage"].append(
    {
        "path": "Docs/phase-1-architecture.md",
        "subject_ids": [
            "department.manager-authority",
            "department.manager-hierarchy",
            "employee.required-fields",
            "leave.manager-delegation",
        ],
        "result": "CONSISTENT",
        "sha256": f"sha256:{hashlib.sha256((Path('/Users/egesoykara/Desktop/IK') / 'Docs/phase-1-architecture.md').read_bytes()).hexdigest()}",
        "evidence": "Canonical architecture now records manager, validation and worker authority.",
    }
)
source["decision_control"]["documentation_coverage"].append(
    {
        "path": "Docs/personnel-excel-import-export.md",
        "subject_ids": ["excel.atomic-import-export"],
        "result": "CONSISTENT",
        "sha256": f"sha256:{hashlib.sha256((Path('/Users/egesoykara/Desktop/IK') / 'Docs/personnel-excel-import-export.md').read_bytes()).hexdigest()}",
        "evidence": "Operator documentation records exact workbook schemas, limits and atomic failure behavior.",
    }
)
source["change_control"]["receipt_sha256"] = {
    validation_id: "PLANNED" for validation_id in ["E2E1", "FG1", "FG2", "FG3"]
}
source["change_control"]["current_snapshot_sha256"] = "PLANNED"
source["change_control"]["unrelated_baseline_sha256"] = "sha256:0a749d70bffd6d202e06c8ce0688ad590f7d7f662ea60662088e215c3ac82f60"
source["manifest"]["sha256"] = "PLANNED"

SOURCE_PATH.write_text(json.dumps(source, ensure_ascii=False, indent=2) + "\n")
