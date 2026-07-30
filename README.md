# IK.Web

Blazor Server application for the HR Personnel Registry and Leave Module.

## Current Phase
Phase 1 implements the EF Core and MSSQL-backed domain foundation for:

- personnel and department hierarchy
- regional manager or higher manager checks
- leave types
- leave type carry-over rules and maximum accrual thresholds
- yearly leave balances
- previous-year carry-over
- 50-day carry-over warning with confirmation
- leave requests
- manager approval followed by Human Resources final approval
- audit logs
- local cookie-auth login and permission-claim authorization
- authenticated employee profile photos and record-linked personal documents
- structured employee bank accounts, identity documents, phones, addresses, education, courses/certificates, and termination details

LDAP / Active Directory login and AD group role mapping are Phase 2 decisions and are not implemented in Phase 1.
The local Phase 1 login page uses static credentials, and each static login user must reference an existing `Employees.EmployeeId`.
Phase 1 now uses ASP.NET Core cookie authentication for the local login flow, with static credentials mapped to a claim-bearing `ClaimsPrincipal` through `StaticPermissionService` and `PermissionClaimsPrincipalFactory`.
The Phase 1 permission authority is claim-based: `CanManageDepartments`, `CanviewEmployeeSearch`, `CanCreateNewEmployee`, `CanManageLeaveTypes`, `CanManageLeaveBalances`, `CanViewLeaveRequests`, `CanManageLeaveRequests`, `CanEditDeleteLeaveRequests`, `CanExectuteApproveLeave`, and `CanViewAuditLogs`.
`StaticPermissionService` resolves permissions role-first and can optionally apply per-user permission add/remove overrides on top of that baseline.
The bundled static accounts are linked to existing employee rows by `Services/StaticLoginService.cs`: `user -> EmployeeId 1`, `admin -> EmployeeId 2`, and `hr -> EmployeeId 34`.
On a clean database, `/Departments` stays open until the first department is created and `/Employees` stays open until the first employee is created, but that bootstrap bypass is anonymous only and does not grant elevated access to signed-in users.
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
- `Docs/TODO-TOIMPLEMENT-LIST.md`

## Validation

```powershell
dotnet build IKSolution.slnx
```
