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

LDAP / Active Directory login and AD group role mapping are Phase 2 decisions and are not implemented in Phase 1.
The local Phase 1 login page uses static credentials, and each static login user must reference an existing `Employees.EmployeeId`.
Phase 1 now uses ASP.NET Core cookie authentication for the local login flow, with static credentials mapped to a claim-bearing `ClaimsPrincipal` through `StaticPermissionService` and `PermissionClaimsPrincipalFactory`.
The Phase 1 permission authority is claim-based: `CanManageDepartments`, `CanviewEmployeeSearch`, `CanCreateNewEmployee`, `CanManageLeaveTypes`, `CanManageLeaveBalances`, `CanViewLeaveRequests`, `CanManageLeaveRequests`, `CanEditDeleteLeaveRequests`, `CanExectuteApproveLeave`, and `CanViewAuditLogs`.
`StaticPermissionService` resolves permissions role-first and can optionally apply per-user permission add/remove overrides on top of that baseline.
The bundled static accounts are linked to existing employee rows by `Services/StaticLoginService.cs`: `user -> EmployeeId 5`, `admin -> EmployeeId 6`, and `hr -> EmployeeId 10`.
On a clean database, `/Departments` stays open until the first department is created and `/Employees` stays open until the first employee is created, but that bootstrap bypass is anonymous only and does not grant elevated access to signed-in users.
Signed-in shell access is principal-based, and page-specific or cross-employee access is permission-claim-based through `PageAccessService`.

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

## Documentation
- `Docs/phase-1-architecture.md`
- `Docs/TODO-TOIMPLEMENT-LIST.md`

## Validation

```powershell
dotnet build IKSolution.slnx
```
