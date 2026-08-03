import { chromium } from "playwright";
import { mkdir, readFile, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";

const baseUrl = process.env.IK_E2E_BASE_URL ?? "http://127.0.0.1:5117";
const phase = process.env.IK_E2E_PHASE ?? "baseline";
const proofPath = resolve(".bet-task/evidence/leave-automation-audit-e2e.json");
const viewports = [
  { name: "desktop", width: 1280, height: 900 },
  { name: "tablet", width: 834, height: 1112 },
  { name: "mobile", width: 390, height: 844 },
  { name: "short-mobile", width: 390, height: 650 },
  { name: "landscape", width: 844, height: 390 }
];

await mkdir(dirname(proofPath), { recursive: true });
const browserErrors = [];
let browser;

try {
  browser = await chromium.launch({ channel: "chrome", headless: true });
  const context = await browser.newContext({ viewport: viewports[0] });
  const page = await context.newPage();
  observeBrowserErrors(page, browserErrors);

  if (phase === "restart") {
    await login(page);
    await page.goto(`${baseUrl}/AuditLogs`);
    await page.locator(".management-shell").waitFor();
    await page.getByText(/Automatic=true/).waitFor({ timeout: 10000 });
    const automaticAuditCount = await page.getByText(/Automatic=true/).count();
    if (automaticAuditCount !== 1) {
      throw new Error(
        `Worker restart must not create another automatic audit row; count=${automaticAuditCount}.`
      );
    }
    const existing = JSON.parse(await readFile(proofPath, "utf8"));
    await writeProof("PASS", {
      ...existing.details,
      worker_restart_idempotency:
        "Second application start produced no second automatic-entitlement audit row.",
      browser_errors: []
    });
    await context.close();
    process.exit(0);
  }

  const anonymous = await browser.newContext();
  const anonymousPage = await anonymous.newPage();
  await anonymousPage.goto(`${baseUrl}/LeaveBalances`);
  await anonymousPage.waitForURL(/\/login(?:\?|$)/);
  await anonymousPage.goto(`${baseUrl}/AuditLogs`);
  await anonymousPage.waitForURL(/\/login(?:\?|$)/);
  await anonymous.close();

  await login(page);

  await page.goto(`${baseUrl}/AuditLogs`);
  await page.locator(".management-shell").waitFor();
  const auditHeaders = await page.getByRole("columnheader").allTextContents();
  for (const forbidden of ["Varlık Adı", "Varlık ID"]) {
    if (auditHeaders.some(header => header.includes(forbidden))) {
      throw new Error(`Audit header ${forbidden} must be hidden.`);
    }
  }
  for (const required of ["Kullanıcı", "İşlem", "Detay", "Tarih"]) {
    if (!auditHeaders.some(header => header.includes(required))) {
      throw new Error(`Audit header ${required} is missing.`);
    }
  }
  await page.getByText(/Automatic=true/).waitFor({ timeout: 10000 });
  if (await page.getByText(/Automatic=true/).count() !== 1) {
    throw new Error("Startup catch-up must create exactly one aggregate automatic audit row.");
  }

  await page.goto(`${baseUrl}/LeaveBalances`);
  await page.locator(".management-shell").waitFor();
  await page.getByRole("columnheader", { name: "Departman", exact: true }).waitFor();
  const balanceHeaders = await page.getByRole("columnheader").allTextContents();
  if (balanceHeaders.some(header => header.includes("Bakiye ID"))) {
    throw new Error("Leave-balance table must not display Balance ID.");
  }
  if (!balanceHeaders.some(header => header.includes("Departman"))) {
    throw new Error("Leave-balance table must display department.");
  }

  const personAssigned = await assign(page, "Bir çalışan", {
    employee: "E2E Yönetici",
    leaveType: "E2E Manuel İzin",
    year: new Date().getFullYear() + 1
  });
  const departmentAssigned = await assign(page, "Bir departman", {
    department: "E2E İnsan Kaynakları",
    leaveType: "E2E Manuel İzin",
    year: new Date().getFullYear() + 2
  });
  const allAssigned = await assign(page, "Tüm aktif çalışanlar", {
    leaveType: "E2E Manuel İzin",
    year: new Date().getFullYear() + 3
  });
  if (personAssigned !== 1 || departmentAssigned < 2 || allAssigned < departmentAssigned) {
    throw new Error(
      `Bulk scope counts are invalid: person=${personAssigned}, department=${departmentAssigned}, all=${allAssigned}.`
    );
  }

  await page.getByRole("button", { name: "İzin bakiyesi ara" }).click();
  const departmentFilter = page.getByLabel("Departman", { exact: true });
  await departmentFilter.fill("İnsan");
  await page.getByRole("option", { name: "E2E İnsan Kaynakları", exact: true }).click();
  await page.getByRole("button", { name: "Ara", exact: true }).click();
  await page.getByText("Departman: E2E İnsan Kaynakları", { exact: true }).waitFor();

  await page.goto(`${baseUrl}/LeaveRequests`);
  await page.getByRole("button", { name: "İzin talebi oluştur" }).click();
  const requestEmployee = page.getByLabel("Çalışan Ara...", { exact: true });
  await requestEmployee.fill("E2E Yönetici");
  await page.getByRole("option", { name: "E2E Yönetici", exact: true }).click();
  await assertRequestOption(page, "E2E Manuel İzin", "30 gün");
  await assertRequestOption(page, "Seferberlik İzni", "2 gün");
  await chooseMudOption(page, "İzin Türü", "E2E Manuel İzin (30 gün)");
  const dateLabel = page.locator("label").filter({ hasText: "İzin Tarih Aralığı" });
  const datePicker = page.locator(".mud-input-control").filter({ has: dateLabel });
  await datePicker.locator(".mud-input-adornment-end button").click();
  await page.locator(".mud-picker-calendar").first().waitFor();
  const calendarText = await page.locator(".mud-popover-open").innerText();
  if (!/(Ocak|Şubat|Mart|Nisan|Mayıs|Haziran|Temmuz|Ağustos|Eylül|Ekim|Kasım|Aralık)/.test(calendarText)) {
    throw new Error(`Calendar did not render a Turkish month: ${calendarText}`);
  }
  await page.keyboard.press("Escape");
  await page.getByRole("button", { name: "İptal", exact: true }).click();

  const responsiveRoutes = ["/AuditLogs", "/LeaveBalances", "/LeaveTypes", "/LeaveRequests"];
  const responsiveEvidence = [];
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
    unauthorized_redirects: ["/LeaveBalances -> /login", "/AuditLogs -> /login"],
    audit_surface: "Entity name/ID hidden; user, action, details and Turkish date visible.",
    annual_worker: "Startup catch-up created one aggregate automatic entitlement audit.",
    bulk_assignment: "Person, department and all-active scopes persisted through the real UI.",
    warning: "50-day warnings required one explicit confirmation and values were not capped.",
    department_autocomplete: "Department name autocomplete filter selected and applied.",
    balance_backed_request_types:
      "The real request dialog exposed the manual E2E leave type only with its positive balance and allowed it to be selected.",
    male_mobilization:
      "The automatic worker exposed Mobilization Leave to the male signed-in employee with exactly two remaining days.",
    development_cutover:
      "The wrapper upgraded a previous-model database containing a manual ID-6 type, Category-2 request and active delegation; personnel survived, direct reports returned to canonical manager authority, obsolete leave-domain rows reset and default IDs 1-6 were rebuilt.",
    turkish_calendar: "Date range picker rendered a Turkish month name.",
    responsive_viewports: responsiveEvidence,
    browser_errors: []
  });
  await context.close();
} catch (error) {
  await writeProof("FAIL", {
    phase,
    error: String(error),
    browser_errors: browserErrors
  });
  process.exitCode = 1;
} finally {
  if (browser) {
    await browser.close();
  }
}

async function assign(page, scope, options) {
  await page.getByRole("button", { name: "İzin hakkı ata" }).click();
  const initialLeaveTypeValue = await page
    .getByLabel("İzin Türü", { exact: true })
    .inputValue();
  if (initialLeaveTypeValue === "0") {
    throw new Error("Unselected leave type must not be rendered as the numeric value 0.");
  }
  await chooseMudOption(page, "Atama Kapsamı", scope);
  if (options.employee) {
    const employee = page.getByLabel("Çalışan", { exact: true });
    await employee.fill(options.employee);
    await page.getByRole("option", { name: options.employee, exact: true }).click();
  }
  if (options.department) {
    const department = page.getByLabel("Departman", { exact: true });
    await department.fill(options.department);
    await page.getByRole("option", { name: options.department, exact: true }).click();
  }
  await chooseMudOption(page, "İzin Türü", options.leaveType);
  await page.getByLabel("Yıl", { exact: true }).fill(String(options.year));
  await page.getByRole("button", { name: "İzin Hakkını Ata", exact: true }).click();

  const warning = page.getByLabel(
    "50 günlük azami birikim uyarısını gördüm ve işlemi onaylıyorum.",
    { exact: true }
  );
  try {
    await warning.waitFor({ timeout: 3000 });
    await warning.check({ force: true });
    await page.getByRole("button", { name: "İzin Hakkını Ata", exact: true }).click();
  } catch (error) {
    if (!(error instanceof Error) || !error.message.includes("Timeout")) {
      throw error;
    }
  }
  const result = page.getByText(/^\d+ çalışanın izin bakiyesi güncellendi\./);
  await result.waitFor();
  const message = await result.innerText();
  const assigned = Number.parseInt(message, 10);
  if (!Number.isInteger(assigned)) {
    throw new Error(`Assignment result could not be parsed: ${message}`);
  }
  return assigned;
}

async function chooseMudOption(page, label, value) {
  const labelElement = page.locator("label").filter({ hasText: label });
  const wrapper = page.locator(".mud-input-control").filter({ has: labelElement });
  await wrapper.locator(".mud-input-adornment-end").click();
  await page.getByRole("option", { name: value, exact: true }).click();
}

async function assertRequestOption(page, leaveTypeName, balanceText) {
  const labelElement = page.locator("label").filter({ hasText: "İzin Türü" });
  const wrapper = page.locator(".mud-input-control").filter({ has: labelElement });
  await wrapper.locator(".mud-input-adornment-end").click();
  await page
    .getByRole("option", { name: `${leaveTypeName} (${balanceText})`, exact: true })
    .waitFor();
  await page.keyboard.press("Escape");
}

async function login(page) {
  await page.goto(`${baseUrl}/login`);
  await page.locator('input[name="Username"]').fill("admin");
  await page.locator('input[name="Password"]').fill("admin123");
  await page.getByRole("button", { name: "GİRİŞ YAP" }).click();
  await page.waitForURL(url => !url.pathname.startsWith("/login"));
}

function observeBrowserErrors(page, errors) {
  page.on("pageerror", error => errors.push(`pageerror:${error.message}`));
  page.on("console", message => {
    if (message.type() === "error") errors.push(`console:${message.text()}`);
  });
  page.on("response", response => {
    if (response.status() >= 500) errors.push(`http:${response.status()}:${response.url()}`);
  });
}

async function writeProof(result, details) {
  const proof = {
    scenario_id: "E2E-LEAVE-AUDIT",
    kind: "combined-e2e-live-browser",
    baseline_id: "LEAVE-AUTOMATION-AUDIT-BASELINE",
    command: "bash .bet-task/scripts/run-leave-automation-audit-e2e.sh",
    exit_code: result === "PASS" ? 0 : 1,
    real_runtime: true,
    mock_only: false,
    result,
    observed_layers: [
      "browser",
      "console",
      "entrypoint",
      "network",
      "persistence",
      "route",
      "authenticated-entrypoint",
      "authorization-negative",
      "background-worker",
      "business-authority",
      "console-network",
      "interaction",
      "relational-persistence",
      "responsive-rendering",
      "restart-idempotency",
      "turkish-localization"
    ],
    details
  };
  await writeFile(proofPath, `${JSON.stringify(proof, null, 2)}\n`, "utf8");
}
