# UI/UX Regression Checklist

Run this checklist after UI/UX implementation, visual polish, final-touch, or regression-fix work.

## Required Checks
- Desktop viewport renders without blank content, unreadable text, incoherent overlap, or broken navigation states.
- Mobile viewport renders without blank content, unreadable text, incoherent overlap, or horizontal layout failure.
- Authenticated shell navigation shows only permitted links and has a clear active state.
- Login, authenticated dashboard, and changed routes render without unhandled exception markers.
- Tables, cards, buttons, inputs, labels, and chips keep stable spacing and readable contrast.
- External visual dependencies are avoided unless explicitly approved and documented.
- Existing behavior, data authority, authentication, permissions, and workflow ownership remain unchanged.

## Evidence: 2026-07-13 Stitch Dashboard Application
- Desktop dashboard checked at 1280x900 with `admin/admin123`; `/` rendered the Stitch dashboard without blank content, overlap, or unreadable active navigation.
- Mobile dashboard checked at 390x844 with `admin/admin123`; cards and tables collapsed into readable single-column responsive layouts without horizontal failure.
- Login checked at 1280x900; app bar, card, inputs, and submit button rendered without overlap.
- Navigation active state checked by computed style: `Dashboard` rendered as white text on the indigo active background with a secondary left border.
- Authenticated route smoke checked `/Employees`, `/Departments`, `/LeaveRequests`, `/LeaveApprovals`, and `/AuditLogs`; no unhandled exception marker appeared.
- Permission-gated shell checked with `admin/admin123`; 8 available modules appeared, matching the admin permission set.
- `user/user123` and `hr/hr123` returned `/login?error=unconfigured` because the active local database does not contain their linked employee rows; no seed data was mutated for this UI task.
- The external Google Fonts dependency was removed; typography now uses the local/system font stack.

## Evidence: 2026-07-13 Leave Balance Name Display
- Leave balance table checked so `Çalışan` and `İzin Türü` columns render employee and leave type names instead of raw `EmployeeId` and `LeaveTypeId` values.
- Leave balance search dialog checked so employee and leave type filters use typed name text fields, matching the other search dialogs instead of exposing numeric ID fields or fixed selects.
- Existing create/edit form behavior remains service-backed and continues to submit the selected IDs internally without exposing the IDs as table/search labels.

## Evidence: 2026-07-13 Leave Request Name Display
- Leave request table checked so `Çalışan` and `İzin Türü` columns render employee and leave type names instead of raw `EmployeeId` and `LeaveTypeId` values.
- Leave request search dialog checked so employee and leave type filters use typed name text fields instead of numeric ID fields.
- Existing create/edit form behavior remains service-backed and continues to submit the selected IDs internally.

## Evidence: 2026-07-14 Stitch Management Pattern
- Stitch project `projects/12904864656943999098` was fetched successfully with OWNER access; screen `İzin Talepleri Paneli` supplied the Professional HR Authority tokens and layout reference.
- `dotnet build IK.Web.csproj` passed with only the existing `initial` migration type-name warnings.
- `dotnet test Tests/IK.Web.Tests.csproj` passed 2 tests.
- Authenticated Playwright smoke with `admin/admin123` checked `/Employees`, `/Departments`, `/LeaveTypes`, `/LeaveBalances`, `/LeaveRequests`, `/LeaveApprovals`, and `/AuditLogs` at 1280x900; every route rendered one `management-shell`, one `management-data-table`, no exception marker, no body-level horizontal overflow, and a clear active navigation state.
- Desktop management screenshot evidence was captured for `/LeaveRequests` and `/LeaveApprovals`; icon actions, table headers, cards, and active navigation rendered without clipping or incoherent overlap.
- Mobile `/Employees`, `/Departments`, `/LeaveTypes`, `/LeaveBalances`, `/LeaveRequests`, `/LeaveApprovals`, and `/AuditLogs` were checked at 390x844; every route rendered one management shell, one management table, readable content, no exception marker, no body-level horizontal layout failure, and accessible labels on all toolbar and row action icons.
- No external visual dependency was added; the design continues to use the local/system typography stack and existing MudBlazor assets.
- Existing data authority, authentication, permissions, leave workflow services, and audit ownership were not changed.

## Evidence: 2026-07-14 Dashboard Leave Balance Total
- Dashboard total remaining-days logic checked so the `Toplam Kalan Gün` card counts only the latest yearly balance per leave type while still summing different leave types.
- `dotnet build IK.Web.csproj` passed with only the existing `initial` migration type-name warnings.
- `dotnet test Tests/IK.Web.Tests.csproj` passed 4 tests, including focused coverage for same leave type across years plus a different leave type.
- Authenticated Playwright smoke with `admin/admin123` checked `/` at 1280x900 and 390x844; each viewport rendered one `dashboard-shell`, the `Toplam Kalan Gün` card displayed the active leave-type caption, no exception marker appeared, and there was no document-level horizontal overflow.
- Existing leave renewal, carry-over, request approval, authentication, permission, and audit authorities were not changed.

## Evidence: 2026-07-14 Employee Department Manager Names
- Employee table checked so `Departman` and `Yönetici` columns render relationship names instead of raw `DepartmentId` and `ManagerId` values.
- Employee search dialog checked so department and manager filters use typed name text fields instead of numeric ID fields.
- `dotnet test IKSolution.slnx` passed 4 tests; only existing migration and unrelated leave page nullable warnings appeared.
- Authenticated Playwright smoke with `hr/hr123` checked `/Employees` at 1280x900 and 390x844; each viewport rendered one `management-shell`, one `management-data-table`, active `Çalışanlar` navigation, no exception marker, no document-level horizontal overflow, and no `Departman ID` or `Yönetici ID` labels in the table/search UI.
- Existing employee create/edit persistence remains FK-backed internally and no authentication, permission, audit, or workflow authority was changed.

## Evidence: 2026-07-14 Leave Approval Leave Type
- Leave approval table checked so `İzin Türü` renders the related leave request's leave type name.
- Leave approval search dialog checked so leave approvals can be filtered by typed leave type name.
- `dotnet test IKSolution.slnx` passed 4 tests; only existing migration and unrelated leave page nullable warnings appeared.
- Authenticated Playwright smoke with `hr/hr123` checked `/LeaveApprovals` at 1280x900 and 390x844; each viewport rendered one `management-shell`, one `management-data-table`, active `İzin Onayları` navigation, no exception marker, no document-level horizontal overflow, and `İzin Türü` in both the table labels and search dialog.
- Existing approval decision behavior remains service-backed through `LeaveRequestService`; no approval workflow, authentication, permission, audit, or balance authority was changed.

## Evidence: 2026-07-14 Audit Log Pagination
- Audit log table checked so `/AuditLogs` uses server-side pagination instead of the previous fixed latest-row limit; the page header now states that all audit rows are viewed page by page.
- `dotnet build IKSolution.slnx` passed.
- `dotnet test IKSolution.slnx` passed 5 tests, including focused audit log pagination coverage for total count, stable ordering, second page, and rows beyond the previous 50-row limit.
- Authenticated Playwright smoke with `admin/admin123` checked `/AuditLogs` at 1280x900 and 390x844; each viewport rendered one `management-shell`, one `management-data-table`, the pager label `Sayfa başına kayıt:`, no exception marker, and no document-level horizontal overflow.
- Desktop pager navigation moved from `1-25 / 355` to `26-50 / 355`, and the first visible audit row changed from `355` to `330`, confirming older audit rows are reachable through pagination.
- Existing audit creation authority remains service-backed through `AuditLogService`; authentication, permissions, workflow ownership, and audit write behavior were not changed.

## Evidence: 2026-07-15 Management Pagination Rollout
- `/Employees`, `/Departments`, `/LeaveTypes`, `/LeaveBalances`, `/LeaveRequests`, and `/LeaveApprovals` now use the same MudBlazor server-side table pagination pattern as `/AuditLogs`; fixed `Recent*` table lists and `Take(25)` display limits were removed from those management pages.
- `dotnet build IKSolution.slnx` passed with only the existing lowercase migration-name warnings.
- `dotnet test IKSolution.slnx` passed 8 tests, including focused shared management pagination coverage for unpaged total count, second-page access, filtered count before paging, and invalid page input normalization.
- Authenticated Playwright smoke with `admin/admin123` checked `/Employees`, `/Departments`, `/LeaveTypes`, `/LeaveBalances`, `/LeaveRequests`, `/LeaveApprovals`, and `/AuditLogs` at 1280x900 and 390x844; every route rendered one `management-shell`, one `management-data-table`, the pager label `Sayfa başına kayıt:`, no exception marker, and no document-level horizontal overflow.
- Server log polling after the final Playwright smoke showed expected EF SQL output only; no DbContext concurrency exception or Blazor unhandled exception was logged.
- Existing search filters, permissions, authentication, audit writes, and leave workflow service ownership were not changed; searches now reload the first server-side page instead of replacing the table with a fixed latest 25-row list.

## Evidence: 2026-07-15 Management Filter UX and Table Scroll
- `/Employees`, `/Departments`, `/LeaveTypes`, `/LeaveBalances`, `/LeaveRequests`, and `/LeaveApprovals` search dialogs were checked with 6, 2, 1, 2, 2, and 3 autocomplete controls respectively; numeric, date, and enum filters retained their appropriate specialized controls.
- Applied-filter state was checked on `/LeaveRequests`: the table toolbar displayed `Talep ID: 3` on the left, its accessible remove control restored the unfiltered table, and both the toolbar-wide `Tümünü temizle` action and the search-dialog `Filtreleri Temizle` action removed all applied filters.
- Search-dialog draft isolation was checked through the implementation and browser flow: `İptal` discards draft edits, while `Ara` applies the draft and reloads page zero.
- The shared management table viewport was checked with one filtered row and 25 audit rows: one row had no maximum-height constraint, while the 25-row audit page used a 1008px internal viewport with `overflow-y: auto`, `scrollHeight 1648 > clientHeight 1008`, and a sticky table header. This confirms scrolling begins only when a 16th rendered row exists.
- `dotnet build IK.Web.csproj -c Release --no-restore` passed with only the two existing lowercase migration-name warnings.
- `dotnet test Tests/IK.Web.Tests.csproj -c Release --no-restore` passed 26 tests, including focused autocomplete option matching, text normalization, active-filter callbacks, approval visibility scoping, SQL-side distinct option projection, and complete six-page/seven-table UI contract coverage.
- Authenticated Playwright smoke with `admin/admin123` checked `/Employees`, `/Departments`, `/LeaveTypes`, `/LeaveBalances`, `/LeaveRequests`, `/LeaveApprovals`, and `/AuditLogs` at 1280x900 and 390x844; every route rendered one management shell and management table with zero document-level horizontal overflow, no browser page errors, and no unhandled exception marker.
- Desktop and mobile visual inspection confirmed readable autocomplete suggestions, filter chips, individual close controls, wrapped mobile toolbar actions, table content, pager controls, and stable contrast without overlap or clipping.
- No external runtime visual dependency was added. Department parent filtering intentionally moved from raw parent ID equality to autocomplete parent-name matching; page-specific visibility and permission rules, authentication, audit writes, and leave workflow service authority remain unchanged.

## Evidence: 2026-07-16 Working-Day and Half-Day Leave Requests
- `/LeaveRequests` create/edit replaced time-derived duration with date-only selection plus an explicit `Yarım gün izin` switch; the form displays the authoritative requested working-day amount immediately and states that weekends are excluded.
- Submit confirmation was exercised with a half-day request: the dialog stated that `0,5` working day would be deducted after final approval, and the validation flow cancelled at confirmation so no UI-smoke request was persisted.
- `dotnet test IKSolution.slnx -c Release --no-restore` passed 49 tests, including weekend exclusion, weekend-only rejection, weekend-only overlap allowance, weekday-overlap rejection, explicit half-day create/update, `0.5` final balance deduction, weekend-spanning final balance deduction, pre-cutover pending-request recalculation, date-only MSSQL model mapping, and the leave-request UI contract.
- The date-only migration script was generated from `20260709062323_initial3` to `20260716112454_UseDateOnlyLeaveRequestPeriod`; it drops the dependent period index/check constraint, alters both request period columns to MSSQL `date`, and recreates the index/check constraint in dependency-safe order.
- Authenticated Playwright smoke with `admin/admin123` checked `/LeaveRequests` at 1280x900 and 390x844; both viewports rendered the management shell, active leave-request navigation, date inputs, half-day switch, working-day alert, table, and actions without browser page errors, unhandled exception markers, document-level horizontal overflow, clipping, or unreadable contrast.
- The changed dialog exposed no native time inputs. Desktop and mobile screenshots were visually inspected; fields, labels, switch, alert, and actions retained stable spacing and remained readable.
- No external visual dependency was added. Existing authentication, permissions, manager/HR workflow ownership, audit ownership, and final-approval balance authority remain service-backed.
- The required independent post-implementation review reran Release tests/build, migration/model checks, and diff validation and graded the final implementation 10/10.

## Evidence: 2026-07-16 Single Leave Period Picker
- `/LeaveRequests` create/edit now uses one `MudDateRangePicker` for full-day periods instead of separate start/end date controls; the form keeps one `DateRange` authority for submission, preview, create, and edit behavior.
- Browser interaction selected 17–20 July 2026 from the range picker and displayed `2` working days. Enabling half-day immediately selected the range start, 17 July, displayed `0,5`, and the same field position then accepted 21 July as a different half-day date while retaining `0,5`.
- `dotnet test IKSolution.slnx -c Release --no-restore` passed 50 tests, including the focused single-range-authority and half-day-default UI contract.
- Authenticated Playwright smoke with `admin/admin123` checked the changed dialog at 1280x900 and 390x844; the range fields, conditional half-day date field, switch, working-day alert, and actions remained readable without browser page errors or document-level horizontal overflow.
- Desktop and mobile screenshots were visually inspected. The full-day control remained a single responsive selection area, labels and contrast were readable, and no external visual dependency was added.
- The required independent post-implementation review reran all 50 Release tests, the solution Release build, and diff validation and graded the final single-picker implementation 10/10.

## Evidence: 2026-07-16 Projected Remaining Leave Preview
- `/LeaveRequests` preloads each authorized employee's total remaining days with the same `LeaveBalanceDashboardSummary.SumCurrentRemainingDays` authority used by the Home dashboard, then subtracts the live working-day preview without mutating any balance or starting a database query during employee selection.
- Authenticated Playwright interaction selected the Home employee in the request form and verified exact arithmetic against Home's `Toplam Kalan Gün`: a two-workday range showed the total minus `2`, then half-day mode showed the total minus `0,5`.
- The projected text explicitly states that the balance applies when the request is approved, matching the service-owned final-approval deduction lifecycle.
- `dotnet test IKSolution.slnx -c Release --no-restore` passed all 53 tests, including exact full-day and half-day projected-balance arithmetic plus the updated leave-request UI contract.
- The changed dialog was checked at 1280x900 and 390x844 with no browser page errors or document-level horizontal overflow. Desktop and mobile screenshots were visually inspected; the longer alert wrapped cleanly and remained readable without overlap or clipping.
- The required independent post-implementation review verified authorized-data scoping, race-free synchronous employee selection, exact arithmetic, lifecycle resets, tests, docs, and diff cleanliness and graded the final implementation 10/10 production-grade.

## Evidence: 2026-07-24 Employee Profile Photo and Categorized Documents
- The dashboard contract verifies two upload controls, own-employee scoping, canonical category grouping, format-specific size limits, authenticated file routes, and a shared operation gate that disables both controls while either upload is active.
- Responsive dashboard CSS includes a single-column mobile files layout and mobile hero/avatar ordering. No external visual dependency was added; the implementation continues to use the existing MudBlazor and local/system styling authority.
- `dotnet test IKSolution.slnx -c Release --no-restore` passed all 68 tests, including file signature and limit validation, canonical-folder storage, ownership isolation, inactive-category history, atomic-write cleanup, cancelled metadata compensation, profile-photo concurrency cleanup, and database delete restrictions.
- `dotnet build IKSolution.slnx -c Release --no-restore` passed with zero errors. The only package warning is the repository's existing `AngleSharp` 1.4.0 advisory.
- The final idempotent MSSQL migration script contains the category-key check, profile `rowversion`, unique storage-key indexes, upload audit-action range, and `ON DELETE NO ACTION` for both employee-file relationships. The connected database has `20260724124154_AddEmployeeFiles` applied and EF reports no pending model changes.
- Live HTTP smoke returned `200` for `/login`, redirected unauthenticated `/` to login, and returned `401` for both employee-file download routes; the server emitted no unhandled application exception.
- The in-app Browser runtime was unavailable (`agent.browsers.list()` returned no browser), so authenticated desktop/mobile rendering and screenshot inspection could not be executed or claimed. This is the only outstanding external checklist evidence limitation.
- Existing authentication, permission, leave workflow, employee ownership, and audit authorities remain intact. The required independent post-implementation review reran the code, schema, migration, and Release checks and graded the implementation 10/10 production-grade.
