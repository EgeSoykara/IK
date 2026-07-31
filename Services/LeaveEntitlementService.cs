using IK.Web.Models;

namespace IK.Web.Services;

public sealed class LeaveEntitlementService
{
    public IReadOnlyList<LeaveEntitlementGrant> CalculateAutomaticEntitlements(
        Employee employee,
        IEnumerable<LeaveType> leaveTypes,
        int year)
    {
        return leaveTypes
            .Where(leaveType => leaveType.EntitlementKind != LeaveEntitlementKind.Manual)
            .Select(leaveType => Calculate(employee, leaveType, year))
            .Where(evaluation => evaluation.Eligible)
            .Select(evaluation => new LeaveEntitlementGrant(
                evaluation.LeaveTypeId,
                evaluation.EntitledDays))
            .ToArray();
    }

    public LeaveEntitlementEvaluation Calculate(
        Employee employee,
        LeaveType leaveType,
        int year)
    {
        if (year < 2000)
        {
            throw new InvalidOperationException("İzin bakiyesi yılı 2000 veya sonrası olmalıdır.");
        }

        return leaveType.EntitlementKind switch
        {
            LeaveEntitlementKind.Manual => Eligible(leaveType, leaveType.AnnualQuota),
            LeaveEntitlementKind.AllEmployees => Eligible(leaveType, leaveType.AnnualQuota),
            LeaveEntitlementKind.FemaleEmployees => employee.Gender == EmployeeGender.Female
                ? Eligible(leaveType, leaveType.AnnualQuota)
                : Ineligible(leaveType, "Bu izin türü yalnız kadın çalışanlara atanabilir."),
            LeaveEntitlementKind.ServiceYears0To10
                or LeaveEntitlementKind.ServiceYears10To20
                or LeaveEntitlementKind.ServiceYears20Plus =>
                CalculateServiceTier(employee, leaveType, year),
            _ => throw new InvalidOperationException("İzin türünün otomatik hak ediş kuralı geçersiz.")
        };
    }

    private static LeaveEntitlementEvaluation CalculateServiceTier(
        Employee employee,
        LeaveType leaveType,
        int year)
    {
        if (employee.StartDate is null)
        {
            return Ineligible(
                leaveType,
                "Çalışanın işe başlama tarihi olmadığı için kıdem izni hesaplanamadı.",
                missingStartDate: true);
        }

        var startDate = DateOnly.FromDateTime(employee.StartDate.Value.Date);
        var yearEnd = new DateOnly(year, 12, 31);
        if (startDate > yearEnd)
        {
            return Ineligible(leaveType, "Çalışan seçilen yılda henüz işe başlamamış.");
        }

        var eligibleMonths = 0;
        for (var month = 1; month <= 12; month++)
        {
            if (startDate.Year == year && month < startDate.Month)
            {
                continue;
            }

            var serviceYears = startDate.Year == year && month == startDate.Month
                ? 0
                : CompletedServiceYears(
                    startDate,
                    new DateOnly(year, month, 1).AddDays(-1));

            if (TierFor(serviceYears) == leaveType.EntitlementKind)
            {
                eligibleMonths++;
            }
        }

        if (eligibleMonths == 0)
        {
            return Ineligible(leaveType, "Çalışan bu kıdem kademesine uygun değil.");
        }

        var entitledDays = decimal.Round(
            leaveType.AnnualQuota * eligibleMonths / 12m,
            2,
            MidpointRounding.AwayFromZero);
        return Eligible(leaveType, entitledDays);
    }

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

public sealed record LeaveEntitlementGrant(int LeaveTypeId, decimal EntitledDays);

public sealed record LeaveEntitlementEvaluation(
    int LeaveTypeId,
    bool Eligible,
    decimal EntitledDays,
    bool MissingStartDate,
    string? Reason);
