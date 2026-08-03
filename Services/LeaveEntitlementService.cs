using IK.Web.Models;

namespace IK.Web.Services;

public sealed class LeaveEntitlementService
{
    public LeaveEntitlementEvaluation Calculate(
        Employee employee,
        LeaveType leaveType,
        int year,
        DateOnly? asOfDate = null)
    {
        if (year < 2000)
        {
            throw new InvalidOperationException("İzin bakiyesi yılı 2000 veya sonrası olmalıdır.");
        }

        ValidateConfiguredAmount(leaveType.AnnualQuota, "Yıllık kota");
        var effectiveDate = asOfDate ?? new DateOnly(year, 12, 31);

        return leaveType.EntitlementKind switch
        {
            LeaveEntitlementKind.Manual => Eligible(leaveType, leaveType.AnnualQuota),
            LeaveEntitlementKind.FemaleEmployees => employee.Gender == EmployeeGender.Female
                ? Eligible(leaveType, leaveType.AnnualQuota)
                : Ineligible(leaveType, "Bu izin türü yalnız kadın çalışanlara atanabilir."),
            LeaveEntitlementKind.AllEmployees => CalculateAllEmployeeEntitlement(
                employee,
                leaveType,
                year,
                effectiveDate),
            LeaveEntitlementKind.ServiceYears0To10
                or LeaveEntitlementKind.ServiceYears10To20
                or LeaveEntitlementKind.ServiceYears20Plus =>
                CalculateServiceTier(employee, leaveType, year, effectiveDate),
            _ => throw new InvalidOperationException("İzin türünün hak ediş kuralı geçersiz.")
        };
    }

    public static bool IsServiceTier(LeaveEntitlementKind entitlementKind) =>
        entitlementKind is
            LeaveEntitlementKind.ServiceYears0To10
            or LeaveEntitlementKind.ServiceYears10To20
            or LeaveEntitlementKind.ServiceYears20Plus;

    private static LeaveEntitlementEvaluation CalculateAllEmployeeEntitlement(
        Employee employee,
        LeaveType leaveType,
        int year,
        DateOnly asOfDate)
    {
        if (!TryGetEmploymentWindow(employee, year, asOfDate, out var start, out var end, out var failure))
        {
            return Ineligible(
                leaveType,
                failure!,
                missingStartDate: employee.StartDate is null);
        }

        var eligibleDays = end.DayNumber - start.DayNumber + 1;
        return Eligible(
            leaveType,
            CalculateWholeDayProration(leaveType.AnnualQuota, eligibleDays, DaysInYear(year)));
    }

    private static LeaveEntitlementEvaluation CalculateServiceTier(
        Employee employee,
        LeaveType leaveType,
        int year,
        DateOnly asOfDate)
    {
        if (!TryGetEmploymentWindow(employee, year, asOfDate, out var start, out var end, out var failure))
        {
            return Ineligible(
                leaveType,
                failure!,
                missingStartDate: employee.StartDate is null);
        }

        var employmentStart = DateOnly.FromDateTime(employee.StartDate!.Value.Date);
        var eligibleDays = 0;
        for (var date = start; date <= end; date = date.AddDays(1))
        {
            if (TierFor(CompletedServiceYears(employmentStart, date)) == leaveType.EntitlementKind)
            {
                eligibleDays++;
            }
        }

        if (eligibleDays == 0)
        {
            return Ineligible(leaveType, "Çalışan bu kıdem kademesine uygun değil.");
        }

        return Eligible(
            leaveType,
            CalculateWholeDayProration(leaveType.AnnualQuota, eligibleDays, DaysInYear(year)));
    }

    private static bool TryGetEmploymentWindow(
        Employee employee,
        int year,
        DateOnly asOfDate,
        out DateOnly start,
        out DateOnly end,
        out string? failure)
    {
        start = default;
        end = default;
        failure = null;
        if (employee.StartDate is null)
        {
            failure = "Çalışanın işe başlama tarihi olmadığı için otomatik izin hesaplanamadı.";
            return false;
        }

        var employmentStart = DateOnly.FromDateTime(employee.StartDate.Value.Date);
        var yearStart = new DateOnly(year, 1, 1);
        var yearEnd = new DateOnly(year, 12, 31);
        if (employmentStart > yearEnd || employmentStart > asOfDate)
        {
            failure = "Çalışan seçilen tarihte henüz işe başlamamış.";
            return false;
        }

        start = employmentStart > yearStart ? employmentStart : yearStart;
        end = yearEnd;
        return true;
    }

    private static decimal CalculateWholeDayProration(
        decimal annualQuota,
        int eligibleCalendarDays,
        int calendarDaysInYear) =>
        decimal.Floor(annualQuota * eligibleCalendarDays / calendarDaysInYear);

    private static int DaysInYear(int year) => DateTime.IsLeapYear(year) ? 366 : 365;

    private static int CompletedServiceYears(DateOnly startDate, DateOnly asOfDate)
    {
        if (asOfDate < startDate)
        {
            return 0;
        }

        var years = asOfDate.Year - startDate.Year;
        if (startDate.AddYears(years) > asOfDate)
        {
            years--;
        }

        return Math.Max(0, years);
    }

    private static LeaveEntitlementKind TierFor(int completedServiceYears)
    {
        if (completedServiceYears < 10)
        {
            return LeaveEntitlementKind.ServiceYears0To10;
        }

        return completedServiceYears < 20
            ? LeaveEntitlementKind.ServiceYears10To20
            : LeaveEntitlementKind.ServiceYears20Plus;
    }

    private static void ValidateConfiguredAmount(decimal days, string fieldName)
    {
        if (days < 0m || decimal.Truncate(days * 2m) != days * 2m)
        {
            throw new InvalidOperationException(
                $"{fieldName} negatif olamaz ve yalnız tam ya da yarım gün olabilir.");
        }
    }

    private static LeaveEntitlementEvaluation Eligible(LeaveType leaveType, decimal entitledDays)
    {
        return new LeaveEntitlementEvaluation(
            leaveType.LeaveTypeId,
            true,
            entitledDays,
            false,
            null);
    }

    private static LeaveEntitlementEvaluation Ineligible(
        LeaveType leaveType,
        string reason,
        bool missingStartDate = false)
    {
        return new LeaveEntitlementEvaluation(
            leaveType.LeaveTypeId,
            false,
            0m,
            missingStartDate,
            reason);
    }
}

public sealed record LeaveEntitlementEvaluation(
    int LeaveTypeId,
    bool Eligible,
    decimal EntitledDays,
    bool MissingStartDate,
    string? Reason);
