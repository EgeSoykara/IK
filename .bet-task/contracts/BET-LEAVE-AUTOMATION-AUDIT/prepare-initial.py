from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path


ROOT = Path("/Users/deniz/Desktop/IK")
SKILL_ROOT = Path("/Users/deniz/.codex/skills/bet-task")
OUTPUT = ROOT / ".bet-task/contracts/BET-LEAVE-AUTOMATION-AUDIT/contract.json"

sys.dont_write_bytecode = True
sys.path.insert(0, str(SKILL_ROOT / "scripts"))

from bet_contract import api as contract_api  # noqa: E402
from bet_contract.decisions import enrich_blocker, enrich_decision  # noqa: E402


def digest(path: str) -> str:
    return "sha256:" + hashlib.sha256((ROOT / path).read_bytes()).hexdigest()


contract = contract_api.build_template(SKILL_ROOT)
contract["contract_id"] = "BET-LEAVE-AUTOMATION-AUDIT"
contract["contract_revision"] = 1
contract["status"] = "BLOCKED"
contract["repository"] = {
    "root": str(ROOT),
    "git_head": "4415aece3c05f6e8fbf95760eb3ee8f80de93d2b",
}
contract["policy"]["repo_policy_path"] = str(ROOT / "AGENTS.md")
contract["policy"]["repo_policy_sha256"] = digest("AGENTS.md")
contract["task"] = {
    "outcome": (
        "Denetim kayıtlarını operatör odaklı sadeleştirmek; varsayılan ve "
        "uygunluk kurallı izin türlerini tanımlamak; her yıl 1 Ocak'ta "
        "kıdem/cinsiyet esaslı ve geçiş yılı oranlamalı izin hak edişlerini "
        "idempotent worker ile atamak; tüm takvimleri Türkçeleştirmek; izin "
        "bakiyesi yönetiminde kişi, departman ve tüm çalışan kapsamlarını sunmak."
    ),
    "explicit_exclusions": [
        "Denetim izinin EntityName/EntityId verisini veritabanından silmek",
        "Resmî tatil veya hafta sonunu izin talebi gün hesabına dahil etmek",
        "Mevcut izin onay, yönetici vekâleti veya çalışan erişim otoritesini değiştirmek",
        "Kullanıcının vermediği izin kota değerlerini veya 30 yıl üstü politikasını tahmin etmek",
        "İlgisiz UI yeniden tasarımı, kimlik doğrulama değişikliği veya harici entegrasyon",
    ],
}
contract["delivery"] = {
    "phase": "DEVELOPMENT",
    "user_selected": True,
    "selection_evidence": "CURRENT_USER_REQUEST: aşama: DEVELOPMENT",
    "selection_decision_id": "D-PHASE-DEVELOPMENT-LEAVE-R1",
    "cutover": {
        "strategy": "CLEAN_REMOVE",
        "compatibility_decision": "FORBIDDEN",
        "compatibility_evidence": (
            "CURRENT_USER_REQUEST: aşama: DEVELOPMENT; yeni izin hak ediş "
            "otoritesi temiz geçişle uygulanacak, paralel eski/yeni worker veya fallback olmayacak."
        ),
        "compatibility_paths": [],
        "data_reset": {
            "planned": False,
            "scope": "Veri sıfırlama veya silme yok",
            "target_environment": "NOT_APPLICABLE",
            "environment_evidence": "Görev mevcut izin ve denetim verisini silmeyi istemiyor",
            "related_data_handling": "Mevcut kayıtlar korunur; yalnız yeni şema/migration eklenir",
        },
    },
}
contract["confirmed_decisions"] = [
    {
        "id": "D-PHASE-DEVELOPMENT-LEAVE-R1",
        "kind": "DELIVERY_PHASE",
        "decision": "DEVELOPMENT",
        "user_confirmed": True,
        "subject_ids": ["delivery.phase"],
        "evidence": "CURRENT_USER_REQUEST: aşama: DEVELOPMENT",
        "confirmation_source": "CURRENT_USER_REQUEST",
        "confirmation_artifact": None,
    }
]
contract["blockers"] = [
    {
        "id": "B-LEAVE-POLICY-NUMBERS",
        "question": (
            "0-10, 10-20, 20-30 yıllık çalışan; hastalık ve hamilelik izinlerinin "
            "yıllık gün sayılarını ve devir kurallarını; ayrıca 30 yıl ve üzeri "
            "çalışanın hangi kademeyi kullanacağını doğrulayın."
        ),
        "evidence": (
            "Kullanıcı beş izin türünü ve 10/20 yıl geçiş oranlamasını tanımladı; "
            "ancak yıllık kota değerleri, hastalık/hamilelik kotası, devir davranışı "
            "ve 30+ yıl kapsamı repo kodu, canonical dokümanlar veya aktif kararlarda yok. "
            "KKTC resmî iş mevzuatındaki kademeler de kullanıcının 0-10/10-20/20-30 "
            "şirket politikasından farklıdır; sayı uydurmak davranış ve hukuk riski yaratır."
        ),
    }
]
for item in contract["confirmed_decisions"]:
    enrich_decision(item, revision=1)
for item in contract["blockers"]:
    enrich_blocker(item, revision=1)

classes = [
    "api-data-contract",
    "backend-domain",
    "background-worker",
    "configuration-release",
    "database-persistence",
    "documentation-operator",
    "product-ui",
    "security-boundary",
    "ui-ux",
]
contract["classification"] = {
    "classes": classes,
    "validation_rows": classes,
    "integrated_module": {
        "required": True,
        "evidence": (
            "Blazor Server audit/leave pages, EF Core leave types and balances, "
            "employee start-date/gender data, public-holiday calculations, hosted worker, "
            "permission claims and operator documentation share one runtime workflow."
        ),
    },
}
contract["applicability"].update(
    {
        "background_worker": {
            "required": True,
            "evidence": "1 Ocak yıllık hak ediş ataması ve restart catch-up hosted worker gerektirir.",
        },
        "database": {
            "required": True,
            "evidence": "İzin türü uygunluk kuralları, varsayılan seed ve idempotent yıllık bakiyeler EF/MSSQL şemasını etkiler.",
        },
        "database_mapping": {
            "required": True,
            "evidence": "LeaveType uygunluk sütunları ve kısıtları attribute-first mapping doğrulaması gerektirir.",
        },
        "external_integration": {
            "required": False,
            "evidence": "Harici sağlayıcı, webhook veya uzak servis yoktur.",
        },
        "operational_readiness": {
            "required": True,
            "evidence": "Worker schedule, disable path, retry, idempotency, log ve iptal davranışı gereklidir.",
        },
        "security_boundary": {
            "required": True,
            "evidence": "Toplu bakiye ataması yalnız CanManageLeaveBalances backend otoritesiyle yapılmalıdır.",
        },
        "ui_design_stitch": {
            "required": False,
            "evidence": "Yeni görsel yön değil, mevcut management-shell/table/dialog deseninde rutin iyileştirme yapılır.",
        },
        "ui_live_browser": {
            "required": True,
            "evidence": "Audit tablosu, toplu hedef seçimi, autocomplete ve Türkçe takvimler gerçek tarayıcı kanıtı gerektirir.",
        },
        "ui_product_consistency": {
            "required": True,
            "evidence": "Dokunulan yönetim ekranları mevcut MudBlazor ürün dilini korumalıdır.",
        },
    }
)
authority = [
    ("Models/AuditLog.cs", "AuditLog", "Denetim veri izi ve EntityName/EntityId saklama otoritesi"),
    ("Services/AuditLogPageService.cs", "GetPageAsync", "Salt-okunur denetim sayfalama otoritesi"),
    ("Components/Pages/AuditLogs.razor", "AuditLogs route", "Denetim kullanıcı yüzeyi"),
    ("Models/LeaveType.cs", "LeaveType", "İzin türü kota/devir veri modeli"),
    ("Models/LeaveBalance.cs", "LeaveBalance", "Çalışan-tür-yıl benzersiz bakiye otoritesi"),
    ("Services/LeaveBalanceService.cs", "RenewAnnualBalanceAsync", "Yenileme, devir, transaction ve audit otoritesi"),
    ("Components/Pages/LeaveBalances.razor", "LeaveBalances route", "Manuel bakiye ve arama kullanıcı yüzeyi"),
    ("Models/Employee.cs", "Employee.StartDate and Gender", "Kıdem ve cinsiyet uygunluk kaynakları"),
    ("Services/LeaveDayCalculator.cs", "LeaveDayCalculator", "Hafta sonu dışlayan tek izin günü hesap otoritesi"),
    ("Services/PublicHolidayCalendar.cs", "PublicHolidayCalendar", "Tanımlı resmî tatil tarih otoritesi"),
    ("Services/ManagerDelegationWorker.cs", "ExecuteAsync", "Repo hosted-worker schedule/recovery/log örneği"),
    ("Program.cs", "service and middleware registration", "DI, worker ve kültür kayıt otoritesi"),
    ("Docs/phase-1-architecture.md", "Locked Decisions and Code Authority", "Canonical izin/audit mimari dokümanı"),
]
contract["authority_map"] = [
    {"path": path, "symbol": symbol, "reason": reason, "sha256": digest(path)}
    for path, symbol, reason in authority
]
contract["invariants"] = [
    "AuditLog EntityName ve EntityId verisi denetim bütünlüğü için korunur; yalnız UI sütunları kaldırılır.",
    "Denetim sayfası salt okunur ve CanViewAuditLogs backend sayfa erişim kararını korur.",
    "İzin talebi günleri yalnız LeaveDayCalculator ve PublicHolidayCalendar üzerinden Pazartesi-Cuma/resmî tatil dışı hesaplanır.",
    "Otomatik hak ediş yalnız aktif çalışanlarda StartDate ve Gender alanlarını kullanır; eksik veride sessiz fallback yapılmaz.",
    "Çalışan-izin türü-yıl benzersizliği ve tek transaction idempotent tekrar çalışmayı güvenli kılar.",
    "10 ve 20 yılın yıl içinde dolduğu yılda eski/yeni kademe payları kullanıcının Mart örneğine uygun ay esaslı bölünür.",
    "Hamilelik izni otomatik olarak yalnız Female çalışanlara atanır; manuel toplu işlem yetki ve uygunluk sonucunu açıkça bildirir.",
    "Toplu kişi/departman/tüm çalışan işlemi bir servis otoritesinde all-or-nothing yürür ve kısmi sessiz başarı bırakmaz.",
    "Bakiye ID kullanıcı yüzeyi ve arama sözleşmesinden tamamen kaldırılır; DB anahtarı iç otorite olarak kalır.",
    "Tüm MudBlazor tarih/takvim kontrolleri tr-TR kültürüyle ay ve gün adlarını Türkçe render eder.",
    "Attribute ile ifade edilebilen yeni mapping semantiği Fluent API ile tekrarlanmaz.",
]
contract["entity_mapping"] = {
    "touched": True,
    "attribute_first": True,
    "semantic_duplication": False,
    "fluent_exceptions": [],
    "evidence": "LeaveType uygunluk alanları ve mevcut LeaveBalance benzersiz indeksi attribute-first incelendi.",
    "verification": {
        "plan": "Yeni alanların required/range/index/check kapsamını migration, snapshot ve model testiyle doğrula.",
        "result": "PLANNED",
        "evidence": "Sayısal politika kararı sonrası implementation için planlandı.",
    },
}
scope_files = [
    "BetSolution/Docs/UX/ui-ux-regression-checklist.md",
    "Components/Pages/AuditLogs.razor",
    "Components/Pages/LeaveBalances.razor",
    "Components/Pages/LeaveTypes.razor",
    "Database/001_create_human_resources_schema.sql",
    "Database/HumanResourcesDbContext.cs",
    "Docs/leave-entitlement-policy.md",
    "Docs/phase-1-architecture.md",
    "Migrations/HumanResourcesDbContextModelSnapshot.cs",
    "Models/AuditActionType.cs",
    "Models/LeaveEntitlementKind.cs",
    "Models/LeaveType.cs",
    "Program.cs",
    "README.md",
    "Services/AnnualLeaveEntitlementWorker.cs",
    "Services/AnnualLeaveEntitlementWorkerOptions.cs",
    "Services/LeaveBalanceService.cs",
    "Services/LeaveEntitlementService.cs",
    "Tests/AuditLogPageServiceTests.cs",
    "Tests/LeaveEntitlementServiceTests.cs",
    "Tests/ManagementUiContractTests.cs",
]
contract["scope"] = {
    "diff_base": contract["repository"]["git_head"],
    "files": scope_files,
    "docs": [
        "BetSolution/Docs/UX/ui-ux-regression-checklist.md",
        "Docs/leave-entitlement-policy.md",
        "Docs/phase-1-architecture.md",
        "README.md",
    ],
    "surfaces": ["/AuditLogs", "/LeaveBalances", "/LeaveTypes"],
    "contracts": [
        "Audit read-model UI contract",
        "Leave-type eligibility and default seed contract",
        "Annual entitlement/proration/idempotency worker contract",
        "Bulk person/department/all balance assignment contract",
        "Turkish calendar localization contract",
        "EF Core/MSSQL migration contract",
    ],
    "unrelated_dirty_files": ["Services/StaticLoginService.cs", "appsettings.json"],
}
contract["validation"]["fast_gates"] = [
    {
        "id": "FG-LEAVE-TESTS",
        "kind": "unit",
        "command": (
            "dotnet test Tests/IK.Web.Tests.csproj -c Release --no-restore "
            "--filter \"FullyQualifiedName~LeaveEntitlementServiceTests|"
            "FullyQualifiedName~AuditLogPageServiceTests|FullyQualifiedName~ManagementUiContractTests\""
        ),
        "covers": "Kota uygunluğu, geçiş oranlaması, idempotency, toplu kapsam, audit ve UI sözleşmeleri",
        "result": "PLANNED",
        "evidence": "Sayısal politika kararı sonrası uygulanacak.",
    },
    {
        "id": "FG-ALL-TESTS",
        "kind": "unit",
        "command": "dotnet test IKSolution.slnx -c Release --no-restore",
        "covers": "Mevcut izin/onay/vekâlet/personel regresyonları",
        "result": "PLANNED",
        "evidence": "Implementation sonrası uygulanacak.",
    },
    {
        "id": "FG-BUILD",
        "kind": "build",
        "command": "dotnet build IKSolution.slnx -c Release --no-restore",
        "covers": "Razor, DI, worker ve çözüm derleme entegrasyonu",
        "result": "PLANNED",
        "evidence": "Implementation sonrası uygulanacak.",
    },
    {
        "id": "FG-MIGRATION",
        "kind": "database",
        "command": "dotnet ef migrations has-pending-model-changes --project IK.Web.csproj --no-build",
        "covers": "EF model, migration ve snapshot uyumu",
        "result": "PLANNED",
        "evidence": "Implementation sonrası uygulanacak.",
    },
]
contract["validation"]["comprehensive_scenarios"] = [
    {
        "id": "E2E-LEAVE-AUDIT",
        "baseline_id": "LEAVE-AUTOMATION-AUDIT-BASELINE",
        "kind": "combined-e2e-live-browser",
        "entrypoint": "Gerçek MSSQL-backed Blazor Server /AuditLogs, /LeaveBalances ve tarih seçici rotaları",
        "coverage": (
            "Yetkili/yetkisiz audit ve bakiye erişimi; kişi/departman/tüm çalışan toplu atama; "
            "uygunluk ve validation; Türkçe ay/gün adları; masaüstü/tablet/mobil/short-mobile/landscape; "
            "console/page/request hataları; persistence ve worker restart idempotency"
        ),
        "command": "bash .bet-task/scripts/run-leave-automation-audit-e2e.sh",
        "evidence_plan": "Disposable IK_E2E_ MSSQL veritabanında gerçek uygulama ve tarayıcı akışı; scenario JSON üret.",
        "real_runtime": True,
        "mock_only": False,
        "result": "PLANNED",
        "evidence": "Sayısal politika kararı sonrası uygulanacak.",
        "proof_artifact": {
            "base": "REPOSITORY",
            "path": ".bet-task/evidence/leave-automation-audit-e2e.json",
            "scenario_sha256": "PLANNED",
        },
    }
]
validation_ids = [
    "E2E-LEAVE-AUDIT",
    "FG-ALL-TESTS",
    "FG-BUILD",
    "FG-LEAVE-TESTS",
    "FG-MIGRATION",
]
contract["validation"]["parallel_safe"] = {
    "items": [],
    "evidence": "dotnet build/test aynı çıktı dizinlerini paylaşabileceği için sıralı çalıştırılır.",
}
contract["validation"]["ordered_shared_state"] = {
    "items": ["FG-MIGRATION", "E2E-LEAVE-AUDIT"],
    "evidence": "Migration/model kanıtı disposable gerçek runtime/browser baseline öncesinde tamamlanır.",
}
contract["validation"]["policy_dependencies"] = {
    source_id: {
        "mode": "ALL_CURRENT",
        "validation_ids": validation_ids,
        "evidence": f"{source_id} worker/UI/runtime sınırlarının tamamını etkiler.",
    }
    for source_id in (
        "conditional.operational",
        "conditional.ui-gates",
        "conditional.ui-quality",
    )
}
contract["validation"]["initial_baseline_not_applicable_evidence"] = (
    "Not applicable because a combined real MSSQL/runtime/browser baseline is planned."
)
requirements = [
    ("R-AUDIT", "Denetim kayıtları ekranından Varlık Adı ve Varlık ID alanlarını kaldır ve ekranı okunabilir biçimde düzenle.", ["İK operatörü", "denetçi"]),
    ("R-DEFAULT-TYPES", "0-10, 10-20, 20-30 yıllık çalışan, hastalık ve hamilelik izinlerini varsayılan izin türleri olarak sağla ve değişiklik otoritesini belgele.", ["İK operatörü", "bakım geliştiricisi"]),
    ("R-WORKER", "Her 1 Ocak'ta aktif çalışanlara StartDate/Gender uygunluğuna göre izin hak edişi atayan idempotent background worker sağla.", ["çalışan", "İK operatörü", "sistem işletmeni"]),
    ("R-PRORATION", "10 veya 20 yıl kıdem eşiğini yıl içinde geçen çalışanın eski/yeni kademe kotasını ay oranında böl.", ["çalışan", "İK operatörü"]),
    ("R-WORKDAYS", "İzin kullanım günlerinde hafta sonu ve tanımlı resmî tatilleri hariç tut.", ["çalışan", "İK operatörü"]),
    ("R-TURKISH-CALENDAR", "Tüm takvim ve tarih seçicilerde İngilizce yerine Türkçe ay/gün adları göster.", ["tüm kullanıcılar"]),
    ("R-BULK-BALANCE", "Manuel bakiye ekranında kişi, departman ve tüm çalışan kapsamlarına atama sağla; Bakiye ID'yi kaldır ve departman adı autocomplete araması ekle.", ["admin", "İK operatörü"]),
    ("R-DOCS", "İzin türü/kota değişiklik yerini ve worker işletim davranışını canonical dokümanda belirt.", ["sistem işletmeni", "bakım geliştiricisi"]),
]
contract["outcome_closure"]["requirements"] = [
    {
        "id": rid,
        "source": "USER" if rid != "R-DOCS" else "REPOSITORY_POLICY",
        "statement": statement,
        "beneficiaries": beneficiaries,
        "disposition": "IN_SCOPE",
        "decision_id": "NOT_APPLICABLE",
        "evidence": "CURRENT_USER_REQUEST" if rid != "R-DOCS" else "AGENTS.md Documentation",
    }
    for rid, statement, beneficiaries in requirements
]
dimensions = {
    "FUNCTIONAL": (True, "Audit, hak ediş, oranlama, toplu atama ve Türkçe takvim davranışı değişir."),
    "EXPERIENCE": (True, "Audit ve bakiye yönetimi operatör akışı değişir."),
    "COVERAGE": (True, "Kişi/departman/tüm çalışan, cinsiyet, kıdem eşiği, yıl ve viewport kapsamı zorunludur."),
    "QUALITY": (True, "Türkçe yerelleştirme, okunabilirlik, erişilebilirlik ve durum kalitesi istenir."),
    "CAPACITY": (True, "Tüm aktif çalışanlar × otomatik izin türleri tek worker koşusunda işlenir; tekrar/yarış yükü vardır."),
    "OPERATIONS": (True, "Schedule, disable, retry, restart catch-up, log ve cancellation davranışı zorunludur."),
    "SECURITY_DATA": (True, "Toplu izin yazımı yetki ve çalışan kapsamı sınırıdır."),
    "REGRESSION": (True, "Mevcut leave request/day calculation/approval/delegation ve audit persistence korunur."),
    "DOCUMENTATION": (True, "Değiştirilebilir kota otoritesi ve operator workflow belgelenmelidir."),
}
contract["outcome_closure"]["dimensions"] = {
    key: {"required": required, "evidence": evidence}
    for key, (required, evidence) in dimensions.items()
}
contract["outcome_closure"]["demand_envelope"] = {
    "applicable": True,
    "normal_load": "Bir takvim yılı için tüm aktif çalışanlar × uygun otomatik izin türleri; tek worker instance.",
    "peak_load": "Uygulama yeniden başlatma ile 1 Ocak koşusunun çakışması; aynı yıl için eşzamanlı iki deneme.",
    "failure_recovery_load": "Transaction sonrası güvenli retry ve restart catch-up; duplicate bakiye/audit üretmeden tekrar.",
    "evidence": "Background-worker ve toplu tüm çalışan kapsamı sonlu concurrency/throughput sınırı oluşturur.",
}
acceptance_specs = [
    ("A-FUNCTIONAL", "FUNCTIONAL", ["R-AUDIT", "R-DEFAULT-TYPES", "R-WORKER", "R-PRORATION", "R-WORKDAYS", "R-TURKISH-CALENDAR", "R-BULK-BALANCE"], "Mevcut ekranlarda otomatik hak ediş/çoklu kapsam yoktur.", "Tüm istenen davranış tek otorite yollarında çalışır ve sayısal politika doğrulanmıştır.", ["FG-LEAVE-TESTS", "E2E-LEAVE-AUDIT"]),
    ("A-EXPERIENCE", "EXPERIENCE", ["R-AUDIT", "R-TURKISH-CALENDAR", "R-BULK-BALANCE"], "Audit varlık teknik sütunları ve tek çalışan bakiye akışı gösterir.", "Operatör işlem/kullanıcı/tarih/açıklama odaklı audit ve açık kişi/departman/tümü akışı kullanır.", ["E2E-LEAVE-AUDIT"]),
    ("A-COVERAGE", "COVERAGE", ["R-WORKER", "R-PRORATION", "R-BULK-BALANCE"], "Kıdem/cinsiyet/departman/tüm çalışan matrisini kapsayan otorite yoktur.", "0/10/20 eşikleri, geçiş ayları, kadın/erkek/null cinsiyet, departman/tümü ve responsive viewports kapsanır.", ["FG-LEAVE-TESTS", "E2E-LEAVE-AUDIT"]),
    ("A-QUALITY", "QUALITY", ["R-AUDIT", "R-TURKISH-CALENDAR", "R-BULK-BALANCE"], "Mevcut management UI ve kısmi tr-TR formatları baseline'dır.", "İngilizce takvim metni, taşma, konsol/page/request hatası ve eksik durum kalmaz.", ["E2E-LEAVE-AUDIT"]),
    ("A-CAPACITY", "CAPACITY", ["R-WORKER", "R-BULK-BALANCE"], "Toplu worker yoktur.", "Kaydedilmiş çalışan/tür/yıl benzersizliği altında tüm aktif çalışanlar tek transaction ve tekrar koşuda duplicate olmadan işlenir.", ["FG-LEAVE-TESTS", "E2E-LEAVE-AUDIT"]),
    ("A-OPERATIONS", "OPERATIONS", ["R-WORKER", "R-DOCS"], "Yalnız günlük vekâlet worker örneği vardır.", "1 Ocak schedule, startup catch-up, disable, retry, cancellation ve özet log kanıtlanır.", ["FG-LEAVE-TESTS", "E2E-LEAVE-AUDIT"]),
    ("A-SECURITY", "SECURITY_DATA", ["R-AUDIT", "R-BULK-BALANCE"], "CanViewAuditLogs/CanManageLeaveBalances sayfa kararları vardır.", "Yetkisiz principal veri okumaz/yazmaz; toplu hedef istemciden güvenilmeden server tarafında çözülür; PII loglanmaz.", ["FG-LEAVE-TESTS", "E2E-LEAVE-AUDIT"]),
    ("A-REGRESSION", "REGRESSION", ["R-WORKDAYS", "R-WORKER", "R-BULK-BALANCE"], "Mevcut izin testleri ve izin günü otoriteleri baseline'dır.", "Tüm mevcut testler geçer; hafta sonu/resmî tatil, onay ve vekâlet davranışları değişmez.", ["FG-ALL-TESTS", "E2E-LEAVE-AUDIT"]),
    ("A-DOCS", "DOCUMENTATION", ["R-DEFAULT-TYPES", "R-DOCS"], "Doküman yalnız genel AnnualQuota ve manuel yenilemeyi anlatır.", "Varsayılan türler, kota/devir değişiklik yeri, worker schedule/recovery ve yetki kapsamı canonical olarak belgelenir.", ["FG-BUILD"]),
]
contract["outcome_closure"]["acceptance"] = [
    {
        "id": aid,
        "dimension": dimension,
        "requirement_ids": requirement_ids,
        "criterion": target,
        "baseline": baseline,
        "target": target,
        "validation_ids": validation_ids_for_acceptance,
        "result": "PLANNED",
        "evidence": "Sayısal izin politikası kararı sonrası uygulanacak.",
    }
    for aid, dimension, requirement_ids, baseline, target, validation_ids_for_acceptance in acceptance_specs
]
contract["outcome_closure"]["evidence"] = "Discovery graph/code/docs evidence recorded; numeric policy remains blocked."
contract["outcome_closure"]["overall_result"] = "PLANNED"
contract["review"]["focus"] = [
    "Raw user requirements and numeric policy closure",
    "Proration boundary correctness and decimal carry",
    "Worker idempotency/concurrency/retry/disable behavior",
    "Bulk authorization and all-or-nothing persistence",
    "Audit data retention with UI-only field removal",
    "Turkish calendar and responsive product consistency",
]
contract["security_closeout"]["checks"] = [
    {
        "id": check_id,
        "applicable": True,
        "plan": plan,
        "result": "PLANNED",
        "evidence": "Implementation and validation pending numeric policy decision.",
    }
    for check_id, plan in (
        ("audit-logging", "Verify worker/bulk mutations are audited without PII and audit read surface stays read-only."),
        ("backend-authority-authz-negative", "Prove CanManageLeaveBalances and CanViewAuditLogs negative server-side paths."),
        ("config-fail-closed", "Prove worker disabled configuration does no writes and invalid policy fails visibly."),
        ("dependency-external-boundary", "Confirm no external dependency is introduced; MSSQL/TimeProvider boundaries are bounded."),
        ("input-contract-secret-pii", "Validate target scope, year and leave type; inspect logs/errors for PII or secrets."),
        ("transaction-idempotency-data-loss-rollback", "Prove serializable all-or-nothing bulk/worker writes, unique keys, retry and no overwrite/data loss."),
    )
]
contract["review"]["lifecycle"] = {
    "state": "NOT_STARTED",
    "reviewer_identity": "PLANNED",
    "reviewer_model": "PLANNED",
    "independence": "PENDING",
    "no_progress_rounds": 0,
    "passes": [],
    "evidence": "Independent reviewer starts after fast gates.",
}
contract["evidence"] = {
    "artifact_policy": "HASH_BOUND_CANONICAL",
    "manifest": {
        "base": "REPOSITORY",
        "path": ".bet-task/evidence/leave-automation-audit-ledger.json",
        "sha256": "PLANNED",
    },
}

contract_api.refresh_policy_contract(contract, SKILL_ROOT)
contract_api.refresh_decision_control(contract)
contract_api.refresh_checkpoint(
    contract,
    phase="DISCOVERY",
    completed_step_ids=[],
    pending_step_ids=["DISCOVERY", "IMPLEMENTATION", "VALIDATION", "REVIEW", "FINAL_CLOSEOUT"],
    invalidated_validation_ids=[],
    active_remediation_batch_id=None,
    enforce_definition_revision=False,
)
OUTPUT.parent.mkdir(parents=True, exist_ok=True)
contract_api.atomic_write_json(OUTPUT, contract, compact=False)
print(json.dumps({"result": "WRITTEN", "path": str(OUTPUT)}, ensure_ascii=False))
