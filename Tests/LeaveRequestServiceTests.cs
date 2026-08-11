using System.Security.Claims;
using IK.Web.Database;
using IK.Web.Models;
using IK.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace IK.Web.Tests;

public sealed class LeaveRequestServiceTests
{
    [Fact]
    public async Task TopLevelDepartmentManager_RequiresDelegateAndSkipsManagerApproval()
    {
        await using var dbContext = CreateDbContext();
        await SeedManagerApprovalScenarioAsync(dbContext);
        (await dbContext.Departments.FindAsync(1))!.ManagerEmployeeId = 10;
        dbContext.LeaveBalances.Add(new LeaveBalance
        {
            EmployeeId = 10,
            LeaveTypeId = 1,
            Year = DateTime.Today.Year,
            EntitledDays = 20,
            RemainingDays = 20
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var startDate = FutureDate(daysFromToday: 12);
        var missingDelegate = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateRequestAsync(
                employeeId: 10,
                category: LeaveRequestCategory.AnnualLeave,
                startDate: startDate,
                endDate: startDate,
                reason: "Yönetici izni",
                actorEmployeeId: 13));
        Assert.Contains("vekil seçimi zorunludur", missingDelegate.Message, StringComparison.OrdinalIgnoreCase);

        var request = await service.CreateRequestAsync(
            employeeId: 10,
            category: LeaveRequestCategory.AnnualLeave,
            startDate: startDate,
            endDate: startDate,
            reason: "Yönetici izni",
            delegateEmployeeId: 11,
            actorEmployeeId: 13);

        Assert.Equal(LeaveRequestStatus.HumanResourcesReview, request.CurrentStatus);
        var managerApproval = await dbContext.LeaveApprovals
            .SingleAsync(item => item.RequestId == request.RequestId
                                 && item.ApproverRole == LeaveApproverRole.Manager);
        Assert.Equal(LeaveApprovalDecision.Approved, managerApproval.Decision);
        Assert.Null(managerApproval.ApproverEmployeeId);
        var humanResourcesApproval = await dbContext.LeaveApprovals
            .SingleAsync(item => item.RequestId == request.RequestId
                                 && item.ApproverRole == LeaveApproverRole.HumanResources);
        Assert.Equal(LeaveApprovalDecision.Pending, humanResourcesApproval.Decision);
        Assert.Null(humanResourcesApproval.ApproverEmployeeId);
    }

    [Fact]
    public async Task ManagerDecisionAsync_Rejects_Manager_Who_Is_Not_Assigned_Manager()
    {
        await using var dbContext = CreateDbContext();
        await SeedManagerApprovalScenarioAsync(dbContext);

        var service = CreateService(dbContext);
        var startDate = FutureDate(daysFromToday: 20);
        var request = await service.CreateRequestAsync(
            employeeId: 11,
            category: LeaveRequestCategory.AnnualLeave,
            startDate: startDate,
            endDate: startDate.AddDays(2),
            reason: "Yillik izin",
            actorEmployeeId: 11);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ManagerDecisionAsync(
                requestId: request.RequestId,
                managerEmployeeId: 12,
                approve: true,
                comment: null,
                actorEmployeeId: 12));

        Assert.Equal("Bu onay adımına yalnızca atanmış yönetici karar verebilir.", exception.Message);
    }

    [Fact]
    public async Task ManagerDecisionAsync_Assigned_Manager_Approves_And_Creates_HumanResources_Step()
    {
        await using var dbContext = CreateDbContext();
        await SeedManagerApprovalScenarioAsync(dbContext);

        var service = CreateService(dbContext);
        var startDate = FutureDate(daysFromToday: 30);
        var request = await service.CreateRequestAsync(
            employeeId: 11,
            category: LeaveRequestCategory.AnnualLeave,
            startDate: startDate,
            endDate: startDate.AddDays(1),
            reason: "Yillik izin",
            actorEmployeeId: 11);

        await service.ManagerDecisionAsync(
            requestId: request.RequestId,
            managerEmployeeId: 10,
            approve: true,
            comment: null,
            actorEmployeeId: 10);

        var updatedRequest = await dbContext.LeaveRequests.SingleAsync(item => item.RequestId == request.RequestId);
        var approvals = await dbContext.LeaveApprovals
            .Where(item => item.RequestId == request.RequestId)
            .OrderBy(item => item.ApproverRole)
            .ToListAsync();

        Assert.Equal(LeaveRequestStatus.HumanResourcesReview, updatedRequest.CurrentStatus);
        Assert.Equal(2, approvals.Count);
        Assert.Collection(
            approvals,
            managerApproval =>
            {
                Assert.Equal(LeaveApproverRole.Manager, managerApproval.ApproverRole);
                Assert.Equal(10, managerApproval.ApproverEmployeeId);
                Assert.Equal(LeaveApprovalDecision.Approved, managerApproval.Decision);
            },
            humanResourcesApproval =>
            {
                Assert.Equal(LeaveApproverRole.HumanResources, humanResourcesApproval.ApproverRole);
                Assert.Null(humanResourcesApproval.ApproverEmployeeId);
                Assert.Equal(LeaveApprovalDecision.Pending, humanResourcesApproval.Decision);
            });
    }

    [Fact]
    public async Task CreateRequestAsync_ExplicitHalfDay_NormalizesDatesAndCalculatesHalfDay()
    {
        await using var dbContext = CreateDbContext();
        await SeedManagerApprovalScenarioAsync(dbContext);

        var service = CreateService(dbContext);
        var startDate = FutureDate(daysFromToday: 40).Date.AddHours(9);
        var endDate = startDate.AddHours(4);

        var request = await service.CreateRequestAsync(
            employeeId: 11,
            category: LeaveRequestCategory.AnnualLeave,
            startDate: startDate,
            endDate: endDate,
            reason: "Yarim gun izin",
            actorEmployeeId: 11,
            isHalfDay: true);

        Assert.Equal(startDate.Date, request.StartDate);
        Assert.Equal(endDate.Date, request.EndDate);
        Assert.Equal(0.5m, request.RequestedDays);
    }

    [Fact]
    public async Task CreateRequestAsync_ConfiguredPublicHoliday_IsNotCounted()
    {
        await using var dbContext = CreateDbContext();
        await SeedManagerApprovalScenarioAsync(dbContext);

        var startDate = FutureDate(daysFromToday: 45);
        var endDate = NextWorkingDay(startDate);
        dbContext.PublicHolidays.Add(new PublicHoliday
        {
            Date = DateOnly.FromDateTime(startDate),
            Name = "Test Tatili"
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var request = await service.CreateRequestAsync(
            employeeId: 11,
            category: LeaveRequestCategory.AnnualLeave,
            startDate: startDate,
            endDate: endDate,
            reason: "Resmi tatil testi",
            actorEmployeeId: 11);

        Assert.Equal(1m, request.RequestedDays);
    }

    [Fact]
    public async Task ManagerDecisionAsync_RemovedHolidayThatCreatesOverlap_RejectsApproval()
    {
        await using var dbContext = CreateDbContext();
        await SeedManagerApprovalScenarioAsync(dbContext);

        var monday = NextWeekday(DayOfWeek.Monday, minimumDaysFromToday: 60);
        var holiday = new PublicHoliday
        {
            Date = DateOnly.FromDateTime(monday),
            Name = "Değişen Takvim Tatili"
        };
        dbContext.PublicHolidays.Add(holiday);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var firstRequest = await service.CreateRequestAsync(
            employeeId: 11,
            category: LeaveRequestCategory.AnnualLeave,
            startDate: monday.AddDays(-3),
            endDate: monday,
            reason: "Cuma ve tatil",
            actorEmployeeId: 11);
        await service.CreateRequestAsync(
            employeeId: 11,
            category: LeaveRequestCategory.AnnualLeave,
            startDate: monday,
            endDate: monday.AddDays(1),
            reason: "Tatil ve salı",
            actorEmployeeId: 11);

        dbContext.PublicHolidays.Remove(holiday);
        await dbContext.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ManagerDecisionAsync(
                firstRequest.RequestId,
                managerEmployeeId: 10,
                approve: true,
                comment: null,
                actorEmployeeId: 10));

        Assert.Equal(
            "Çalışanın bu dönemle çakışan başka bir izin talebi zaten mevcut.",
            exception.Message);
    }

    [Fact]
    public async Task CreateRequestAsync_RejectsPastDates()
    {
        await using var dbContext = CreateDbContext();
        await SeedManagerApprovalScenarioAsync(dbContext);

        var service = CreateService(dbContext);
        var startDate = DateTime.Today.AddDays(-1).AddHours(9);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateRequestAsync(
                employeeId: 11,
                category: LeaveRequestCategory.AnnualLeave,
                startDate: startDate,
                endDate: startDate.AddHours(4),
                reason: "Gecmis izin",
                actorEmployeeId: 11));

        Assert.Equal("Geçmiş tarihli izin talebi oluşturulamaz.", exception.Message);
    }

    [Fact]
    public async Task UpdateRequestAsync_ExplicitHalfDay_NormalizesDatesAndRecalculatesHalfDay()
    {
        await using var dbContext = CreateDbContext();
        await SeedManagerApprovalScenarioAsync(dbContext);

        var service = CreateService(dbContext);
        var startDate = FutureDate(daysFromToday: 50).Date.AddHours(9);
        var request = await service.CreateRequestAsync(
            employeeId: 11,
            category: LeaveRequestCategory.AnnualLeave,
            startDate: startDate,
            endDate: startDate.AddHours(8),
            reason: "Yillik izin",
            actorEmployeeId: 11);

        var updatedStartDate = NextWorkingDay(startDate).Date.AddHours(13);
        var updatedEndDate = updatedStartDate.AddHours(4);

        var updatedRequest = await service.UpdateRequestAsync(
            request.RequestId,
            employeeId: 11,
            category: LeaveRequestCategory.AnnualLeave,
            startDate: updatedStartDate,
            endDate: updatedEndDate,
            reason: "Yarim gun izin",
            actorEmployeeId: 11,
            isHalfDay: true);

        Assert.Equal(updatedStartDate.Date, updatedRequest.StartDate);
        Assert.Equal(updatedEndDate.Date, updatedRequest.EndDate);
        Assert.Equal(0.5m, updatedRequest.RequestedDays);
    }

    [Fact]
    public async Task CreateRequestAsync_RangeContainingWeekend_CountsOnlyWeekdays()
    {
        await using var dbContext = CreateDbContext();
        await SeedManagerApprovalScenarioAsync(dbContext);

        var service = CreateService(dbContext);
        var friday = NextWeekday(DayOfWeek.Friday, 25);

        var request = await service.CreateRequestAsync(
            employeeId: 11,
            category: LeaveRequestCategory.AnnualLeave,
            startDate: friday,
            endDate: friday.AddDays(3),
            reason: "Hafta sonunu kapsayan izin",
            actorEmployeeId: 11);

        Assert.Equal(2m, request.RequestedDays);
    }

    [Fact]
    public async Task CreateRequestAsync_WeekendOnly_RejectsRequest()
    {
        await using var dbContext = CreateDbContext();
        await SeedManagerApprovalScenarioAsync(dbContext);

        var service = CreateService(dbContext);
        var saturday = NextWeekday(DayOfWeek.Saturday, 25);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateRequestAsync(
                employeeId: 11,
                category: LeaveRequestCategory.AnnualLeave,
                startDate: saturday,
                endDate: saturday.AddDays(1),
                reason: "Hafta sonu izin",
                actorEmployeeId: 11));

        Assert.Equal("Seçilen tarih aralığında iş günü bulunmuyor.", exception.Message);
    }

    [Fact]
    public async Task CreateRequestAsync_RequestsIntersectingOnlyOnWeekend_AreAllowed()
    {
        await using var dbContext = CreateDbContext();
        await SeedManagerApprovalScenarioAsync(dbContext);

        var service = CreateService(dbContext);
        var friday = NextWeekday(DayOfWeek.Friday, 35);
        var firstRequest = await service.CreateRequestAsync(
            employeeId: 11,
            category: LeaveRequestCategory.AnnualLeave,
            startDate: friday,
            endDate: friday.AddDays(2),
            reason: "Cuma izni",
            actorEmployeeId: 11);

        var secondRequest = await service.CreateRequestAsync(
            employeeId: 11,
            category: LeaveRequestCategory.AnnualLeave,
            startDate: friday.AddDays(1),
            endDate: friday.AddDays(3),
            reason: "Pazartesi izni",
            actorEmployeeId: 11);

        Assert.Equal(1m, firstRequest.RequestedDays);
        Assert.Equal(1m, secondRequest.RequestedDays);
    }

    [Fact]
    public async Task CreateRequestAsync_RequestsSharingAWeekday_AreRejected()
    {
        await using var dbContext = CreateDbContext();
        await SeedManagerApprovalScenarioAsync(dbContext);

        var service = CreateService(dbContext);
        var friday = NextWeekday(DayOfWeek.Friday, 35);
        await service.CreateRequestAsync(
            employeeId: 11,
            category: LeaveRequestCategory.AnnualLeave,
            startDate: friday,
            endDate: friday.AddDays(3),
            reason: "Cuma ve pazartesi izni",
            actorEmployeeId: 11);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateRequestAsync(
                employeeId: 11,
                category: LeaveRequestCategory.AnnualLeave,
                startDate: friday.AddDays(3),
                endDate: friday.AddDays(4),
                reason: "Pazartesi ve salı izni",
                actorEmployeeId: 11));

        Assert.Equal("Çalışanın bu dönemle çakışan başka bir izin talebi zaten mevcut.", exception.Message);
    }

    [Fact]
    public async Task HumanResourcesDecisionAsync_ApprovedHalfDay_DeductsHalfDayFromBalance()
    {
        await using var dbContext = CreateDbContext();
        await SeedManagerApprovalScenarioAsync(dbContext);

        var service = CreateService(dbContext);
        var weekday = FutureDate(daysFromToday: 45);
        var request = await service.CreateRequestAsync(
            employeeId: 11,
            category: LeaveRequestCategory.AnnualLeave,
            startDate: weekday,
            endDate: weekday,
            reason: "Yarım gün izin",
            actorEmployeeId: 11,
            isHalfDay: true);

        await service.ManagerDecisionAsync(
            request.RequestId,
            managerEmployeeId: 10,
            approve: true,
            comment: null,
            actorEmployeeId: 10);
        await service.HumanResourcesDecisionAsync(
            request.RequestId,
            humanResourcesEmployeeId: 12,
            approve: true,
            comment: null,
            actorEmployeeId: 12);

        var balance = await dbContext.LeaveBalances.SingleAsync(item =>
            item.EmployeeId == 11 &&
            item.LeaveTypeId == 1 &&
            item.Year == weekday.Year);
        Assert.Equal(0.5m, balance.UsedDays);
        Assert.Equal(19.5m, balance.RemainingDays);
    }

    [Fact]
    public async Task HumanResourcesDecisionAsync_AfterManualUsedDayCorrection_AddsApprovedDays()
    {
        await using var dbContext = CreateDbContext();
        await SeedManagerApprovalScenarioAsync(dbContext);
        var weekday = FutureDate(daysFromToday: 45);
        var balance = await dbContext.LeaveBalances.SingleAsync(item =>
            item.EmployeeId == 11
            && item.LeaveTypeId == 1
            && item.Year == weekday.Year);
        balance.UsedDays = 1m;
        balance.RecalculateRemainingDays();
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var request = await service.CreateRequestAsync(
            employeeId: 11,
            category: LeaveRequestCategory.AnnualLeave,
            startDate: weekday,
            endDate: weekday,
            reason: "Düzeltme sonrası yarım gün",
            actorEmployeeId: 11,
            isHalfDay: true);
        await service.ManagerDecisionAsync(
            request.RequestId,
            managerEmployeeId: 10,
            approve: true,
            comment: null,
            actorEmployeeId: 10);
        await service.HumanResourcesDecisionAsync(
            request.RequestId,
            humanResourcesEmployeeId: 12,
            approve: true,
            comment: null,
            actorEmployeeId: 12);

        Assert.Equal(1.5m, balance.UsedDays);
        Assert.Equal(18.5m, balance.RemainingDays);
        Assert.Equal(
            0.5m,
            await dbContext.LeaveRequestBalanceAllocations.SumAsync(item => item.Days));
    }

    [Fact]
    public async Task HumanResourcesDecisionAsync_UsedDayReductionReleasesCorrectedCapacity()
    {
        await using var dbContext = CreateDbContext();
        await SeedManagerApprovalScenarioAsync(dbContext);
        var startDate = FutureDate(daysFromToday: 70);
        var balance = await dbContext.LeaveBalances.SingleAsync(item =>
            item.EmployeeId == 11
            && item.LeaveTypeId == 1
            && item.Year == startDate.Year);
        var historicalDate = startDate.AddDays(-3);
        while (historicalDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            historicalDate = historicalDate.AddDays(-1);
        }
        var historicalRequest = new LeaveRequest
        {
            RequestId = 800,
            EmployeeId = 11,
            Category = LeaveRequestCategory.AnnualLeave,
            StartDate = historicalDate,
            EndDate = historicalDate,
            RequestedDays = 1m,
            Reason = "Düzeltme öncesi izin",
            CurrentStatus = LeaveRequestStatus.Approved
        };
        dbContext.LeaveRequests.Add(historicalRequest);
        dbContext.LeaveRequestBalanceAllocations.Add(new LeaveRequestBalanceAllocation
        {
            Request = historicalRequest,
            Balance = balance,
            Source = LeaveBalanceAllocationSource.Entitlement,
            Days = 1m
        });
        balance.UsedDays = 1m;
        balance.RecalculateRemainingDays();
        (await dbContext.Employees.SingleAsync(item => item.EmployeeId == 11)).StartDate =
            DateTime.Today.AddYears(-2);
        await dbContext.SaveChangesAsync();

        var balanceService = CreateBalanceService(dbContext);
        await balanceService.UpdateAsync(
            BalanceManagerPrincipal(),
            balance.BalanceId,
            balance.EmployeeId,
            balance.LeaveTypeId,
            balance.Year,
            balance.EntitledDays,
            balance.CarryOverDays,
            usedDays: 0m,
            rowVersion: [],
            actorEmployeeId: 1);

        var endDate = startDate;
        var workingDays = 1;
        while (workingDays < 20)
        {
            endDate = endDate.AddDays(1);
            if (endDate.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
            {
                workingDays++;
            }
        }

        var correctedCapacityRequest = new LeaveRequest
        {
            RequestId = 801,
            EmployeeId = 11,
            Category = LeaveRequestCategory.AnnualLeave,
            StartDate = startDate,
            EndDate = endDate,
            RequestedDays = 20m,
            Reason = "Düzeltilmiş kapasitenin tamamı",
            CurrentStatus = LeaveRequestStatus.HumanResourcesReview
        };
        dbContext.LeaveRequests.Add(correctedCapacityRequest);
        await dbContext.SaveChangesAsync();

        await CreateService(dbContext).HumanResourcesDecisionAsync(
            correctedCapacityRequest.RequestId,
            humanResourcesEmployeeId: 12,
            approve: true,
            comment: null,
            actorEmployeeId: 12);

        Assert.Equal(20m, balance.UsedDays);
        Assert.Equal(0m, balance.RemainingDays);
        Assert.Equal(
            21m,
            await dbContext.LeaveRequestBalanceAllocations.SumAsync(item => item.Days));

        await balanceService.UpdateAsync(
            BalanceManagerPrincipal(),
            balance.BalanceId,
            balance.EmployeeId,
            balance.LeaveTypeId,
            balance.Year,
            balance.EntitledDays,
            balance.CarryOverDays,
            balance.UsedDays,
            rowVersion: [],
            actorEmployeeId: 1);

        Assert.Equal(
            1,
            await dbContext.AuditLogs.CountAsync(item =>
                item.ActionType == AuditActionType.LeaveBalanceUpdated));
    }

    [Fact]
    public async Task HumanResourcesDecisionAsync_RangeContainingWeekend_DeductsOnlyWeekdays()
    {
        await using var dbContext = CreateDbContext();
        await SeedManagerApprovalScenarioAsync(dbContext);

        var service = CreateService(dbContext);
        var friday = NextWeekday(DayOfWeek.Friday, 55);
        var request = await service.CreateRequestAsync(
            employeeId: 11,
            category: LeaveRequestCategory.AnnualLeave,
            startDate: friday,
            endDate: friday.AddDays(3),
            reason: "Hafta sonunu kapsayan izin",
            actorEmployeeId: 11);

        await service.ManagerDecisionAsync(
            request.RequestId,
            managerEmployeeId: 10,
            approve: true,
            comment: null,
            actorEmployeeId: 10);
        await service.HumanResourcesDecisionAsync(
            request.RequestId,
            humanResourcesEmployeeId: 12,
            approve: true,
            comment: null,
            actorEmployeeId: 12);

        var balance = await dbContext.LeaveBalances.SingleAsync(item =>
            item.EmployeeId == 11 &&
            item.LeaveTypeId == 1 &&
            item.Year == friday.Year);
        Assert.Equal(2m, balance.UsedDays);
        Assert.Equal(18m, balance.RemainingDays);
    }

    [Fact]
    public async Task HumanResourcesDecisionAsync_AnnualLeave_DeductsCarryOverBeforeTierEntitlements()
    {
        await using var dbContext = CreateDbContext();
        await SeedManagerApprovalScenarioAsync(dbContext);

        var monday = NextWeekday(DayOfWeek.Monday, 55);
        var firstTierBalance = await dbContext.LeaveBalances.SingleAsync(item =>
            item.EmployeeId == 11
            && item.LeaveTypeId == 1
            && item.Year == monday.Year);
        firstTierBalance.EntitledDays = 10m;
        firstTierBalance.CarryOverDays = 3m;
        firstTierBalance.UsedDays = 0m;
        firstTierBalance.RecalculateRemainingDays();

        dbContext.LeaveTypes.Add(new LeaveType
        {
            LeaveTypeId = 2,
            Name = "10-20 Yıllık Çalışan İzni",
            AnnualQuota = 30m,
            MaxAccrualDays = 50m,
            EntitlementKind = LeaveEntitlementKind.ServiceYears10To20
        });
        dbContext.LeaveBalances.Add(new LeaveBalance
        {
            BalanceId = 5,
            EmployeeId = 11,
            LeaveTypeId = 2,
            Year = monday.Year,
            EntitledDays = 10m,
            CarryOverDays = 2m,
            UsedDays = 0m,
            RemainingDays = 12m
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var request = await service.CreateRequestAsync(
            employeeId: 11,
            category: LeaveRequestCategory.AnnualLeave,
            startDate: monday,
            endDate: monday.AddDays(7),
            reason: "Kademeli yıllık izin",
            actorEmployeeId: 11);
        Assert.Equal(6m, request.RequestedDays);

        await service.ManagerDecisionAsync(
            request.RequestId,
            managerEmployeeId: 10,
            approve: true,
            comment: null,
            actorEmployeeId: 10);
        await service.HumanResourcesDecisionAsync(
            request.RequestId,
            humanResourcesEmployeeId: 12,
            approve: true,
            comment: null,
            actorEmployeeId: 12);

        var allocations = await dbContext.LeaveRequestBalanceAllocations
            .Where(item => item.RequestId == request.RequestId)
            .OrderBy(item => item.Source)
            .ThenBy(item => item.BalanceId)
            .ToListAsync();
        Assert.Collection(
            allocations,
            item =>
            {
                Assert.Equal(LeaveBalanceAllocationSource.CarryOver, item.Source);
                Assert.Equal(firstTierBalance.BalanceId, item.BalanceId);
                Assert.Equal(3m, item.Days);
            },
            item =>
            {
                Assert.Equal(LeaveBalanceAllocationSource.CarryOver, item.Source);
                Assert.Equal(5, item.BalanceId);
                Assert.Equal(2m, item.Days);
            },
            item =>
            {
                Assert.Equal(LeaveBalanceAllocationSource.Entitlement, item.Source);
                Assert.Equal(firstTierBalance.BalanceId, item.BalanceId);
                Assert.Equal(1m, item.Days);
            });
        Assert.Equal(4m, firstTierBalance.UsedDays);
        Assert.Equal(2m, await dbContext.LeaveBalances
            .Where(item => item.BalanceId == 5)
            .Select(item => item.UsedDays)
            .SingleAsync());
    }

    [Fact]
    public async Task HumanResourcesDecisionAsync_LegacyPendingCalendarDayAmount_RecalculatesBeforeDeduction()
    {
        await using var dbContext = CreateDbContext();
        await SeedManagerApprovalScenarioAsync(dbContext);

        var service = CreateService(dbContext);
        var friday = NextWeekday(DayOfWeek.Friday, 65);
        var request = await service.CreateRequestAsync(
            employeeId: 11,
            category: LeaveRequestCategory.AnnualLeave,
            startDate: friday,
            endDate: friday.AddDays(3),
            reason: "Eski hesapla bekleyen izin",
            actorEmployeeId: 11);

        await service.ManagerDecisionAsync(
            request.RequestId,
            managerEmployeeId: 10,
            approve: true,
            comment: null,
            actorEmployeeId: 10);
        request.RequestedDays = 4m;
        await dbContext.SaveChangesAsync();

        await service.HumanResourcesDecisionAsync(
            request.RequestId,
            humanResourcesEmployeeId: 12,
            approve: true,
            comment: null,
            actorEmployeeId: 12);

        var balance = await dbContext.LeaveBalances.SingleAsync(item =>
            item.EmployeeId == 11 &&
            item.LeaveTypeId == 1 &&
            item.Year == friday.Year);
        Assert.Equal(2m, request.RequestedDays);
        Assert.Equal(2m, balance.UsedDays);
        Assert.Equal(18m, balance.RemainingDays);
    }

    [Fact]
    public async Task UpdateRequestAsync_RefreshesManagerApproverWhenEmployeeChanges()
    {
        await using var dbContext = CreateDbContext();
        await SeedManagerApprovalScenarioAsync(dbContext);

        var service = CreateService(dbContext);
        var startDate = FutureDate(daysFromToday: 60);
        var request = await service.CreateRequestAsync(
            employeeId: 11,
            category: LeaveRequestCategory.AnnualLeave,
            startDate: startDate,
            endDate: startDate.AddDays(1),
            reason: "Yillik izin",
            actorEmployeeId: 11);

        var updatedRequest = await service.UpdateRequestAsync(
            request.RequestId,
            employeeId: 13,
            category: LeaveRequestCategory.AnnualLeave,
            startDate: startDate.AddDays(3),
            endDate: startDate.AddDays(4),
            reason: "Calisan degisti",
            actorEmployeeId: 1);

        Assert.Equal(13, updatedRequest.EmployeeId);
        Assert.Equal(12, updatedRequest.ManagerApproverEmployeeId);

        var managerApproval = await dbContext.LeaveApprovals.SingleAsync(item =>
            item.RequestId == request.RequestId &&
            item.ApproverRole == LeaveApproverRole.Manager);
        Assert.Equal(12, managerApproval.ApproverEmployeeId);
        Assert.Equal(LeaveApprovalDecision.Pending, managerApproval.Decision);
    }

    [Fact]
    public async Task UpdateRequestAsync_RejectsApprovedRequests()
    {
        await using var dbContext = CreateDbContext();
        await SeedManagerApprovalScenarioAsync(dbContext);

        var service = CreateService(dbContext);
        var startDate = NextWorkingDay(FutureDate(daysFromToday: 70));
        var request = await service.CreateRequestAsync(
            employeeId: 11,
            category: LeaveRequestCategory.AnnualLeave,
            startDate: startDate,
            endDate: startDate,
            reason: "Yillik izin",
            actorEmployeeId: 11);

        request.CurrentStatus = LeaveRequestStatus.Approved;
        await dbContext.SaveChangesAsync();

        var updateDate = NextWorkingDay(startDate.AddDays(3));
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateRequestAsync(
                request.RequestId,
                employeeId: 11,
                category: LeaveRequestCategory.AnnualLeave,
                startDate: updateDate,
                endDate: updateDate,
                reason: "Onayli izin degisikligi",
                actorEmployeeId: 1));

        Assert.Equal("Yalnızca yönetici onayı bekleyen izin talepleri düzenlenebilir.", exception.Message);
    }

    [Fact]
    public async Task UpdateRequestAsync_RejectsInsufficientBalance()
    {
        await using var dbContext = CreateDbContext();
        await SeedManagerApprovalScenarioAsync(dbContext);

        var service = CreateService(dbContext);
        var startDate = FutureDate(daysFromToday: 80);
        var request = await service.CreateRequestAsync(
            employeeId: 11,
            category: LeaveRequestCategory.AnnualLeave,
            startDate: startDate,
            endDate: startDate.AddHours(8),
            reason: "Yillik izin",
            actorEmployeeId: 11);

        var balance = await dbContext.LeaveBalances.SingleAsync(item =>
            item.EmployeeId == 11 &&
            item.LeaveTypeId == 1 &&
            item.Year == startDate.Year);
        balance.RemainingDays = 0;
        await dbContext.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateRequestAsync(
                request.RequestId,
                employeeId: 11,
                category: LeaveRequestCategory.AnnualLeave,
                startDate: NextWorkingDay(startDate),
                endDate: NextWorkingDay(startDate).AddHours(8),
                reason: "Bakiye yetersiz",
                actorEmployeeId: 1));

        Assert.Equal("Talep edilen dönem için izin bakiyesi yeterli değil.", exception.Message);
    }

    [Fact]
    public async Task CreateRequestAsync_AllowsBalanceBackedManualLeaveType()
    {
        await using var dbContext = CreateDbContext();
        await SeedManagerApprovalScenarioAsync(dbContext);
        var startDate = FutureDate(daysFromToday: 70);
        dbContext.LeaveTypes.Add(new LeaveType
        {
            LeaveTypeId = 7,
            Name = "Manuel Mazeret İzni",
            AnnualQuota = 3m,
            CarryOverRule = false,
            MaxAccrualDays = 3m,
            EntitlementKind = LeaveEntitlementKind.Manual
        });
        dbContext.LeaveBalances.Add(new LeaveBalance
        {
            EmployeeId = 11,
            LeaveTypeId = 7,
            Year = startDate.Year,
            EntitledDays = 3m,
            RemainingDays = 3m
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var request = await service.CreateRequestAsync(
            employeeId: 11,
            category: LeaveRequestCategory.SpecificLeaveType,
            startDate: startDate,
            endDate: startDate,
            reason: "Manuel tür talebi",
            actorEmployeeId: 11,
            leaveTypeId: 7);

        Assert.Equal(LeaveRequestCategory.SpecificLeaveType, request.Category);
        Assert.Equal(7, request.LeaveTypeId);
        await service.ManagerDecisionAsync(
            request.RequestId,
            managerEmployeeId: 10,
            approve: true,
            comment: null,
            actorEmployeeId: 10);
        await service.HumanResourcesDecisionAsync(
            request.RequestId,
            humanResourcesEmployeeId: 12,
            approve: true,
            comment: null,
            actorEmployeeId: 12);
        var balance = await dbContext.LeaveBalances.SingleAsync(item =>
            item.EmployeeId == 11 && item.LeaveTypeId == 7 && item.Year == startDate.Year);
        Assert.Equal(1m, balance.UsedDays);
        Assert.Equal(2m, balance.RemainingDays);
    }

    [Fact]
    public async Task CreateRequestAsync_RejectsSpecificTypeWithoutYearBalance()
    {
        await using var dbContext = CreateDbContext();
        await SeedManagerApprovalScenarioAsync(dbContext);
        dbContext.LeaveTypes.Add(new LeaveType
        {
            LeaveTypeId = 7,
            Name = "Manuel Mazeret İzni",
            AnnualQuota = 3m,
            CarryOverRule = false,
            MaxAccrualDays = 3m,
            EntitlementKind = LeaveEntitlementKind.Manual
        });
        await dbContext.SaveChangesAsync();
        var startDate = FutureDate(daysFromToday: 72);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(dbContext).CreateRequestAsync(
                employeeId: 11,
                category: LeaveRequestCategory.SpecificLeaveType,
                startDate: startDate,
                endDate: startDate,
                reason: "Bakiyesiz tür",
                actorEmployeeId: 11,
                leaveTypeId: 7));

        Assert.Equal("Talep yılı için seçilen izin bakiyesi bulunamadı.", exception.Message);
    }

    [Fact]
    public async Task CreateRequestAsync_RejectsMobilizationForNonMaleEmployeeEvenWithBalance()
    {
        await using var dbContext = CreateDbContext();
        await SeedManagerApprovalScenarioAsync(dbContext);
        (await dbContext.Employees.FindAsync(11))!.Gender = EmployeeGender.Female;
        var startDate = FutureDate(daysFromToday: 74);
        dbContext.LeaveTypes.Add(new LeaveType
        {
            LeaveTypeId = 6,
            Name = "Seferberlik İzni",
            AnnualQuota = 2m,
            CarryOverRule = false,
            MaxAccrualDays = 2m,
            EntitlementKind = LeaveEntitlementKind.MaleEmployees
        });
        dbContext.LeaveBalances.Add(new LeaveBalance
        {
            EmployeeId = 11,
            LeaveTypeId = 6,
            Year = startDate.Year,
            EntitledDays = 2m,
            RemainingDays = 2m
        });
        await dbContext.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(dbContext).CreateRequestAsync(
                employeeId: 11,
                category: LeaveRequestCategory.SpecificLeaveType,
                startDate: startDate,
                endDate: startDate,
                reason: "Uygunsuz seferberlik",
                actorEmployeeId: 11,
                leaveTypeId: 6));

        Assert.Equal("Çalışan seçilen izin türü için uygun değil.", exception.Message);
    }

    [Fact]
    public async Task RetrospectiveRequest_RequiresExplicitFlagAndUsesNormalApprovalWorkflow()
    {
        await using var dbContext = CreateDbContext();
        await SeedManagerApprovalScenarioAsync(dbContext);
        var date = DateTime.Today.AddDays(-7);
        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            date = date.AddDays(-1);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(dbContext).CreateRequestAsync(
                11,
                LeaveRequestCategory.AnnualLeave,
                date,
                date,
                "Acil devamsızlık",
                11));

        var request = await CreateService(dbContext).CreateRequestAsync(
            11,
            LeaveRequestCategory.AnnualLeave,
            date,
            date,
            "Acil devamsızlık",
            11,
            isRetrospective: true);

        Assert.True(request.IsRetrospective);
        Assert.Equal(LeaveRequestStatus.ManagerReview, request.CurrentStatus);
        Assert.Equal(1m, request.RequestedDays);
    }

    [Fact]
    public async Task PendingRequest_CanBeCancelledDirectlyByOwnerWithoutBalanceMutation()
    {
        await using var dbContext = CreateDbContext();
        await SeedManagerApprovalScenarioAsync(dbContext);
        var date = FutureDate(20);
        var request = await CreateService(dbContext).CreateRequestAsync(
            11,
            LeaveRequestCategory.AnnualLeave,
            date,
            date,
            "Plan değişikliği",
            11);

        await CreateCancellationService(dbContext).CancelPendingAsync(
            EmployeePrincipal(11),
            request.RequestId,
            11);

        Assert.Equal(LeaveRequestStatus.Cancelled, request.CurrentStatus);
        Assert.Equal(0m, (await dbContext.LeaveBalances.FindAsync(1))!.UsedDays);
        Assert.Empty(await dbContext.LeaveApprovals.Where(item => item.RequestId == request.RequestId).ToListAsync());
        var directCancellation = await dbContext.LeaveCancellationRequests.SingleAsync();
        Assert.True(directCancellation.IsDirectCancellation);
        Assert.Equal(LeaveRequestStatus.Approved, directCancellation.CurrentStatus);
        Assert.Equal(11, directCancellation.RequestedByEmployeeId);
        Assert.Equal("employee-11", directCancellation.RequestedByDisplayName);
    }

    [Fact]
    public async Task PendingRequest_DirectCancellationPersistsElevatedEditorAsDecisionActor()
    {
        await using var dbContext = CreateDbContext();
        await SeedManagerApprovalScenarioAsync(dbContext);
        var date = FutureDate(22);
        var request = await CreateService(dbContext).CreateRequestAsync(
            11,
            LeaveRequestCategory.AnnualLeave,
            date,
            date,
            "Plan değişikliği",
            11);

        await CreateCancellationService(dbContext).CancelPendingAsync(
            LeaveRequestEditorPrincipal(12),
            request.RequestId,
            12);

        var directCancellation = await dbContext.LeaveCancellationRequests.SingleAsync();
        Assert.Equal(12, directCancellation.RequestedByEmployeeId);
        Assert.Equal("editor-12", directCancellation.RequestedByDisplayName);
        Assert.Equal("Talep, yönetici kararı verilmeden doğrudan iptal edildi.", directCancellation.Reason);
    }

    [Fact]
    public async Task ManagerApprovedRequest_MustFinishHumanResourcesReviewBeforeCancellationRequest()
    {
        await using var dbContext = CreateDbContext();
        await SeedManagerApprovalScenarioAsync(dbContext);
        var date = FutureDate(25);
        var requestService = CreateService(dbContext);
        var request = await requestService.CreateRequestAsync(
            11,
            LeaveRequestCategory.AnnualLeave,
            date,
            date,
            "Planlanan izin",
            11);
        await requestService.ManagerDecisionAsync(request.RequestId, 10, true, null, 10);

        var cancellationService = CreateCancellationService(dbContext);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            cancellationService.CancelPendingAsync(
                EmployeePrincipal(11), request.RequestId, 11));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            cancellationService.CreateAsync(
                EmployeePrincipal(11), request.RequestId, date, date, "Plan değişti", 11));

        await requestService.HumanResourcesDecisionAsync(request.RequestId, 12, true, null, 12);
        var cancellation = await cancellationService.CreateAsync(
            EmployeePrincipal(11), request.RequestId, date, date, "Plan değişti", 11);

        Assert.Equal(LeaveRequestStatus.ManagerReview, cancellation.CurrentStatus);
        Assert.Equal(1m, cancellation.RequestedRefundDays);
    }

    [Fact]
    public async Task ApprovedLeave_PartialCancellationRefundsOnlyUnusedWorkingDaysAfterManagerAndHrApproval()
    {
        await using var dbContext = CreateDbContext();
        await SeedManagerApprovalScenarioAsync(dbContext);
        var start = NextWeekday(DayOfWeek.Monday, minimumDaysFromToday: 30);
        var end = start.AddDays(4);
        var requestService = CreateService(dbContext);
        var request = await requestService.CreateRequestAsync(
            11,
            LeaveRequestCategory.AnnualLeave,
            start,
            end,
            "Yıllık izin",
            11);
        await requestService.ManagerDecisionAsync(request.RequestId, 10, true, null, 10);
        await requestService.HumanResourcesDecisionAsync(request.RequestId, 12, true, null, 12);
        Assert.Equal(5m, (await dbContext.LeaveBalances.FindAsync(1))!.UsedDays);

        var cancellationService = CreateCancellationService(dbContext);
        var cancellation = await cancellationService.CreateAsync(
            EmployeePrincipal(11),
            request.RequestId,
            start.AddDays(3),
            end,
            "İşe erken dönüş",
            11);
        Assert.Equal(2m, cancellation.RequestedRefundDays);

        await cancellationService.ManagerDecisionAsync(
            cancellation.CancellationRequestId, 10, true, null, 10);
        await cancellationService.HumanResourcesDecisionAsync(
            cancellation.CancellationRequestId, 12, true, null, 12);

        var balance = await dbContext.LeaveBalances.FindAsync(1);
        Assert.Equal(3m, balance!.UsedDays);
        Assert.Equal(17m, balance.RemainingDays);
        Assert.Equal(3m, request.RequestedDays);
        Assert.Equal(end.Date, request.EndDate);
        Assert.Equal(LeaveRequestStatus.Approved, request.CurrentStatus);
        Assert.Equal(2m, await dbContext.LeaveCancellationBalanceRefunds.SumAsync(item => item.Days));
    }

    [Fact]
    public async Task ApprovedLeave_SingleMiddleDayCancellationPreservesLeaveBeforeAndAfterTheDay()
    {
        await using var dbContext = CreateDbContext();
        await SeedManagerApprovalScenarioAsync(dbContext);
        var start = NextWeekday(DayOfWeek.Monday, minimumDaysFromToday: 36);
        var end = start.AddDays(4);
        var requestService = CreateService(dbContext);
        var request = await requestService.CreateRequestAsync(
            11,
            LeaveRequestCategory.AnnualLeave,
            start,
            end,
            "Yıllık izin",
            11);
        await requestService.ManagerDecisionAsync(request.RequestId, 10, true, null, 10);
        await requestService.HumanResourcesDecisionAsync(request.RequestId, 12, true, null, 12);

        var cancellationService = CreateCancellationService(dbContext);
        var cancellationDate = start.AddDays(2);
        var cancellation = await cancellationService.CreateAsync(
            EmployeePrincipal(11),
            request.RequestId,
            cancellationDate,
            cancellationDate,
            "Yalnız çarşamba işe dönüş",
            11);
        Assert.Equal(cancellationDate.Date, cancellation.CancellationStartDate);
        Assert.Equal(cancellationDate.Date, cancellation.CancellationEndDate);
        Assert.Equal(1m, cancellation.RequestedRefundDays);

        await cancellationService.ManagerDecisionAsync(
            cancellation.CancellationRequestId, 10, true, null, 10);
        await cancellationService.HumanResourcesDecisionAsync(
            cancellation.CancellationRequestId, 12, true, null, 12);

        Assert.Equal(4m, request.RequestedDays);
        Assert.Equal(start.Date, request.StartDate);
        Assert.Equal(end.Date, request.EndDate);
        Assert.Equal(4m, (await dbContext.LeaveBalances.FindAsync(1))!.UsedDays);

        var replacementRequest = await requestService.CreateRequestAsync(
            11,
            LeaveRequestCategory.AnnualLeave,
            cancellationDate,
            cancellationDate,
            "İptal edilen gün için yeni plan",
            11);
        Assert.Equal(1m, replacementRequest.RequestedDays);
    }

    [Fact]
    public async Task ApprovedLeave_CancellationNeverRefundsMoreThanApprovedAllocationWhenHolidayIsRemoved()
    {
        await using var dbContext = CreateDbContext();
        await SeedManagerApprovalScenarioAsync(dbContext);
        var start = NextWeekday(DayOfWeek.Monday, minimumDaysFromToday: 40);
        var holidayDate = DateOnly.FromDateTime(start.AddDays(2));
        var holiday = new PublicHoliday { Date = holidayDate, Name = "Geçici tatil" };
        dbContext.PublicHolidays.Add(holiday);
        await dbContext.SaveChangesAsync();

        var requestService = CreateService(dbContext);
        var request = await requestService.CreateRequestAsync(
            11,
            LeaveRequestCategory.AnnualLeave,
            start,
            start.AddDays(4),
            "Tatil değişimi senaryosu",
            11);
        await requestService.ManagerDecisionAsync(request.RequestId, 10, true, null, 10);
        await requestService.HumanResourcesDecisionAsync(request.RequestId, 12, true, null, 12);
        Assert.Equal(4m, request.RequestedDays);

        dbContext.PublicHolidays.Remove(holiday);
        await dbContext.SaveChangesAsync();

        var cancellationService = CreateCancellationService(dbContext);
        var cancellation = await cancellationService.CreateAsync(
            EmployeePrincipal(11),
            request.RequestId,
            start,
            start.AddDays(4),
            "İşe erken dönüş",
            11);
        Assert.Equal(4m, cancellation.RequestedRefundDays);

        await cancellationService.ManagerDecisionAsync(
            cancellation.CancellationRequestId, 10, true, null, 10);
        await cancellationService.HumanResourcesDecisionAsync(
            cancellation.CancellationRequestId, 12, true, null, 12);

        Assert.Equal(0m, (await dbContext.LeaveBalances.FindAsync(1))!.UsedDays);
        Assert.Equal(LeaveRequestStatus.Cancelled, request.CurrentStatus);
        Assert.Equal(start.AddDays(4).Date, request.EndDate);
    }

    [Fact]
    public async Task ApprovedLeave_CancellationRefundUsesApprovalSnapshotWhenHolidayIsAddedLater()
    {
        await using var dbContext = CreateDbContext();
        await SeedManagerApprovalScenarioAsync(dbContext);
        var start = NextWeekday(DayOfWeek.Monday, minimumDaysFromToday: 45);
        var end = start.AddDays(4);
        var requestService = CreateService(dbContext);
        var request = await requestService.CreateRequestAsync(
            11,
            LeaveRequestCategory.AnnualLeave,
            start,
            end,
            "Tatil eklenmesi senaryosu",
            11);
        await requestService.ManagerDecisionAsync(request.RequestId, 10, true, null, 10);
        await requestService.HumanResourcesDecisionAsync(request.RequestId, 12, true, null, 12);
        Assert.Equal(5m, request.RequestedDays);
        Assert.Equal(5m, await dbContext.LeaveRequestApprovedDays
            .Where(item => item.RequestId == request.RequestId)
            .SumAsync(item => item.Days));

        dbContext.PublicHolidays.Add(new PublicHoliday
        {
            Date = DateOnly.FromDateTime(start.AddDays(1)),
            Name = "Sonradan ilan edilen tatil"
        });
        await dbContext.SaveChangesAsync();

        var cancellationService = CreateCancellationService(dbContext);
        var cancellation = await cancellationService.CreateAsync(
            EmployeePrincipal(11),
            request.RequestId,
            start,
            end,
            "İzin iptali",
            11);
        Assert.Equal(5m, cancellation.RequestedRefundDays);

        await cancellationService.ManagerDecisionAsync(
            cancellation.CancellationRequestId, 10, true, null, 10);
        await cancellationService.HumanResourcesDecisionAsync(
            cancellation.CancellationRequestId, 12, true, null, 12);

        Assert.Equal(0m, (await dbContext.LeaveBalances.FindAsync(1))!.UsedDays);
        Assert.Equal(LeaveRequestStatus.Cancelled, request.CurrentStatus);
        Assert.Empty(await dbContext.LeaveRequestApprovedDays
            .Where(item => item.RequestId == request.RequestId)
            .ToListAsync());
    }

    [Fact]
    public async Task ApprovedLeave_FullCancellationReturnsAllDaysAndMarksOriginalCancelled()
    {
        await using var dbContext = CreateDbContext();
        await SeedManagerApprovalScenarioAsync(dbContext);
        var date = FutureDate(35);
        var requestService = CreateService(dbContext);
        var request = await requestService.CreateRequestAsync(
            11,
            LeaveRequestCategory.AnnualLeave,
            date,
            date,
            "Bir günlük izin",
            11);
        await requestService.ManagerDecisionAsync(request.RequestId, 10, true, null, 10);
        await requestService.HumanResourcesDecisionAsync(request.RequestId, 12, true, null, 12);

        var cancellationService = CreateCancellationService(dbContext);
        var cancellation = await cancellationService.CreateAsync(
            EmployeePrincipal(11), request.RequestId, date, date, "İzin gereksinimi kalmadı", 11);
        await cancellationService.ManagerDecisionAsync(
            cancellation.CancellationRequestId, 10, true, null, 10);
        await cancellationService.HumanResourcesDecisionAsync(
            cancellation.CancellationRequestId, 12, true, null, 12);

        var balance = await dbContext.LeaveBalances.FindAsync(1);
        Assert.Equal(0m, balance!.UsedDays);
        Assert.Equal(20m, balance.RemainingDays);
        Assert.Equal(LeaveRequestStatus.Cancelled, request.CurrentStatus);
        Assert.Equal(1m, request.RequestedDays);
        Assert.Equal(1m, await dbContext.LeaveCancellationBalanceRefunds.SumAsync(item => item.Days));
    }

    private static LeaveRequestService CreateService(HumanResourcesDbContext dbContext)
    {
        var auditLogService = new AuditLogService(dbContext);
        return new LeaveRequestService(
            dbContext,
            new LeaveDayCalculator(),
            new LeaveEntitlementService(),
            new PublicHolidayCalendar(TestHumanResourcesDbContextFactory.From(dbContext)),
            auditLogService,
            new ManagerDelegationService(
                dbContext,
                auditLogService,
                new EmployeeResponsibilityRoleService(dbContext, auditLogService),
                TimeProvider.System),
            TimeProvider.System,
            NullLogger<LeaveRequestService>.Instance);
    }

    private static LeaveCancellationService CreateCancellationService(
        HumanResourcesDbContext dbContext)
    {
        var audit = new AuditLogService(dbContext);
        var delegation = new ManagerDelegationService(
            dbContext,
            audit,
            new EmployeeResponsibilityRoleService(dbContext, audit),
            TimeProvider.System);
        return new LeaveCancellationService(
            dbContext,
            new PageAccessService(TestHumanResourcesDbContextFactory.From(dbContext)),
            audit,
            delegation,
            TimeProvider.System,
            NullLogger<LeaveCancellationService>.Instance);
    }

    private static ClaimsPrincipal EmployeePrincipal(int employeeId) =>
        new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, $"employee-{employeeId}"),
                new Claim(UserClaimTypes.EmployeeId, employeeId.ToString())
            ],
            "Test"));

    private static ClaimsPrincipal LeaveRequestEditorPrincipal(int employeeId) =>
        new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, $"editor-{employeeId}"),
                new Claim(UserClaimTypes.EmployeeId, employeeId.ToString()),
                new Claim(
                    PermissionClaimTypes.Permission,
                    PermissionNames.CanEditDeleteLeaveRequests)
            ],
            "Test"));

    private static LeaveBalanceService CreateBalanceService(
        HumanResourcesDbContext dbContext) =>
        new(
            dbContext,
            new AuditLogService(dbContext),
            new LeaveEntitlementService(),
            new PageAccessService(TestHumanResourcesDbContextFactory.From(dbContext)));

    private static ClaimsPrincipal BalanceManagerPrincipal() =>
        new(
            new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.Name, "admin"),
                    new Claim(
                        PermissionClaimTypes.Permission,
                        PermissionNames.CanManageLeaveBalances)
                ],
                "Test"));

    private static HumanResourcesDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HumanResourcesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new HumanResourcesDbContext(options);
    }

    private static DateTime FutureDate(int daysFromToday)
    {
        var futureDate = DateTime.Today.AddDays(daysFromToday).Date.AddHours(9);
        if (futureDate.Year != futureDate.AddDays(3).Year)
        {
            futureDate = new DateTime(futureDate.Year + 1, 1, 15, 9, 0, 0);
        }

        while (futureDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            futureDate = futureDate.AddDays(1);
        }

        return futureDate;
    }

    private static DateTime NextWeekday(DayOfWeek targetDay, int minimumDaysFromToday)
    {
        var date = FutureDate(minimumDaysFromToday);
        while (date.DayOfWeek != targetDay || date.Year != date.AddDays(3).Year)
        {
            date = date.AddDays(1);
        }

        return date;
    }

    private static DateTime NextWorkingDay(DateTime date)
    {
        do
        {
            date = date.AddDays(1);
        }
        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);

        return date;
    }

    private static async Task SeedManagerApprovalScenarioAsync(HumanResourcesDbContext dbContext)
    {
        dbContext.Departments.Add(new Department
        {
            DepartmentId = 1,
            DepartmentName = "IT"
        });

        dbContext.Employees.AddRange(
            new Employee
            {
                EmployeeId = 10,
                SicilNo = "10010",
                FirstName = "Manager",
                LastName = "One",
                KktcKimlikNo = "1000000001",
                DepartmentId = 1,
                Status = EmploymentStatus.Active
            },
            new Employee
            {
                EmployeeId = 11,
                SicilNo = "10011",
                FirstName = "Employee",
                LastName = "One",
                KktcKimlikNo = "1000000002",
                DepartmentId = 1,
                ManagerId = 10,
                Status = EmploymentStatus.Active
            },
            new Employee
            {
                EmployeeId = 12,
                SicilNo = "10012",
                FirstName = "Manager",
                LastName = "Two",
                KktcKimlikNo = "1000000003",
                DepartmentId = 1,
                Status = EmploymentStatus.Active
            },
            new Employee
            {
                EmployeeId = 13,
                SicilNo = "10013",
                FirstName = "Employee",
                LastName = "Two",
                KktcKimlikNo = "1000000004",
                DepartmentId = 1,
                ManagerId = 12,
                Status = EmploymentStatus.Active
            });

        dbContext.LeaveTypes.Add(new LeaveType
        {
            LeaveTypeId = 1,
            Name = "Yillik Izin",
            AnnualQuota = 20,
            MaxAccrualDays = 50,
            EntitlementKind = LeaveEntitlementKind.ServiceYears0To10
        });

        dbContext.LeaveBalances.AddRange(
            new LeaveBalance
            {
                BalanceId = 1,
                EmployeeId = 11,
                LeaveTypeId = 1,
                Year = DateTime.Today.Year,
                EntitledDays = 20,
                CarryOverDays = 0,
                UsedDays = 0,
                RemainingDays = 20
            },
            new LeaveBalance
            {
                BalanceId = 2,
                EmployeeId = 11,
                LeaveTypeId = 1,
                Year = DateTime.Today.AddYears(1).Year,
                EntitledDays = 20,
                CarryOverDays = 0,
                UsedDays = 0,
                RemainingDays = 20
            },
            new LeaveBalance
            {
                BalanceId = 3,
                EmployeeId = 13,
                LeaveTypeId = 1,
                Year = DateTime.Today.Year,
                EntitledDays = 20,
                CarryOverDays = 0,
                UsedDays = 0,
                RemainingDays = 20
            },
            new LeaveBalance
            {
                BalanceId = 4,
                EmployeeId = 13,
                LeaveTypeId = 1,
                Year = DateTime.Today.AddYears(1).Year,
                EntitledDays = 20,
                CarryOverDays = 0,
                UsedDays = 0,
                RemainingDays = 20
            });

        await dbContext.SaveChangesAsync();
    }
}
