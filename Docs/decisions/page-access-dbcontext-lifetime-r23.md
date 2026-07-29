# Sayfa erişimi DbContext yaşam süresi — R23

## Karar

- Navbar ve sayfa erişim kontrollerinin veritabanı sorguları Blazor circuit kapsamındaki ortak `HumanResourcesDbContext` örneğini kullanmaz.
- `PageAccessService`, kayıtlı `IDbContextFactory<HumanResourcesDbContext>` üzerinden her veritabanı kontrolü için ayrı, kısa ömürlü bir context oluşturur ve sorgudan sonra context'i kapatır.
- Claim tabanlı ve veritabanına ihtiyaç duymayan izin kontrolleri sorgu oluşturmadan çalışmaya devam eder.
- Bu değişiklik şema veya EF modeli değişikliği değildir; migration gerektirmez.

## Gerekçe ve kanıt

- `Services/PageAccessService.cs` -> `CanAccessDepartmentsAsync`, `CanAccessEmployeesAsync`, `CanAccessLeaveApprovalsAsync` -> eşzamanlı navbar ve sayfa render sorgularının aynı context üzerinde çakışmasını engeller.
- `Program.cs` -> `AddDbContextFactory<HumanResourcesDbContext>` -> üretim context yaşam süresinin tek oluşturma otoritesidir.
- `Tests/LeaveTrackingTests.cs` -> `PageAccessDatabaseChecks_CreateIndependentContexts` -> eşzamanlı üç erişim kontrolünün üç ayrı context oluşturduğunu doğrular.
