using IK.Web.Database;
using IK.Web.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace IK.Web.Services;

public sealed record EmployeeTerminationSearchCriteria(
    string? Employee,
    string? Reason,
    DateOnly? StartDate,
    DateOnly? EndDate);

public sealed record EmployeeTerminationPage(
    IReadOnlyList<EmployeeTermination> Items,
    int TotalItems);

public sealed record EmployeeTerminationCommand(
    int? EmployeeTerminationId,
    int EmployeeId,
    byte[] EmployeeRowVersion,
    DateOnly TerminationDate,
    string Reason,
    string? Description);

public sealed class EmployeeTerminationService(
    IDbContextFactory<HumanResourcesDbContext> dbContextFactory,
    PageAccessService pageAccessService)
{
    public async Task<EmployeeTerminationPage> GetPageAsync(
        ClaimsPrincipal? principal,
        int page,
        int pageSize,
        EmployeeTerminationSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthorized(principal);
        await using var database = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = ApplySearch(
            database.EmployeeTerminations
                .AsNoTracking()
                .Include(item => item.Employee)
                .ThenInclude(employee => employee.Department),
            criteria);
        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.TerminationDate)
            .ThenByDescending(item => item.EmployeeTerminationId)
            .Skip(Math.Max(0, page) * Math.Max(1, pageSize))
            .Take(Math.Max(1, pageSize))
            .ToListAsync(cancellationToken);
        return new EmployeeTerminationPage(items, totalItems);
    }

    public async Task<EmployeeTermination?> GetAsync(
        ClaimsPrincipal? principal,
        int terminationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthorized(principal);
        await using var database = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await database.EmployeeTerminations
            .AsNoTracking()
            .Include(item => item.Employee)
            .ThenInclude(employee => employee.Department)
            .SingleOrDefaultAsync(item => item.EmployeeTerminationId == terminationId, cancellationToken);
    }

    public async Task<IReadOnlyList<Employee>> SearchEligibleEmployeesAsync(
        ClaimsPrincipal? principal,
        string? value,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthorized(principal);
        await using var database = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = database.Employees
            .AsNoTracking()
            .Where(employee => employee.Status == EmploymentStatus.Active)
            .Where(employee => !database.EmployeeTerminations.Any(item => item.EmployeeId == employee.EmployeeId));
        if (!string.IsNullOrWhiteSpace(value))
        {
            var search = value.Trim();
            query = query.Where(employee =>
                employee.FirstName.Contains(search)
                || employee.LastName.Contains(search)
                || (employee.FirstName + " " + employee.LastName).Contains(search)
                || employee.SicilNo.Contains(search));
        }
        return await query
            .OrderBy(employee => employee.FirstName)
            .ThenBy(employee => employee.LastName)
            .Take(Math.Clamp(limit, 1, 50))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> SearchRecordedEmployeesAsync(
        ClaimsPrincipal? principal,
        string? value,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthorized(principal);
        await using var database = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = database.EmployeeTerminations.AsNoTracking().Select(item => item.Employee);
        if (!string.IsNullOrWhiteSpace(value))
        {
            var search = value.Trim();
            query = query.Where(employee =>
                employee.FirstName.Contains(search)
                || employee.LastName.Contains(search)
                || (employee.FirstName + " " + employee.LastName).Contains(search)
                || employee.SicilNo.Contains(search));
        }
        return await query
            .OrderBy(employee => employee.FirstName)
            .ThenBy(employee => employee.LastName)
            .Select(employee => employee.FirstName + " " + employee.LastName + " (" + employee.SicilNo + ")")
            .Take(Math.Clamp(limit, 1, 50))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> SearchReasonsAsync(
        ClaimsPrincipal? principal,
        string? value,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthorized(principal);
        await using var database = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = database.EmployeeTerminations.AsNoTracking().Select(item => item.Reason);
        if (!string.IsNullOrWhiteSpace(value))
        {
            var search = value.Trim();
            query = query.Where(reason => reason.Contains(search));
        }
        return await query
            .Distinct()
            .OrderBy(reason => reason)
            .Take(Math.Clamp(limit, 1, 50))
            .ToListAsync(cancellationToken);
    }

    public async Task<EmployeeTermination> SaveAsync(
        ClaimsPrincipal? principal,
        EmployeeTerminationCommand command,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthorized(principal);
        var reason = NormalizeRequired(command.Reason, "Ayrılma nedeni zorunludur.");
        var description = NormalizeOptional(command.Description);
        await using var database = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var employee = await database.Employees.SingleOrDefaultAsync(
            item => item.EmployeeId == command.EmployeeId,
            cancellationToken) ?? throw new InvalidOperationException("Seçilen çalışan bulunamadı.");
        ApplyEmployeeConcurrency(database, employee, command.EmployeeRowVersion);
        if (employee.StartDate.HasValue
            && command.TerminationDate < DateOnly.FromDateTime(employee.StartDate.Value))
        {
            throw new InvalidOperationException("İşten ayrılma tarihi işe başlama tarihinden önce olamaz.");
        }

        EmployeeTermination entity;
        AuditActionType actionType;
        if (command.EmployeeTerminationId.HasValue)
        {
            entity = await database.EmployeeTerminations.SingleOrDefaultAsync(
                item => item.EmployeeTerminationId == command.EmployeeTerminationId.Value
                        && item.EmployeeId == command.EmployeeId,
                cancellationToken) ?? throw new InvalidOperationException("Ayrılma kaydı artık bulunmuyor.");
            actionType = AuditActionType.EmployeeTerminationUpdated;
        }
        else
        {
            if (employee.Status != EmploymentStatus.Active)
                throw new InvalidOperationException("Yalnızca aktif çalışanlar için yeni ayrılma kaydı oluşturulabilir.");
            if (await database.EmployeeTerminations.AnyAsync(item => item.EmployeeId == command.EmployeeId, cancellationToken))
                throw new InvalidOperationException("Bu çalışan için zaten bir işten ayrılma kaydı bulunuyor.");
            entity = new EmployeeTermination { EmployeeId = command.EmployeeId };
            database.EmployeeTerminations.Add(entity);
            actionType = AuditActionType.EmployeeTerminationCreated;
        }

        entity.TerminationDate = command.TerminationDate;
        entity.Reason = reason;
        entity.Description = description;
        employee.Status = EmploymentStatus.Passive;
        employee.UpdatedAt = DateTimeOffset.UtcNow;
        await new AuditLogService(database).AppendAsync(
            actionType,
            nameof(EmployeeTermination),
            employee.EmployeeId.ToString(),
            principal.GetRequiredEmployeeId(),
            $"EmployeeId={employee.EmployeeId}; Date={command.TerminationDate:yyyy-MM-dd}; Reason={reason}; Status=Passive",
            cancellationToken);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return entity;
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException("Çalışan veya ayrılma kaydı başka bir kullanıcı tarafından değiştirildi. Listeyi yenileyip tekrar deneyin.");
        }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException("Ayrılma kaydı kaydedilemedi. Çalışanın başka bir ayrılma kaydı olabilir.");
        }
    }

    public async Task DeleteAsync(
        ClaimsPrincipal? principal,
        int terminationId,
        byte[] employeeRowVersion,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthorized(principal);
        await using var database = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await database.EmployeeTerminations
            .Include(item => item.Employee)
            .SingleOrDefaultAsync(item => item.EmployeeTerminationId == terminationId, cancellationToken)
            ?? throw new InvalidOperationException("Ayrılma kaydı artık bulunmuyor.");
        ApplyEmployeeConcurrency(database, entity.Employee, employeeRowVersion);
        entity.Employee.UpdatedAt = DateTimeOffset.UtcNow;
        database.EmployeeTerminations.Remove(entity);
        await new AuditLogService(database).AppendAsync(
            AuditActionType.EmployeeTerminationDeleted,
            nameof(EmployeeTermination),
            entity.EmployeeId.ToString(),
            principal.GetRequiredEmployeeId(),
            "İşten ayrılma kaydı silindi; çalışan durumu değiştirilmedi.",
            cancellationToken);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException("Çalışan veya ayrılma kaydı başka bir kullanıcı tarafından değiştirildi. Listeyi yenileyip tekrar deneyin.");
        }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException("Ayrılma kaydı silinemedi. Listeyi yenileyip tekrar deneyin.");
        }
    }

    private static IQueryable<EmployeeTermination> ApplySearch(
        IQueryable<EmployeeTermination> query,
        EmployeeTerminationSearchCriteria criteria)
    {
        if (!string.IsNullOrWhiteSpace(criteria.Employee))
        {
            var employee = criteria.Employee.Trim();
            query = query.Where(item =>
                item.Employee.FirstName.Contains(employee)
                || item.Employee.LastName.Contains(employee)
                || (item.Employee.FirstName + " " + item.Employee.LastName).Contains(employee)
                || item.Employee.SicilNo.Contains(employee)
                || (item.Employee.FirstName + " " + item.Employee.LastName + " (" + item.Employee.SicilNo + ")") == employee);
        }
        if (!string.IsNullOrWhiteSpace(criteria.Reason))
            query = query.Where(item => item.Reason.Contains(criteria.Reason.Trim()));
        if (criteria.StartDate.HasValue)
            query = query.Where(item => item.TerminationDate >= criteria.StartDate.Value);
        if (criteria.EndDate.HasValue)
            query = query.Where(item => item.TerminationDate <= criteria.EndDate.Value);
        return query;
    }

    private void EnsureAuthorized(ClaimsPrincipal? principal)
    {
        if (!pageAccessService.CanManageEmployeeTerminations(principal))
            throw new UnauthorizedAccessException("İşten ayrılma kayıtlarını yönetme yetkiniz bulunmuyor.");
    }

    private static void ApplyEmployeeConcurrency(
        HumanResourcesDbContext database,
        Employee employee,
        byte[] rowVersion)
    {
        if (rowVersion.Length == 0 || !employee.RowVersion.SequenceEqual(rowVersion))
            throw new InvalidOperationException("Çalışan veya ayrılma kaydı başka bir kullanıcı tarafından değiştirildi. Listeyi yenileyip tekrar deneyin.");
        database.Entry(employee).Property(item => item.RowVersion).OriginalValue = rowVersion;
    }

    private static string NormalizeRequired(string value, string error) =>
        string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException(error) : value.Trim();
    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
