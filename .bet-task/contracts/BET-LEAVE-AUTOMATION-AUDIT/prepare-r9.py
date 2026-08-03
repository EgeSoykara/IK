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
SOURCE_PATH = CONTRACT_DIR / "source-r8.json"
OUTPUT_PATH = CONTRACT_DIR / "source-r9.json"
CONTRACT_PATH = CONTRACT_DIR / "contract.json"

sys.dont_write_bytecode = True
sys.path.insert(0, str(SKILL_ROOT / "scripts"))

from bet_contract import api as contract_api  # noqa: E402
from bet_contract.change_rules import live_change_state  # noqa: E402
from bet_contract.decisions import enrich_decision  # noqa: E402
from build_task_contract import build_contract  # noqa: E402


source = contract_api.loads_json(SOURCE_PATH.read_text(encoding="utf-8"))
contract = contract_api.loads_json(CONTRACT_PATH.read_text(encoding="utf-8"))
overrides = source["overrides"]

revision_path = CONTRACT_DIR / "revisions/8.json"
revision_path.parent.mkdir(parents=True, exist_ok=True)
contract_api.atomic_write_json(revision_path, contract, compact=False)

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

overrides["contract_revision"] = 9
overrides["status"] = "READY"
overrides["repository"]["git_head"] = subprocess.check_output(
    ["git", "rev-parse", "HEAD"], cwd=ROOT, text=True
).strip()
overrides["task"]["outcome"] = (
    "İzin günlerini yalnız tam veya yarım gün hassasiyetinde tutmak; günlük worker ile "
    "yeni çalışan ve yıl içi kıdem hak edişlerini gün esaslı ve tam güne aşağı "
    "yuvarlanmış biçimde güncellemek; izin taleplerini yıllık/hastalık kategorilerine "
    "indirgemek; yıllık izni önce devirden sonra normal haktan düşmek; hamilelik iznini "
    "yalnız adminin manuel atamasına bırakmak; devir uyarılarını ayrı admin ekranında "
    "yönetmek; izin türü kimliklerini pozitif identity yapmak ve kişisel bilgi öz "
    "servisini ad, soyad ve KKTC kimlik numarasıyla genişletmek."
)

new_files = {
    "Components/Pages/EmployeePersonnelInformation.razor",
    "Components/Pages/LeaveCarryOverWarnings.razor",
    "Components/Pages/LeaveRequests.razor",
    "Components/Pages/LeaveRequestApprovals.razor",
    "Components/Pages/LeaveRequestTracking.razor",
    "Components/Layout/NavMenu.razor",
    "Database/HumanResourcesDbContext.cs",
    "Database/001_create_human_resources_schema.sql",
    "Docs/leave-entitlement-policy.md",
    "Docs/phase-1-architecture.md",
    "Models/LeaveBalance.cs",
    "Models/LeaveCarryOverWarning.cs",
    "Models/LeaveRequest.cs",
    "Models/LeaveRequestBalanceAllocation.cs",
    "Models/LeaveRequestCategory.cs",
    "Models/LeaveType.cs",
    "Program.cs",
    "README.md",
    "Services/AnnualLeaveEntitlementWorker.cs",
    "Services/LeaveBalanceService.cs",
    "Services/LeaveCarryOverWarningService.cs",
    "Services/LeaveEntitlementService.cs",
    "Services/LeaveRequestService.cs",
    "Tests/EmployeePersonnelInformationTests.cs",
    "Tests/LeaveCarryOverWarningServiceTests.cs",
    "Tests/LeaveEntitlementServiceTests.cs",
    "Tests/LeaveRequestServiceTests.cs",
    "Tests/ManagementUiContractTests.cs",
}
overrides["scope"]["diff_base"] = overrides["repository"]["git_head"]
overrides["scope"]["files"] = sorted(set(overrides["scope"]["files"]) | new_files)
overrides["scope"]["contracts"] = sorted(
    set(overrides["scope"]["contracts"])
    | {
        "Half-day numeric granularity and validation contract",
        "Daily/new-hire entitlement reconciliation contract",
        "Annual/sickness leave-request category and allocation contract",
        "Carry-over warning administration contract",
        "Positive leave-type identity cutover contract",
        "Employee personnel self-service field contract",
    }
)
overrides["scope"]["surfaces"] = sorted(
    set(overrides["scope"]["surfaces"])
    | {
        "/EmployeePersonnelInformation",
        "/LeaveCarryOverWarnings",
        "/LeaveRequests",
        "/LeaveRequestApprovals",
        "/LeaveRequestTracking",
    }
)
overrides["scope"]["unrelated_dirty_files"] = []

overrides["applicability"]["background_worker"]["evidence"] = (
    "Hak ediş otoritesi her yerel takvim gününde yeni çalışanları ve kıdem geçişlerini "
    "yeniden uzlaştırmalıdır."
)
overrides["applicability"]["database"]["evidence"] = (
    "Talep kategorisi/allocation, devir uyarısı, pozitif kimlik cutover'ı ve yarım-gün "
    "kısıtları EF/MSSQL şemasını değiştirir."
)
overrides["applicability"]["database_mapping"]["evidence"] = (
    "Yarım-gün ve pozitif kimlik SQL check constraint'leri ile yeni ilişkiler "
    "attribute-first mapping doğrulaması gerektirir."
)
overrides["applicability"]["security_boundary"]["evidence"] = (
    "Devir uyarısı yönetimi ve personel öz-servis alanları backend yetki/scope sınırıdır."
)
overrides["applicability"]["ui_live_browser"]["evidence"] = (
    "İki kategorili talep, devir uyarı paneli, yarım-gün kontrolleri ve kişisel bilgi "
    "alanları gerçek tarayıcı kanıtı gerektirir."
)

overrides["invariants"] = [
    item
    for item in overrides["invariants"]
    if "ay esaslı" not in item and "Hamilelik izni otomatik" not in item
] + [
    "İzin kota, bakiye, kullanım ve talep günleri negatif olamaz ve yalnız 0,5 gün katı olabilir.",
    "Otomatik prorasyon takvim günü oranıyla hesaplanır ve aşağı yönde tam güne yuvarlanır.",
    "Worker her yerel takvim gününde çalışır; StartDate gününde yeni çalışanın yıl sonuna kadar hak edişini oluşturur veya uzlaştırır.",
    "Otomatik hak ediş yalnız kıdem kademeleri ve hastalık iznini kapsar; hamilelik izni yalnız yetkili adminin manuel atamasıdır.",
    "Çalışan talebi yalnız Yıllık İzin veya Hastalık İzni kategorisidir; yıllık kategori kıdem bakiyelerinin toplamını gösterir.",
    "Yıllık izin tüketimi önce devreden günlerden, sonra normal hak edişten; her kaynakta 0-10, 10-20, 20-30+ sıra düzeniyle ve kalıcı allocation kaydıyla yapılır.",
    "Devir sonucu 50 günlük uyarı eşiğini aşan bakiye ayrı kalıcı uyarı kaydı üretir; admin pending/acknowledged durumunu backend yetkisiyle yönetir.",
    "LeaveTypeId pozitif SQL identity anahtarıdır; varsayılan beş tür temiz kurulumda 1-5 kimliklerini kullanır ve negatif kimlik kalmaz.",
    "Çalışan kendi ad, soyad, KKTC kimlik numarası, cinsiyet ve kan grubunu; elevated kullanıcı mevcut personel scope kuralıyla başkasını düzenler.",
]

decisions = copy.deepcopy(contract["confirmed_decisions"])
new_decisions = [
    {
        "id": "D-LEAVE-HALF-DAY-GRANULARITY-R9",
        "kind": "PRODUCT_BEHAVIOR",
        "decision": "All leave quotas, balances, usages and requests are non-negative multiples of 0.5 day; half-day requests remain supported, while automatic proration rounds down to a whole integer day.",
        "user_confirmed": True,
        "subject_ids": ["leave.numeric-granularity", "leave.proration-rounding"],
        "evidence": "FOLLOWUP_USER_APPROVAL: yarım günlük izinler kalsın. Çalışan izinleri 30 gün, 30.5 gün, 31 gün gibi olsun, 0.5 dışındaki ondalık sayılar olmasın; dediğin gibi yap, yuvarla ve tam sayı şeklinde hesapla",
    },
    {
        "id": "D-LEAVE-DAILY-ONBOARDING-R9",
        "kind": "PRODUCT_BEHAVIOR",
        "decision": "The entitlement worker reconciles every local calendar day; a new employee is granted a calendar-day prorated, whole-day-rounded-down entitlement from StartDate through December 31 without waiting for January 1.",
        "user_confirmed": True,
        "subject_ids": ["leave.worker.daily-reconciliation", "leave.new-hire-proration"],
        "evidence": "CURRENT_USER_REQUEST: İzinler background worker ile her yeni günde kontrol edilip güncellenmeli. O yıl içinde yeni gelen çalışanlar için 1 ocağı beklemek yerine geldiği gibi yıl sonuna kadar olan süre için izni hesaplanmalı.",
    },
    {
        "id": "D-LEAVE-REQUEST-CATEGORIES-R9",
        "kind": "PRODUCT_BEHAVIOR",
        "decision": "Employees request only Annual Leave or Sickness Leave. Annual Leave aggregates all service-tier balances and deducts persisted allocations from carry-over first, then entitlement, in 0-10, 10-20, 20-30-plus tier order.",
        "user_confirmed": True,
        "subject_ids": ["leave.request.categories", "leave.request.deduction-order"],
        "evidence": "FOLLOWUP_USER_APPROVAL: Dediğin gibi önce devreden kısımdan düşürülsün, ardından normal izinden düşürülsün",
    },
    {
        "id": "D-LEAVE-PREGNANCY-MANUAL-R9",
        "kind": "PRODUCT_BEHAVIOR",
        "decision": "Pregnancy leave is never assigned by the automatic entitlement worker; an authorized administrator assigns it manually and female eligibility remains enforced.",
        "user_confirmed": True,
        "subject_ids": ["leave.pregnancy.manual-assignment"],
        "evidence": "CURRENT_USER_REQUEST: Hamilelik izni otomatik atanmasın, manuel şekilde admin atasın onu.",
    },
    {
        "id": "D-LEAVE-CARRYOVER-WARNINGS-R9",
        "kind": "PRODUCT_DESIGN",
        "decision": "Carry-over limit warnings have a separate admin panel that shows carried, current entitlement and total days per employee and supports an explicit acknowledgement workflow.",
        "user_confirmed": True,
        "subject_ids": ["leave.carry-over.warning-management"],
        "evidence": "CURRENT_USER_REQUEST: izin devrinde uyarı veren çalışanlar için admin panelinde ayrı bir panel olsun ve bu çalışanları admin o yeni sayfada yönetsin",
    },
    {
        "id": "D-LEAVE-POSITIVE-TYPE-IDS-R9",
        "kind": "CUTOVER",
        "decision": "LeaveTypeId is a positive SQL identity key; the five default types use IDs 1 through 5 on clean installations and no negative leave-type identity remains.",
        "user_confirmed": True,
        "subject_ids": ["leave-type.positive-identity"],
        "evidence": "CURRENT_USER_REQUEST: İZİN TÜRÜ ID negatif sayı olamaz. 1 den başlayıp kaç tür varsa 2, 3 gibi devam etmeli ve pozitif tam sayı olmalıdır.",
    },
    {
        "id": "D-PERSONAL-SELF-EDIT-R9",
        "kind": "PRODUCT_BEHAVIOR",
        "decision": "The existing personnel-information scope also allows editing first name, last name and KKTC identity number alongside gender and blood group, without widening who may edit which employee.",
        "user_confirmed": True,
        "subject_ids": ["employee.self-service.editable-fields"],
        "evidence": "CURRENT_USER_REQUEST: kişisel bilgilerde çalışan sadece cinsiyet ve kan grubunu değiştiriyor şuan, isim soyismi kimlik no da değiştirilebilsin.",
    },
]
for decision in new_decisions:
    enrich_decision(decision, revision=9)
    decisions.append(decision)
overrides["confirmed_decisions"] = decisions

source["decision_control"]["documentation_coverage"] = [
    {
        "path": "Docs/leave-entitlement-policy.md",
        "subject_ids": sorted(
            {
                subject
                for item in decisions
                for subject in item["subject_ids"]
                if subject.startswith("leave")
            }
        ),
        "result": "UPDATE_REQUIRED",
        "sha256": "PLANNED",
        "evidence": "Canonical leave policy must describe the daily worker, numeric granularity, request categories, deduction order, manual pregnancy assignment, warnings and positive IDs.",
    },
    {
        "path": "Docs/phase-1-architecture.md",
        "subject_ids": ["employee.self-service.editable-fields"],
        "result": "UPDATE_REQUIRED",
        "sha256": "PLANNED",
        "evidence": "Canonical architecture must bind the expanded fields to the unchanged self/elevated backend scope authority.",
    },
]

requirements = copy.deepcopy(contract["outcome_closure"]["requirements"])
replacement_statements = {
    "R-WORKER": "Her yerel takvim gününde aktif çalışan hak edişlerini uzlaştıran; yeni çalışanı StartDate gününde yıl sonuna kadar hesaplayan idempotent worker sağla.",
    "R-PRORATION": "Yeni giriş ve yıl içi kıdem geçişini takvim günü oranıyla hesapla ve otomatik sonucu aşağı yönde tam güne yuvarla.",
}
for requirement in requirements:
    if requirement["id"] in replacement_statements:
        requirement["statement"] = replacement_statements[requirement["id"]]
new_requirements = [
    ("R-HALF-DAY-GRANULARITY", "İzin günlerini negatif olmayan 0,5 gün katlarıyla sınırla; yarım günlük talebi koru."),
    ("R-REQUEST-CATEGORIES", "Talep ekranında yalnız Yıllık İzin ve Hastalık İzni sun; yıllık bakiyeleri birleştir ve devirden başlayarak düş."),
    ("R-PREGNANCY-MANUAL", "Hamilelik iznini otomatik atama; kadın çalışan için admin manuel ataması olarak bırak."),
    ("R-CARRYOVER-WARNINGS", "Devir uyarısı veren çalışanları carried/entitled/total değerleri ve yönetim durumuyla ayrı admin panelinde göster."),
    ("R-POSITIVE-TYPE-IDS", "LeaveTypeId değerlerini pozitif identity yap; negatif varsayılan kimlikleri tamamen kaldır."),
    ("R-PERSONAL-SELF-EDIT", "Kişisel bilgi düzenlemesine ad, soyad ve KKTC kimlik numarasını ekle; mevcut scope otoritesini koru."),
]
for requirement_id, statement in new_requirements:
    requirements.append(
        {
            "id": requirement_id,
            "source": "USER",
            "statement": statement,
            "beneficiaries": ["çalışan", "admin"],
            "disposition": "IN_SCOPE",
            "decision_id": next(
                item["id"]
                for item in new_decisions
                if requirement_id.split("R-")[-1].split("-")[0].lower()
                in item["id"].lower()
            ) if requirement_id == "R-PREGNANCY-MANUAL" else "NOT_APPLICABLE",
            "evidence": "CURRENT_USER_REQUEST",
        }
    )
outcome = copy.deepcopy(contract["outcome_closure"])
outcome["requirements"] = requirements
for acceptance in outcome["acceptance"]:
    acceptance["result"] = "PLANNED"
    acceptance["evidence"] = "Revision-9 implementation and validation pending."
functional = next(item for item in outcome["acceptance"] if item["id"] == "A-FUNCTIONAL")
functional["requirement_ids"] = sorted(
    set(functional["requirement_ids"]) | {item[0] for item in new_requirements}
)
for acceptance_id, requirement_ids in {
    "A-EXPERIENCE": {"R-REQUEST-CATEGORIES", "R-CARRYOVER-WARNINGS", "R-PERSONAL-SELF-EDIT"},
    "A-COVERAGE": {"R-HALF-DAY-GRANULARITY", "R-REQUEST-CATEGORIES", "R-PREGNANCY-MANUAL"},
    "A-QUALITY": {"R-HALF-DAY-GRANULARITY", "R-CARRYOVER-WARNINGS", "R-PERSONAL-SELF-EDIT"},
    "A-OPERATIONS": {"R-CARRYOVER-WARNINGS"},
    "A-SECURITY": {"R-CARRYOVER-WARNINGS", "R-PERSONAL-SELF-EDIT"},
    "A-REGRESSION": {"R-HALF-DAY-GRANULARITY", "R-REQUEST-CATEGORIES"},
    "A-DOCS": {"R-REQUEST-CATEGORIES", "R-CARRYOVER-WARNINGS", "R-POSITIVE-TYPE-IDS"},
}.items():
    acceptance = next(item for item in outcome["acceptance"] if item["id"] == acceptance_id)
    acceptance["requirement_ids"] = sorted(set(acceptance["requirement_ids"]) | requirement_ids)
outcome["overall_result"] = "PLANNED"
outcome["evidence"] = "Revision-9 implementation and all validation evidence are pending."
outcome["demand_envelope"]["normal_load"] = "Daily reconciliation of all active employees and automatic leave types, plus request-allocation and carry-over warning reads."
outcome["demand_envelope"]["peak_load"] = "Worker startup/daily overlap, concurrent approvals and warning acknowledgement on the same persisted balances."
overrides["outcome_closure"] = outcome

validation = copy.deepcopy(contract["validation"])
for item in validation["fast_gates"] + validation["comprehensive_scenarios"]:
    item["result"] = "PLANNED"
    item["evidence"] = "Revision-9 changes invalidate the prior receipt."
focused = next(item for item in validation["fast_gates"] if item["id"] == "FG-LEAVE-TESTS")
focused["command"] = "dotnet test Tests/IK.Web.Tests.csproj -c Release --no-restore --filter \"FullyQualifiedName~LeaveEntitlementServiceTests|FullyQualifiedName~LeaveRequestServiceTests|FullyQualifiedName~LeaveCarryOverWarningServiceTests|FullyQualifiedName~EmployeePersonnelInformationTests|FullyQualifiedName~ManagementUiContractTests\" --disable-build-servers"
focused["covers"] = "0,5 granularity; calendar-day proration; daily worker; request aggregation/allocation; warning authorization; positive IDs; personnel self-service fields"
e2e = validation["comprehensive_scenarios"][0]
e2e["coverage"] = "İki kategorili talep ve yarım gün; günlük/new-hire worker; devir uyarı yönetimi; pozitif tür ID; kişisel bilgi alanları; yetkili/yetkisiz responsive gerçek tarayıcı akışı"
e2e["entrypoint"] = "Gerçek MSSQL-backed Blazor Server leave request, carry-over warning, personnel information and leave-type routes"
e2e["proof_artifact"]["scenario_sha256"] = "PLANNED"
validation["remediation"] = {"performed": False, "batches": []}
overrides["validation"] = validation
source["change_control"]["receipt_sha256"] = {
    item["id"]: "PLANNED"
    for item in validation["fast_gates"] + validation["comprehensive_scenarios"]
}

security = copy.deepcopy(contract["security_closeout"])
for check in security["checks"]:
    check["result"] = "PLANNED"
    check["evidence"] = "Revision-9 security validation pending."
overrides["security_closeout"] = security
overrides["entity_mapping"] = {
    "touched": True,
    "attribute_first": True,
    "semantic_duplication": False,
    "evidence": "Data annotations remain the scalar authority; half-day and positive-key database invariants require minimal SQL check constraints.",
    "fluent_exceptions": [
        {
            "mapping": "Half-day multiples and positive LeaveType identity check constraints",
            "attribute_support": "NOT_EXPRESSIBLE",
            "limitation": "DataAnnotations cannot emit SQL modulo/check expressions or constrain an identity-generated primary key.",
            "minimal_fluent_scope": "Only non-negative 0.5-step decimal and positive LeaveTypeId HasCheckConstraint expressions.",
            "evidence": "Revision-9 migration, model snapshot and clean-install SQL validation pending.",
        }
    ],
    "verification": {
        "plan": "Verify attributes, minimal Fluent constraints, migration, snapshot and clean-install SQL are semantically identical.",
        "result": "PLANNED",
        "evidence": "Revision-9 migration validation pending.",
    },
}

review = copy.deepcopy(contract["review"])
review["focus"] = sorted(
    set(review["focus"])
    | {
        "Half-day database/application/UI invariant",
        "Daily worker and new-hire calendar-day reconciliation",
        "Annual aggregate deduction ordering and persisted allocations",
        "Carry-over warning lifecycle and authorization",
        "Positive identity data-preserving cutover",
        "Personnel self-service PII validation and scope",
    }
)
review["lifecycle"]["state"] = "DELTA_REVIEW_IN_PROGRESS"
review["lifecycle"]["evidence"] = "Revision-9 material delta requires the same independent reviewer after implementation and validation."
overrides["review"] = review

overrides.setdefault("material_deltas", []).append(
    {
        "revision": 9,
        "change": "Replace annual-only scheduling and per-type requests with daily reconciled entitlements, half-day numeric authority, two request categories with allocation ordering, manual pregnancy assignment, carry-over warning management, positive type IDs and expanded personnel self-service fields.",
        "evidence": "Current user request and explicit follow-up approval resolve granularity, rounding and deduction order; user directed implementation without further questions.",
    }
)

overrides["authority_map"] = copy.deepcopy(contract["authority_map"])
extra_authorities = [
    ("Models/LeaveRequest.cs", "LeaveRequest", "İzin talebi veri sözleşmesi ve mevcut LeaveType bağlantısı"),
    ("Services/LeaveRequestService.cs", "CreateRequestAsync and HumanResourcesDecisionAsync", "Talep oluşturma ve onay tüketim otoritesi"),
    ("Components/Pages/LeaveRequests.razor", "LeaveRequests route", "Çalışan talep kategorisi ve yarım-gün kullanıcı yüzeyi"),
    ("Components/Pages/EmployeePersonnelInformation.razor", "EmployeePersonnelInformation route", "Kişisel bilgi öz-servis düzenleme yüzeyi ve backend scope kontrolü"),
    ("Components/Layout/NavMenu.razor", "NavMenu", "Admin devir uyarısı rota görünürlüğü"),
]
known_paths = {item["path"] for item in overrides["authority_map"]}
for path, symbol, reason in extra_authorities:
    if path not in known_paths:
        overrides["authority_map"].append(
            {"path": path, "symbol": symbol, "reason": reason, "sha256": "PLANNED"}
        )
for authority in overrides["authority_map"]:
    authority_path = ROOT / authority["path"]
    if authority_path.is_file():
        authority["sha256"] = "sha256:" + hashlib.sha256(authority_path.read_bytes()).hexdigest()

source["checkpoint"] = {
    "phase": "IMPLEMENTATION",
    "completed_step_ids": ["DISCOVERY"],
    "pending_step_ids": ["IMPLEMENTATION", "VALIDATION", "REVIEW", "FINAL_CLOSEOUT"],
    "invalidated_validation_ids": sorted(source["change_control"]["receipt_sha256"]),
    "active_remediation_batch_id": None,
}

# Derived change-control hashes must be calculated from the fully compiled
# revision, not from the compact source's override subset.
compiled = build_contract(source, SKILL_ROOT, base_contract=contract)
_, _, live_snapshot, unrelated_baseline, _ = live_change_state(compiled)
source["change_control"]["current_snapshot_sha256"] = live_snapshot
source["change_control"]["unrelated_baseline_sha256"] = unrelated_baseline

contract_api.atomic_write_json(OUTPUT_PATH, source, compact=False)
print(json.dumps({"result": "WRITTEN", "path": str(OUTPUT_PATH)}, ensure_ascii=False))
