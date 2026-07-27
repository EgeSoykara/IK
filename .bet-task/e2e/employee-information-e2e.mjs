import { chromium } from "playwright";
import { mkdir, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";

const baseUrl = process.env.IK_E2E_BASE_URL ?? "http://127.0.0.1:5107";
const username = process.env.IK_E2E_USERNAME ?? "admin";
const password = process.env.IK_E2E_PASSWORD ?? "admin123";
const employeeUsername = process.env.IK_E2E_EMPLOYEE_USERNAME ?? "user";
const employeePassword = process.env.IK_E2E_EMPLOYEE_PASSWORD ?? "user123";
const proofPath = resolve(".bet-task/evidence/employee-information-e2e.json");
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
    await employeePage.getByLabel("Belge Türü", { exact: true }).fill("Pasaport");
    await employeePage.getByLabel("Belge Numarası", { exact: true }).fill(`E2E-${Date.now()}`);
    await employeePage.getByRole("button", { name: "Kaydet", exact: true }).click();
    await employeePage.getByText("Kimlik/belge bilgisi kaydedildi.", { exact: true }).waitFor();
    const identityRow = employeePage.getByRole("row", { name: /Pasaport/ }).first();
    await identityRow.getByRole("button", { name: "Kimlik veya belge dosyalarını yönet" }).click();
    await employeePage.getByLabel("Belge dosyası seç").setInputFiles({
      name: "e2e-kimlik-belgesi.pdf",
      mimeType: "application/pdf",
      buffer: Buffer.from("%PDF-1.4\n1 0 obj\n<<>>\nendobj\n%%EOF\n")
    });
    await employeePage.getByText("e2e-kimlik-belgesi.pdf kimlik/belge kaydına eklendi.", { exact: true }).waitFor();
    await employeePage.getByRole("link", { name: /e2e-kimlik-belgesi\.pdf belgesini indir/ }).waitFor();
    await employeeContext.close();

    const context = await browser.newContext({ viewport: viewports[0] });
    const page = await context.newPage();
    observeBrowserErrors(page, browserErrors);
    await login(page, username, password);

    await page.goto(`${baseUrl}/EmployeeBankAccounts`);
    await page.getByText("Personel Bilgileri", { exact: true }).first().click();
    for (const route of routes) {
      await page.locator(`a[href="${route}"]`).waitFor();
    }

    await page.getByRole("button", { name: "Banka kaydı ekle" }).click();
    await page.getByRole("button", { name: "Kaydet" }).click();
    await page.getByText("Banka zorunludur.", { exact: true }).waitFor();

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

    const createdRow = page.getByRole("row", { name: new RegExp(bankName) }).first();
    await createdRow.getByRole("button", { name: "Banka kaydını sil" }).click();
    await page.getByRole("button", { name: "Sil", exact: true }).click();
    await page.getByText("Banka bilgisi silindi.", { exact: true }).waitFor();

    const responsiveEvidence = [];
    for (const viewport of viewports) {
      await page.setViewportSize({ width: viewport.width, height: viewport.height });
      for (const route of routes) {
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
      validation: "required bank field",
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

async function login(page, loginUsername, loginPassword) {
  await page.goto(`${baseUrl}/login`);
  await page.locator('input[name="Username"]').fill(loginUsername);
  await page.locator('input[name="Password"]').fill(loginPassword);
  await page.getByRole("button", { name: "GİRİŞ YAP" }).click();
  await page.waitForURL(url => !url.pathname.startsWith("/login"));
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
      "authenticated-entrypoint",
      "authorization-negative",
      "ownership-scoped-self-service",
      "single-document-ui-authority",
      "form-validation",
      "relational-persistence",
      "responsive-rendering",
      "console-network"
    ],
    details
  };
  await writeFile(proofPath, `${JSON.stringify(proof, null, 2)}\n`, "utf8");
}
