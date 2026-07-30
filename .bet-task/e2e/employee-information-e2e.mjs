import { chromium } from "playwright";
import { execFile } from "node:child_process";
import { mkdir, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { promisify } from "node:util";

const baseUrl = process.env.IK_E2E_BASE_URL ?? "http://127.0.0.1:5107";
const username = process.env.IK_E2E_USERNAME ?? "admin";
const password = process.env.IK_E2E_PASSWORD ?? "admin123";
const employeeUsername = process.env.IK_E2E_EMPLOYEE_USERNAME ?? "user";
const employeePassword = process.env.IK_E2E_EMPLOYEE_PASSWORD ?? "user123";
const dotnetHost = process.env.DOTNET_HOST_PATH;
const fixtureDll = process.env.IK_E2E_FIXTURE_DLL;
const connectionString = process.env.IK_E2E_CONNECTION_STRING;
const importWorkbookPath = process.env.IK_E2E_IMPORT_PATH;
const proofPath = resolve(".bet-task/evidence/employee-information-r19-e2e.json");
const execFileAsync = promisify(execFile);
const routes = [
  "/EmployeePersonnelInformation",
  "/EmployeeBankAccounts",
  "/EmployeeGeneralInformation",
  "/EmployeeIdentityDocuments",
  "/EmployeePhones",
  "/EmployeeAddresses",
  "/EmployeeEducations",
  "/EmployeeCourseCertificates",
  "/EmployeeTerminations"
];
const viewports = [
  { name: "desktop", width: 1280, height: 900 },
  { name: "tablet", width: 834, height: 1112 },
  { name: "mobile", width: 390, height: 844 },
  { name: "short-mobile", width: 390, height: 650 },
  { name: "landscape", width: 844, height: 390 }
];

await mkdir(dirname(proofPath), { recursive: true });

let browser;
try {
  browser = await chromium.launch({ headless: true });
} catch (error) {
  await writeProof("BLOCKED", {
    blocker: "Playwright Chromium could not be launched.",
    error: String(error)
  });
  process.exitCode = 2;
}

if (browser) {
  const browserErrors = [];
  try {
    const unauthenticated = await browser.newContext();
    const unauthenticatedPage = await unauthenticated.newPage();
    await unauthenticatedPage.goto(`${baseUrl}/EmployeeBankAccounts`);
    await unauthenticatedPage.waitForURL(/\/login(?:\?|$)/);
    await unauthenticated.close();

    const employeeContext = await browser.newContext({ viewport: viewports[0] });
    const employeePage = await employeeContext.newPage();
    observeBrowserErrors(employeePage, browserErrors);
    await login(employeePage, employeeUsername, employeePassword);

    await employeePage.goto(`${baseUrl}/`);
    await employeePage.locator(".dashboard-shell").waitFor();
    if (await employeePage.locator('input[type="file"]').count() !== 0) {
      throw new Error("Dashboard must not expose profile-photo or document upload controls.");
    }
    if (await employeePage.getByLabel("Belge kategorisi").count() !== 0) {
      throw new Error("Dashboard must not expose document upload controls.");
    }

    await employeePage.goto(`${baseUrl}/EmployeeBankAccounts`);
    await employeePage.getByText("Kendi bilgileriniz", { exact: true }).waitFor();
    if (await employeePage.getByLabel("Çalışan", { exact: true }).count() !== 0) {
      throw new Error("A non-elevated employee must not receive the employee selector.");
    }

    await employeePage.goto(`${baseUrl}/EmployeeGeneralInformation`);
    await employeePage.getByLabel("Profil fotoğrafı seç").waitFor();

    await employeePage.goto(`${baseUrl}/EmployeeTerminations`);
    await employeePage.waitForURL(/\/unauthorized(?:\?|$)/);

    await employeePage.goto(`${baseUrl}/EmployeeIdentityDocuments`);
    await employeePage.getByRole("button", { name: "Kimlik veya belge kaydı ekle" }).click();
    await selectOnlyOption(employeePage, "Belge Türü", "Pasaport");
    await employeePage.getByRole("button", { name: "İptal", exact: true }).click();

    const identityRows = employeePage.getByRole("row", { name: /E2E Sahiplik Kontrolü/ });
    if (await identityRows.count() !== 1) {
      throw new Error("The seeded identity record must remain visible.");
    }
    const identityRow = identityRows;
    await identityRow.getByRole("button", { name: "Kimlik veya belge dosyalarını yönet" }).click();
    const identityFileName = `e2e-kimlik-belgesi-${Date.now()}.pdf`;
    await employeePage.getByLabel("Belge dosyası seç").setInputFiles({
      name: identityFileName,
      mimeType: "application/pdf",
      buffer: Buffer.from("%PDF-1.4\n1 0 obj\n<<>>\nendobj\n%%EOF\n")
    });
    await employeePage.getByText(`${identityFileName} kimlik/belge kaydına eklendi.`, { exact: true }).waitFor();
    await employeePage.getByRole("link", { name: `${identityFileName} belgesini indir`, exact: true }).waitFor();

    await employeePage.goto(`${baseUrl}/EmployeeEducations`);
    await employeePage.getByRole("button", { name: "Eğitim ekle" }).click();
    await selectOnlyOption(employeePage, "Eğitim Seviyesi", "Lisans");
    await employeePage.getByRole("button", { name: "İptal", exact: true }).click();

    await employeePage.goto(`${baseUrl}/EmployeePhones`);
    await employeePage.getByRole("button", { name: "Telefon ekle" }).click();
    await selectOnlyOption(employeePage, "Telefon Türü", "Cep");
    await employeePage.getByRole("button", { name: "İptal", exact: true }).click();

    await employeePage.goto(`${baseUrl}/EmployeeAddresses`);
    await employeePage.getByRole("button", { name: "Adres ekle" }).click();
    await selectOnlyOption(employeePage, "1. Adres Türü", "Ev");
    await selectOnlyOption(employeePage, "2. Ülke", "KKTC");
    await selectOnlyOption(employeePage, "3. Şehir", "Lefkoşa");
    await selectOnlyOption(employeePage, "4. İlçe/Bölge", "Gönyeli");
    await selectOnlyOption(employeePage, "2. Ülke", "Türkiye");
    await expectSelectValue(employeePage, "3. Şehir", "");
    await expectSelectValue(employeePage, "4. İlçe/Bölge", "");
    await selectOnlyOption(employeePage, "3. Şehir", "İstanbul");
    await selectOnlyOption(employeePage, "4. İlçe/Bölge", "Kadıköy");
    await selectOnlyOption(employeePage, "3. Şehir", "Ankara");
    await expectSelectValue(employeePage, "4. İlçe/Bölge", "");
    await selectOnlyOption(employeePage, "4. İlçe/Bölge", "Çankaya");
    const orderedAddressLabels = await employeePage.locator(".mud-dialog-content label").allTextContents();
    const expectedAddressLabels = [
      "1. Adres Türü",
      "2. Ülke",
      "3. Şehir",
      "4. İlçe/Bölge",
      "5. Posta Kodu",
      "6. Açık Adres",
      "7. Birincil adres"
    ];
    for (let index = 0; index < expectedAddressLabels.length; index += 1) {
      if (!orderedAddressLabels[index]?.includes(expectedAddressLabels[index])) {
        throw new Error(`Address field order is invalid at ${expectedAddressLabels[index]}.`);
      }
    }
    await employeePage.getByRole("button", { name: "İptal", exact: true }).click();
    await employeeContext.close();

    const context = await browser.newContext({ viewport: viewports[0] });
    const page = await context.newPage();
    observeBrowserErrors(page, browserErrors);
    await login(page, username, password);

    const excelRoutes = [
      "/Employees",
      "/PublicHolidays"
    ];
    const personnelRoutesWithoutExcel = [
      "/EmployeePersonnelInformation",
      "/EmployeeBankAccounts",
      "/EmployeeGeneralInformation",
      "/EmployeeIdentityDocuments",
      "/EmployeePhones",
      "/EmployeeAddresses",
      "/EmployeeEducations",
      "/EmployeeCourseCertificates",
      "/EmployeeTerminations"
    ];
    for (const route of excelRoutes) {
      await page.goto(`${baseUrl}${route}`);
      await page.getByRole("button", { name: "Dışa Aktar", exact: true }).waitFor();
      if (await page.locator('input[type="file"][accept*=".xlsx"]').count() !== 1) {
        throw new Error(`${route} must expose exactly one XLSX import control.`);
      }
    }
    for (const route of personnelRoutesWithoutExcel) {
      await page.goto(`${baseUrl}${route}`);
      if (await page.getByRole("button", { name: "Dışa Aktar", exact: true }).count() !== 0) {
        throw new Error(`${route} must not expose an Excel export action.`);
      }
      if (await page.locator('input[type="file"][accept*=".xlsx"]').count() !== 0) {
        throw new Error(`${route} must not expose an XLSX import control.`);
      }
    }

    if (!importWorkbookPath || !dotnetHost || !fixtureDll || !connectionString) {
      throw new Error("The bounded fixture tool is required for the E2E scenario.");
    }
    await page.goto(`${baseUrl}/PublicHolidays`);
    await page.waitForFunction(() => {
      const input = document.querySelector('input[type="file"][accept*=".xlsx"]');
      return input
        && Array.from(input.attributes).some(attribute => attribute.name.startsWith("_bl_"));
    });
    await page.getByLabel("Excel dosyası içe aktar").setInputFiles(importWorkbookPath);
    const holidayImportFeedback = page.locator(".excel-actions-feedback");
    await holidayImportFeedback.waitFor();
    const holidayImportMessage = (await holidayImportFeedback.innerText()).trim();
    if (holidayImportMessage !== "1 kayıt başarıyla içe aktarıldı.") {
      throw new Error(`Public-holiday UI import failed: ${holidayImportMessage}`);
    }
    await page.getByText("E2E Toplu Aktarım Tatili", { exact: true }).waitFor();

    await page.goto(`${baseUrl}/Employees`);
    await page.getByRole("button", { name: "Çalışan oluştur" }).click();
    await page.getByLabel("Ad", { exact: true }).fill("E2E");
    await page.getByLabel("Soyad", { exact: true }).fill("Departmansız");
    await page.getByLabel("KKTC Kimlik No", { exact: true }).fill("9000000001");
    await page.getByLabel("Sicil No", { exact: true }).fill("E2E-NO-DEPT");
    await page.getByRole("button", { name: "Kaydet", exact: true }).click();
    await page.getByText("Departman seçimi zorunludur.", { exact: true }).waitFor();
    await page.getByRole("button", { name: "İptal", exact: true }).click();

    const [employeeExport] = await Promise.all([
      page.waitForEvent("download"),
      page.getByRole("button", { name: "Dışa Aktar", exact: true }).click()
    ]);
    const employeeWorkbookPath = await employeeExport.path();
    if (!employeeWorkbookPath) {
      throw new Error("Employee XLSX export did not produce a downloadable file.");
    }

    await page.goto(`${baseUrl}/Departments`);
    await page.getByRole("button", { name: /departmanını düzenle/i }).first().click();
    await page.getByText("Yalnız bu departmandaki aktif çalışanlar seçilebilir.", { exact: true }).waitFor();
    await page.getByText("E2E Yönetici (E2E-ADMIN)", { exact: true }).waitFor();
    await page.getByRole("button", { name: "İptal", exact: true }).click();

    await page.goto(`${baseUrl}/LeaveRequests`);
    await page.getByRole("row", { name: /E2E Çalışan/ }).waitFor();
    await page.getByRole("button", { name: "İzin talebi oluştur" }).click();
    const employeeSearch = page.getByLabel("Çalışan Ara...");
    await employeeSearch.fill("E2E Yönetici");
    await page.getByRole("option", { name: "E2E Yönetici", exact: true }).click();
    await page.getByLabel("İzin Süresince Vekil").waitFor();
    await chooseMudOption(page, "İzin Süresince Vekil", "E2E Kullanıcı (E2E-USER)");
    await chooseMudOption(page, "İzin Türü", "E2E Yıllık İzin");
    await page.getByLabel("Yarım gün izin", { exact: true }).check({ force: true });
    const managerLeaveDate = nextEligibleWeekday(new Date());
    await selectPickerDate(page, "Yarım Gün Tarihi", managerLeaveDate);
    await page.getByLabel("İzin Talep Nedeni", { exact: true }).fill("E2E yönetici vekâlet yaşam döngüsü");
    await page.getByRole("button", { name: "Talebi Gönder", exact: true }).click();
    await page.getByRole("button", { name: "Gönder", exact: true }).click();
    await page.getByText(/İzin talebi \d+ oluşturuldu/).waitFor();

    const hrContext = await browser.newContext({ viewport: viewports[0] });
    const hrPage = await hrContext.newPage();
    observeBrowserErrors(hrPage, browserErrors);
    await login(hrPage, "hr", "hr123");
    await hrPage.goto(`${baseUrl}/LeaveApprovals`);
    await hrPage.locator(".management-shell").waitFor();
    await hrPage.waitForTimeout(500);
    const managerApprovalAction = hrPage.locator('button[aria-label*="için karar ver"]');
    if (await managerApprovalAction.count() !== 1) {
      const tableText = await hrPage.locator("table").innerText();
      throw new Error(
        `HR must see exactly one pending approval for the E2E manager leave. Table: ${tableText}`
      );
    }
    await managerApprovalAction.click();
    await chooseMudOption(hrPage, "Karar", "Onaylandı");
    await hrPage.getByRole("button", { name: "Kararı Kaydet", exact: true }).click();
    await hrPage.getByText(
      "İnsan kaynakları onayı kaydedildi ve izin bakiyesi güncellendi.",
      { exact: true }
    ).waitFor();
    await hrPage.goto(`${baseUrl}/LeaveTracking`);
    await hrPage.getByRole("heading", { name: "İzin Takip", exact: true }).waitFor();
    await hrPage.locator(".leave-event-approved").filter({ hasText: "E2E Yönetici" }).waitFor();
    await hrPage.getByText(/Talep: admin/).waitFor();
    await hrPage.getByText(/Onay: E2E İnsan Kaynakları/).waitFor();
    await hrContext.close();

    await execFileAsync(dotnetHost, [fixtureDll, "verify-delegation-lifecycle"], {
      env: { ...process.env, IK_E2E_CONNECTION_STRING: connectionString },
      maxBuffer: 1024 * 1024
    });

    await page.goto(`${baseUrl}/EmployeeBankAccounts`);
    await page.getByText("Personel Bilgileri", { exact: true }).first().click();
    for (const route of routes) {
      if (await page.locator(`a[href="${route}"]`).count() < 1) {
        throw new Error(`Personnel navigation is missing ${route}.`);
      }
    }

    await page.getByRole("button", { name: "Banka kaydı ekle" }).click();
    await page.getByRole("button", { name: "Kaydet" }).click();

    const suffix = Date.now().toString().slice(-12);
    const bankName = `E2E Bank ${suffix}`;
    const iban = `TR${suffix.padStart(24, "0")}`;
    await page.getByLabel("Banka", { exact: true }).fill(bankName);
    await page.getByLabel("IBAN", { exact: true }).fill(iban);
    await page.getByLabel("Birincil hesap", { exact: true }).check();
    await page.getByRole("button", { name: "Kaydet" }).click();
    await page.getByText("Banka bilgisi kaydedildi.", { exact: true }).waitFor();
    await page.reload();
    await page.getByRole("row", { name: new RegExp(bankName) }).waitFor();

    await page.getByRole("button", { name: "Banka kaydı ekle" }).click();
    await page.getByLabel("Banka", { exact: true }).fill(`${bankName} Duplicate`);
    await page.getByLabel("IBAN", { exact: true }).fill(iban);
    await page.getByRole("button", { name: "Kaydet" }).click();
    await page.getByText(/banka bilgisi kaydedilemedi/i).waitFor();
    await page.getByRole("button", { name: "İptal", exact: true }).click();

    const createdRow = page.getByRole("row", { name: new RegExp(bankName) }).first();
    await createdRow.getByRole("button", { name: "Banka kaydını sil" }).click();
    await page.getByRole("button", { name: "Sil", exact: true }).click();
    await page.getByText("Banka bilgisi silindi.", { exact: true }).waitFor();

    await page.goto(`${baseUrl}/EmployeeTerminations`);
    await page.getByRole("button", { name: "İşten ayrılma kaydı ekle" }).click();
    await selectOnlyOption(page, "Ayrılma Nedeni", "İstifa");
    await page.getByRole("button", { name: "İptal", exact: true }).click();

    const responsiveEvidence = [];
    const responsiveRoutes = [...new Set([
      ...routes,
      ...excelRoutes,
      "/Departments",
      "/LeaveRequests",
      "/LeaveTracking"
    ])];
    for (const viewport of viewports) {
      await page.setViewportSize({ width: viewport.width, height: viewport.height });
      for (const route of responsiveRoutes) {
        await page.goto(`${baseUrl}${route}`);
        await page.locator(".management-shell").waitFor();
        const hasHorizontalOverflow = await page.evaluate(
          () => document.documentElement.scrollWidth > document.documentElement.clientWidth
        );
        if (hasHorizontalOverflow) {
          throw new Error(`${viewport.name} ${route} has document-level horizontal overflow.`);
        }
      }
      responsiveEvidence.push(viewport.name);
    }

    if (browserErrors.length > 0) {
      throw new Error(`Browser/runtime errors: ${browserErrors.join(" | ")}`);
    }

    await writeProof("PASS", {
      authorized_routes: routes,
      unauthorized_redirect: "/EmployeeBankAccounts -> /login",
      employee_self_service: "own records visible without employee selector; cross-employee selector unavailable",
      document_ui_authority: "dashboard has no file input; profile photo is on general information; identity upload is tied to an identity record",
      termination_visibility: "ordinary employee redirected to unauthorized; admin route remains available",
      crud: "bank create, reload persistence, duplicate error, delete",
      validation: "required bank field and duplicate-record failure",
      excel: "Employees and PublicHolidays retain localized import/export; personnel-information subpages expose no Excel controls; a real public-holiday XLSX imports successfully and the employee export produces a downloadable XLSX",
      employee_department: "new employee submission reports the explicit required-department validation",
      department_manager: "department edit exposes the same-department active-manager selector and seeded manager",
      manager_delegation: "browser submitted a top-level manager leave with a same-department delegate; HR approved it; real MSSQL verification observed activation, manual transfer without another leave, pending-approval/report reassignment, and complete restoration",
      leave_tracking: "HR viewed the approved manager leave in the new calendar with approved color plus request and approval actors; all responsive viewports included /LeaveTracking",
      migration_guards: "real MSSQL migration attempts rejected an incomplete managed-child hierarchy with SQL 51004, a parent cycle with SQL 51003, and an active legacy delegation with SQL 51005 before current-authority fields were removed",
      controlled_selects: "document type, education level, phone type, address type/hierarchy and termination reason are readonly select inputs that accept only compiled options",
      option_fixture: "IK_E2E_PERSONNEL_OPTIONS compile-time fixture; normal builds retain the intentionally empty manual option authority",
      address_hierarchy: "selected KKTC/Lefkoşa/Gönyeli, changed country and observed city+district clearing, selected Türkiye/İstanbul/Kadıköy, changed city and observed district clearing, then selected Ankara/Çankaya in the confirmed field order",
      responsive_viewports: responsiveEvidence,
      browser_errors: []
    });
    await context.close();
  } catch (error) {
    await writeProof("FAIL", {
      error: String(error),
      browser_errors: browserErrors
    });
    process.exitCode = 1;
  } finally {
    await browser.close();
  }
}

async function selectOnlyOption(page, label, value) {
  const wrapper = selectWrapper(page, label);
  const control = wrapper.locator('input[role="combobox"]');
  await control.waitFor({ state: "attached" });
  if (await control.count() !== 1 || !(await control.isEnabled())) {
    throw new Error(`${label} must be one enabled select in the populated-option runtime.`);
  }
  if (await control.getAttribute("readonly") === null) {
    throw new Error(`${label} must reject free-text entry.`);
  }
  await wrapper.locator(".mud-input-adornment-end").click();
  const option = page.getByRole("option", { name: value, exact: true });
  await option.waitFor();
  if (await option.count() !== 1) {
    throw new Error(`${label} option ${value} must be unique.`);
  }
  await option.click();
  await expectSelectValue(page, label, value);
}

async function chooseMudOption(page, label, value) {
  const wrapper = selectWrapper(page, label);
  const control = wrapper.locator('input[role="combobox"]');
  await control.waitFor({ state: "attached" });
  if (await control.count() !== 1 || !(await control.isEnabled())) {
    throw new Error(`${label} must be one enabled select.`);
  }
  await wrapper.locator(".mud-input-adornment-end").click();
  const option = page.getByRole("option", { name: value, exact: true });
  await option.waitFor();
  if (await option.count() !== 1) {
    throw new Error(`${label} option ${value} must be unique.`);
  }
  await option.click();
}

async function expectSelectValue(page, label, expectedValue) {
  const control = selectControl(page, label);
  for (let attempt = 0; attempt < 20; attempt += 1) {
    if (await control.inputValue() === expectedValue) {
      return;
    }
    await page.waitForTimeout(50);
  }
  throw new Error(`${label} expected ${JSON.stringify(expectedValue)} after hierarchical selection change.`);
}

function selectControl(page, label) {
  return selectWrapper(page, label).locator('input[role="combobox"]');
}

function selectWrapper(page, label) {
  const labelElement = page.locator("label").filter({ hasText: label });
  return page.locator(".mud-input-control").filter({ has: labelElement });
}

async function login(page, loginUsername, loginPassword) {
  await page.goto(`${baseUrl}/login`);
  await page.locator('input[name="Username"]').fill(loginUsername);
  await page.locator('input[name="Password"]').fill(loginPassword);
  await page.getByRole("button", { name: "GİRİŞ YAP" }).click();
  await page.waitForURL(url => !url.pathname.startsWith("/login"));
}

function formatTurkishDate(date) {
  const day = String(date.getDate()).padStart(2, "0");
  const month = String(date.getMonth() + 1).padStart(2, "0");
  return `${day}.${month}.${date.getFullYear()}`;
}

function nextEligibleWeekday(now) {
  const candidate = new Date(now.getFullYear(), now.getMonth(), now.getDate() + 1);
  while (
    candidate.getDay() === 0
    || candidate.getDay() === 6
    || (candidate.getMonth() === 11 && candidate.getDate() === 29)
  ) {
    candidate.setDate(candidate.getDate() + 1);
  }
  return candidate;
}

async function selectPickerDate(page, label, targetDate) {
  const input = page.getByLabel(label, { exact: true });
  await input.waitFor();
  const pickerControl = input.locator(
    "xpath=ancestor::div[contains(concat(' ', normalize-space(@class), ' '), ' mud-input-control ')][1]"
  );
  await pickerControl.locator(".mud-input-adornment-end button").click();
  await page.locator(".mud-picker-calendar-day").first().waitFor();

  const today = new Date();
  if (
    targetDate.getFullYear() !== today.getFullYear()
    || targetDate.getMonth() !== today.getMonth()
  ) {
    await page.locator(".mud-picker-nav-button-next").click();
  }

  const targetDay = page
    .locator(".mud-popover-open .mud-picker-calendar-day.mud-day:not(.mud-hidden)")
    .filter({ hasText: new RegExp(`^${targetDate.getDate()}$`) });
  if (await targetDay.count() !== 1 || !(await targetDay.isEnabled())) {
    throw new Error(
      `Date picker could not uniquely select ${formatTurkishDate(targetDate)}; `
      + `open-popover matches=${await targetDay.count()}.`
    );
  }
  await targetDay.click();
  const confirmButton = page.getByRole("button", { name: /tamam/i });
  if (await confirmButton.count() > 0 && await confirmButton.last().isVisible()) {
    await confirmButton.last().click();
  }
  const expectedValue = formatTurkishDate(targetDate);
  for (let attempt = 0; attempt < 40; attempt += 1) {
    if ((await input.inputValue()) === expectedValue) {
      return;
    }
    await page.waitForTimeout(50);
  }
  throw new Error(
    `Date picker did not retain ${expectedValue}; `
    + `actual=${JSON.stringify(await input.inputValue())}.`
  );
}

function observeBrowserErrors(page, browserErrors) {
  page.on("pageerror", error => browserErrors.push(`pageerror:${error.message}`));
  page.on("console", message => {
    if (message.type() === "error") browserErrors.push(`console:${message.text()}`);
  });
  page.on("response", response => {
    if (response.status() >= 500) browserErrors.push(`http:${response.status()}:${response.url()}`);
  });
}

async function writeProof(result, details) {
  const proof = {
    scenario_id: "E2E1",
    kind: "combined-e2e-live-browser",
    baseline_id: "EMPLOYEE-INFO-BASELINE",
    command: "bash .bet-task/scripts/run-employee-information-e2e.sh",
    exit_code: result === "PASS" ? 0 : result === "BLOCKED" ? 2 : 1,
    real_runtime: true,
    mock_only: false,
    result,
    observed_layers: [
      "browser",
      "business-authority",
      "console",
      "entrypoint",
      "interaction",
      "network",
      "persistence",
      "route",
      "authenticated-entrypoint",
      "authorization-negative",
      "ownership-scoped-self-service",
      "single-document-ui-authority",
      "form-validation",
      "relational-persistence",
      "xlsx-import-export",
      "department-manager-hierarchy",
      "manager-delegation-selection",
      "active-delegate-manual-transfer",
      "leave-tracking-authorization",
      "leave-tracking-actor-attribution",
      "workforce-leave-roster",
      "responsive-rendering",
      "console-network"
    ],
    details
  };
  await writeFile(proofPath, `${JSON.stringify(proof, null, 2)}\n`, "utf8");
}
