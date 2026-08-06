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
                actorUserId: "admin",
                actorEmployeeId: 13));
        Assert.Contains("vekil seçimi zorunludur", missingDelegate.Message, StringComparison.OrdinalIgnoreCase);

        var request = await service.CreateRequestAsync(
            employeeId: 10,
            category: LeaveRequestCategory.AnnualLeave,
            startDate: startDate,
            endDate: startDate,
            reason: "Yönetici izni",
            actorUserId: "admin",
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
            actorUserId: "employee-11");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ManagerDecisionAsync(
                requestId: request.RequestId,
                managerEmployeeId: 12,
                approve: true,
                comment: null,
                actorUserId: "manager-12"));

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
            actorUserId: "employee-11");

        await service.ManagerDecisionAsync(
            requestId: request.RequestId,
            managerEmployeeId: 10,
            approve: true,
            comment: null,
            actorUserId: "manager-10");

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
            actorUserId: "employee-11",
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
            actorUserId: "employee-11");

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
            actorUserId: "employee-11");
        await service.CreateRequestAsync(
            employeeId: 11,
            category: LeaveRequestCategory.AnnualLeave,
            startDate: monday,
            endDate: monday.AddDays(1),
            reason: "Tatil ve salı",
            actorUserId: "employee-11");

        dbContext.PublicHolidays.Remove(holiday);
        await dbContext.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ManagerDecisionAsync(
                firstRequest.RequestId,
                managerEmployeeId: 10,
                approve: true,
                comment: null,
                actorUserId: "manager-10"));

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
                actorUserId: "employee-11"));

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
            actorUserId: "employee-11");

        var updatedStartDate = NextWorkingDay(startDate).Date.AddHours(13);
        var updatedEndDate = updatedStartDate.AddHours(4);

        var updatedRequest = await service.UpdateRequestAsync(
            request.RequestId,
            employeeId: 11,
            category: LeaveRequestCategory.AnnualLeave,
            startDate: updatedStartDate,
            endDate: updatedEndDate,
            reason: "Yarim gun izin",
            actorUserId: "employee-11",
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
            actorUserId: "employee-11");

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
                actorUserId: "employee-11"));

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
            actorUserId: "employee-11");

        var secondRequest = await service.CreateRequestAsync(
            employeeId: 11,
            category: LeaveRequestCategory.AnnualLeave,
            startDate: friday.AddDays(1),
            endDate: friday.AddDays(3),
            reason: "Pazartesi izni",
            actorUserId: "employee-11");

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
            actorUserId: "employee-11");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateRequestAsync(
                employeeId: 11,
                category: LeaveRequestCategory.AnnualLeave,
                startDate: friday.AddDays(3),
                endDate: friday.AddDays(4),
                reason: "Pazartesi ve salı izni",
                actorUserId: "employee-11"));

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
            actorUserId: "employee-11",
            isHalfDay: true);

        await service.ManagerDecisionAsync(
            request.RequestId,
            managerEmployeeId: 10,
            approve: true,
            comment: null,
            actorUserId: "manager-10");
        await service.HumanResourcesDecisionAsync(
            request.RequestId,
            humanResourcesEmployeeId: 12,
            approve: true,
            comment: null,
            actorUserId: "hr-12");

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
            actorUserId: "employee-11",
            isHalfDay: true);
        await service.ManagerDecisionAsync(
            request.RequestId,
            managerEmployeeId: 10,
            approve: true,
            comment: null,
            actorUserId: "manager-10");
        await service.HumanResourcesDecisionAsync(
            request.RequestId,
            humanResourcesEmployeeId: 12,
            approve: true,
            comment: null,
            actorUserId: "hr-12");

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
            actorUserId: "admin");

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
            actorUserId: "hr-12");

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
            actorUserId: "admin");

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
            actorUserId: "employee-11");

        await service.ManagerDecisionAsync(
            request.RequestId,
            managerEmployeeId: 10,
            approve: true,
            comment: null,
            actorUserId: "manager-10");
        await service.HumanResourcesDecisionAsync(
            request.RequestId,
            humanResourcesEmployeeId: 12,
            approve: true,
            comment: null,
            actorUserId: "hr-12");

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
            actorUserId: "employee-11");
        Assert.Equal(6m, request.RequestedDays);

        await service.ManagerDecisionAsync(
            request.RequestId,
            managerEmployeeId: 10,
            approve: true,
            comment: null,
            actorUserId: "manager-10");
        await service.HumanResourcesDecisionAsync(
            request.RequestId,
            humanResourcesEmployeeId: 12,
            approve: true,
            comment: null,
            actorUserId: "hr-12");

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
            actorUserId: "employee-11");

        await service.ManagerDecisionAsync(
            request.RequestId,
            managerEmployeeId: 10,
            approve: true,
            comment: null,
            actorUserId: "manager-10");
        request.RequestedDays = 4m;
        await dbContext.SaveChangesAsync();

        await service.HumanResourcesDecisionAsync(
            request.RequestId,
            humanResourcesEmployeeId: 12,
            approve: true,
            comment: null,
            actorUserId: "hr-12");

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
            actorUserId: "employee-11");

        var updatedRequest = await service.UpdateRequestAsync(
            request.RequestId,
            employeeId: 13,
            category: LeaveRequestCategory.AnnualLeave,
            startDate: startDate.AddDays(3),
            endDate: startDate.AddDays(4),
            reason: "Calisan degisti",
            actorUserId: "admin");

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
            actorUserId: "employee-11");

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
                actorUserId: "admin"));

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
            actorUserId: "employee-11");

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
                actorUserId: "admin"));

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
            actorUserId: "employee-11",
            leaveTypeId: 7);

        Assert.Equal(LeaveRequestCategory.SpecificLeaveType, request.Category);
        Assert.Equal(7, request.LeaveTypeId);
        await service.ManagerDecisionAsync(
            request.RequestId,
            managerEmployeeId: 10,
            approve: true,
            comment: null,
            actorUserId: "manager-10");
        await service.HumanResourcesDecisionAsync(
            request.RequestId,
            humanResourcesEmployeeId: 12,
            approve: true,
            comment: null,
            actorUserId: "hr-12");
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
                actorUserId: "employee-11",
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
                actorUserId: "employee-11",
                leaveTypeId: 6));

        Assert.Equal("Çalışan seçilen izin türü için uygun değil.", exception.Message);
    }

    private static LeaveRequestService CreateService(HumanResourcesDbContext dbContext)
    {
        var auditLogService = new AuditLogService(dbContext);
        return new LeaveRequestService(
            dbContext,
            new LeaveDayCalculator(),
            new LeaveEntitlementService(),
            new PublicHolidayCalendar(dbContext),
            auditLogService,
            new ManagerDelegationService(dbContext, auditLogService, TimeProvider.System),
            TimeProvider.System,
            NullLogger<LeaveRequestService>.Instance);
    }

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
