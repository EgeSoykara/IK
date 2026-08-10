# IK.Web

Blazor Server application for the HR Personnel Registry and Leave Module.

## Current Phase
Phase 1 implements the EF Core and MSSQL-backed domain foundation for:

- personnel and department hierarchy
- regional manager or higher manager checks
- leave types
- leave type carry-over rules and maximum accrual thresholds
- yearly leave balances
- January 1 automatic, idempotent leave entitlement assignment
- service-year and gender eligibility with transition-year proration
- person, department, or all-active-employee balance assignment
- previous-year carry-over
- 50-day carry-over warning with confirmation
- leave requests
- manager approval followed by Human Resources final approval
- audit logs
- local cookie-auth login with database-backed roles and permission claims
- optional unique employee e-mail addresses for future directory matching
- authenticated employee profile photos and record-linked personal documents
- structured employee bank accounts, identity documents, phones, addresses, education, courses/certificates, and termination details

LDAP / Active Directory login is a Phase 2 decision and is not implemented in Phase 1.
The local Phase 1 login page uses `StaticUserAuthenticator` only as the current `IUserAuthenticator` credential provider, and each static login user must reference an existing `Employees.EmployeeId`. The login endpoint and claim pipeline depend on the interface, so a future directory provider replaces this single DI binding rather than the application authorization model.
ASP.NET Core cookie authentication builds its claim-bearing `ClaimsPrincipal` from the employee's persisted `ApplicationRoleId` and the role's persisted `ApplicationRolePermissions`.
The Phase 1 permission authority is claim-based: `CanManageDepartments`, `CanviewEmployeeSearch`, `CanCreateNewEmployee`, `CanManageLeaveTypes`, `CanManagePublicHolidays`, `CanManageLeaveBalances`, `CanViewLeaveRequests`, `CanManageLeaveRequests`, `CanEditDeleteLeaveRequests`, `CanExectuteApproveLeave`, `CanActAsHumanResources`, and `CanViewAuditLogs`. `CanActAsHumanResources` is seeded only for the İnsan Kaynakları role and owns HR-stage approval visibility and action semantics without a role-name check.
Roles are stored in `ApplicationRoles`; role-permission assignments are stored in `ApplicationRolePermissions`; every employee has one required role foreign key. The bundled roles are Çalışan, Yönetici, and İnsan Kaynakları. Their permission rows are seed data and are read from the database at login—there is no static role enum, role dictionary, or per-user permission override.
Department management responsibility drives the bundled Çalışan/Yönetici role transition. Assigning a Çalışan as a department manager or active leave delegate promotes that employee to Yönetici. When the last such responsibility ends, a Yönetici returns to Çalışan. İnsan Kaynakları and custom roles are never overwritten by this automation; an employee responsible for another department remains Yönetici. Existing browser sessions use the claims issued at login, so a changed role takes effect after signing in again.
The bundled static credential provider is linked to existing employee rows by `Services/StaticUserAuthenticator.cs`: `user -> EmployeeId 2`, `admin -> EmployeeId 1`, and `hr -> EmployeeId 1002`. Employee e-mail is optional and unique when present, ready to become the future directory-to-employee matching key after the AD mapping decision.
On a clean database, `/Departments` stays open only until the first department is created and then routes to `/Employees`; `/Employees` stays open only until the first employee is created. The first employee's role is server-forced to Yönetici so the `admin` credential can continue configuration, both bootstrap writes use the `initial-configuration` system audit actor, and the employee step routes immediately to login. This anonymous bootstrap never grants elevated access to signed-in users and each step is serialized against concurrent first-record creation.
Signed-in shell access is principal-based, and page-specific or cross-employee access is permission-claim-based through `PageAccessService`.
Structured personnel-information pages are available as a shared tab set under the `Personel Bilgileri` navigation group. An authenticated employee with an `EmployeeId` claim can view and change only their own permitted records; `CanCreateNewEmployee` elevates the user to select and manage other employees. Termination information is excluded from ordinary employee navigation and server-side access and remains available only to elevated employee managers. `Unvan/Pozisyon` is intentionally outside the current delivery.

## Run
Use the .NET SDK configured for the project:

```powershell
dotnet restore IKSolution.slnx
dotnet build IKSolution.slnx
dotnet run --project IK.Web.csproj
```

If `dotnet` is not on `PATH`, use the user-local SDK:

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" restore IKSolution.slnx
& "$env:USERPROFILE\.dotnet\dotnet.exe" build IKSolution.slnx
& "$env:USERPROFILE\.dotnet\dotnet.exe" run --project IK.Web.csproj
```

## Database
The connection string key is `ConnectionStrings:HumanResources`. The checked-in `appsettings.json` value is a placeholder and must be overridden with user secrets or an environment variable before running against a real SQL Server:

```powershell
dotnet user-secrets set "ConnectionStrings:HumanResources" "<real connection string>"
```

```bash
export ConnectionStrings__HumanResources="<real connection string>"
```

Any previously shared real SQL password should be rotated outside the application repository.

The aligned MSSQL creation script is:

```text
Database/001_create_human_resources_schema.sql
```

Apply EF migrations to an existing database before using employee files:

```bash
dotnet ef database update --project IK.Web.csproj
```

## Leave Entitlements

The five default leave types start at 30 days. The three service-year types carry over; sickness and pregnancy do not. Employees with 30 or more service years remain in the top tier, and the existing 50-day maximum-accrual warning applies to every type.

Persisted policy values are maintained on `/LeaveTypes` and stored in the `LeaveTypes` table. The clean-database seed is in `Database/HumanResourcesDbContext.cs`; operating policy and worker configuration are documented in `Docs/leave-entitlement-policy.md`.

`DailyLeaveEntitlementWorker` reconciles current-year entitlements at startup and every local day. New hires receive a whole-day, calendar-day-prorated grant on their start date; pregnancy leave remains manual. It can be disabled with `DailyLeaveEntitlementWorker__Enabled=false`; retry delay is configured with `DailyLeaveEntitlementWorker__RetryDelayMinutes`.

Leave amounts accept only whole or half days. Employees request the aggregated `Annual Leave` or `Sickness Leave` category; annual approval consumes carry-over before current entitlement. Carry-over warnings are managed on `/LeaveCarryOverWarnings`.

The DEVELOPMENT migration `20260803072742_DailyLeaveEntitlementsAndCategories` is an intentional clean reset of legacy leave requests, approvals, balances, leave-driven delegations, and related audit rows. Employee and department records remain intact. Restore the pre-migration database backup to roll back this irreversible cutover.

## Employee Files

Profile photos and personal documents are stored outside `wwwroot`; authenticated employees can read their own files and principals with `CanCreateNewEmployee` can read the selected employee's files. Profile-photo upload lives on `Personel Bilgileri`; identity, diploma, and certificate uploads live on their related identity, education, or course/certificate record. The default root is the git-ignored `App_Data/employee-files` folder. Override it for an operator-managed volume with:

```bash
export EmployeeFiles__RootPath="/absolute/operator-managed/path"
```

Startup rejects a configured root equal to or beneath `wwwroot`.

Storage keys are canonical and contain no user-supplied file names:

```text
employees/{employee-id}/profile-photo/{file-id}.{extension}
employees/{employee-id}/documents/{category-canonical-key}/{file-id}.{extension}
```

Document categories are DB-backed by `EmployeeDocumentCategories.CanonicalKey`. The initial canonical keys are `identity`, `employment`, `education`, `health`, and `other`; later categories can be added as data without changing the document schema. Profile photos accept JPG, PNG, or WEBP up to 5 MB; documents accept PDF, DOCX, JPG, or PNG up to 20 MB.

## Personnel Information

Authenticated employees manage their own structured personnel details through separate routes. Users with `CanCreateNewEmployee` can select and manage another employee:

- `/EmployeePersonnelInformation`
- `/EmployeeBankAccounts`
- `/EmployeeIdentityDocuments`
- `/EmployeePhones`
- `/EmployeeAddresses`
- `/EmployeeEducations`
- `/EmployeeCourseCertificates`

`/EmployeeTerminations` is visible and accessible only to users with `CanCreateNewEmployee`; ordinary employee responses and page queries do not load termination data.

Bank accounts, identity documents, phones, addresses, education records, and course/certificate records support multiple rows per employee. Filtered unique database indexes enforce at most one primary bank, phone, and address record per employee, and primary promotion runs in an explicit transaction. Expected constraint, concurrency, and stale-record failures return generic feedback without exposing PII. Termination details are one-to-one with the employee; saving a termination record also sets the existing employee status to `Passive`. Removing the termination detail does not silently reactivate the employee.

The dashboard has no upload control. `EmployeePersonnelInformation` combines the employee's sicil, personal, organization, and profile-photo surfaces in one tab; identity, diploma, and certificate files are uploaded from and linked to their identity, education, or course/certificate record. Existing files are preserved, and only unambiguous legacy matches are backfilled.

Apply the personnel-information migrations (`20260727000000_AddPersonnelInformation`, `20260727080824_EnforcePrimaryPersonnelRecords`, `20260727110418_LinkEmployeeDocumentsToPersonnelRecords`, and `20260727111934_EnforceEmployeeDocumentRelatedOwnership`) before using these routes:

```bash
dotnet ef database update --project IK.Web.csproj
```

The combined personnel UI E2E command is `bash .bet-task/scripts/run-employee-information-e2e.sh`. It requires `IK_E2E_CONNECTION_STRING` with a disposable database name beginning with `IK_E2E_`; the fixture tool refuses any other database name, recreates the fixture database, and deletes it during cleanup.

## Documentation
- `Docs/phase-1-architecture.md`
- `Docs/leave-entitlement-policy.md`
- `Docs/TODO-TOIMPLEMENT-LIST.md`

## Validation

```powershell
dotnet build IKSolution.slnx
```
