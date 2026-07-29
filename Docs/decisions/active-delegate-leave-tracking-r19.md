# Active Delegate and Leave Tracking — Revision 19

> **Superseded UI decision:** Revision 22 replaces the manual-transfer location described here. This file remains historical evidence; the current and sole transfer UI is `/LeaveApprovals`.

This decision note is the canonical authority for the active-delegate and leave-tracking behavior introduced after the Phase 1 architecture baseline. Where the older Phase 1 document describes single-level leave delegation, this note supersedes that specific behavior.

## Effective manager authority

- `Department.ManagerEmployeeId` remains the primary manager.
- `Department.ActiveDelegateEmployeeId` is the sole persisted current effective-manager override.
- Open `ManagerDelegation` rows are lifecycle history; they do not form a second current-authority path.
- Ordinary employees report to the effective manager. The primary manager and active delegate report to the direct parent department's effective manager. A top-level manager has no manager.
- Manager or parent-department changes are blocked while a delegation chain is open. Hierarchy cycles remain invalid.

## Delegation lifecycle

- A primary manager or current active delegate requesting their own leave must select an active same-department employee outside the open delegation chain.
- HR-approved leave activates the selected delegate only during the inclusive leave period.
- At Revision 19, the current active delegate could transfer authority manually from `/LeaveTracking` without taking leave. Revision 22 moved that sole UI to `/LeaveApprovals`; the backend continues to recheck actor identity, active status, department ownership, chain membership, and cycle safety.
- A delegate's approved leave creates a nested delegation. `ManagerDelegation.ParentManagerDelegationId` records that lifecycle.
- When a nested delegate returns, authority unwinds to the previous delegate. When the primary manager returns, the complete chain closes and `ActiveDelegateEmployeeId` is cleared.
- Every transition recomputes employee managers, child-department manager relationships, and pending manager-review approvers in one serializable transaction.
- `ManagerDelegationWorker` reconciles at startup and daily; repeated reconciliation is idempotent.

## Leave tracking authorization and presentation

- `LeaveTrackingService` is the backend visibility authority for `/LeaveTracking`.
- An authenticated employee can query only their own department.
- Principals with `CanViewLeaveRequests` may query all departments or a selected department and see the selected-day workforce roster.
- Pending manager/HR review periods are yellow; approved periods are green.
- The calendar shows the request-creation actor and final approval employee, but never exposes the leave reason.
- `İzinde`, `Talep Bekliyor`, and `Çalışıyor` are derived from the authoritative leave requests for the selected day; no duplicate attendance state is stored.

## Schema cutover

`Migrations/20260729070505_AddActiveDepartmentDelegateAndLeaveTracking.cs` completes the DEVELOPMENT cutover. It removes the former `ManagerDelegation.IsActive` shadow field and its filtered index, adds the department active-delegate pointer and delegation parent link, permits a null leave source for manual transfers, and extends the audit-action constraint through action 32.
