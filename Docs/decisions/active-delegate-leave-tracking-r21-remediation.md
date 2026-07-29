# Active Delegate and Leave Tracking — Revision 21 Remediation

This note is the canonical amendment to the revision-19 active-delegate and leave-tracking decision.

## Working-day presentation

The calendar and daily workforce roster use the same `LeaveDayCalculator` and `PublicHolidayCalendar` authority as leave creation. Only weekdays that are not configured public holidays are displayed as pending or approved leave days. A selected weekend or public holiday is shown as `İş günü değil`; it is never reported as pending leave or `İzinde`.

## Development migration guard

The revision-19 schema remains a complete DEVELOPMENT cutover with no legacy compatibility path. Before removing `ManagerDelegation.IsActive`, migration `20260729070505_AddActiveDepartmentDelegateAndLeaveTracking` now fails closed with SQL error `51005` if an active legacy delegation exists. Development operators must clear/reset that non-production scenario and rerun the migration; the migration never silently loses the effective-manager authority.

The isolated MSSQL migration-guard fixture creates a real active pre-cutover delegation and proves the upgrade is rejected before the legacy current-authority field is removed.
