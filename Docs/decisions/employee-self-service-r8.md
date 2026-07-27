# Employee Self-Service Decision (Revision 8)

Authenticated employees may manage structured personnel information only for
the `EmployeeId` in their own claims. A principal with
`CanCreateNewEmployee` may select and manage another employee.

Profile-photo upload belongs only to the General Information page. Identity,
education, and course/certificate document upload and listing belong to the
related record on their respective personnel-information pages. The dashboard
does not provide document upload or listing controls.

Backend ownership checks remain authoritative for loading, saving, deleting,
uploading, and downloading. Navigation visibility is not an authorization
boundary.
