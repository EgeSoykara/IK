using Microsoft.Extensions.Options;

namespace IK.Web.Services;

public sealed class AnnualLeaveEntitlementWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IOptions<AnnualLeaveEntitlementWorkerOptions> options,
    ILogger<AnnualLeaveEntitlementWorker> logger) : BackgroundService
{
    internal const string SystemActor = "annual-leave-entitlement-worker";

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        RunAsync(AssignCurrentYearAsync, stoppingToken);

    internal async Task RunAsync(
        Func<int, CancellationToken, Task<AnnualLeaveEntitlementResult>> assignAsync,
        CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Yıllık izin hak ediş çalışanı yapılandırma ile devre dışı.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var localNow = timeProvider.GetLocalNow();
                var year = localNow.Year;

                var result = await assignAsync(year, stoppingToken);

                logger.LogInformation(
                    "Yıllık izin hak edişi tamamlandı. Yıl={Year}, Oluşturulan={Created}, Mevcut={Existing}, UygunOlmayan={Ineligible}, BaşlangıçTarihiEksik={MissingStartDate}, Uyarı={WarningCount}",
                    result.Year,
                    result.CreatedCount,
                    result.ExistingCount,
                    result.IneligibleCount,
                    result.MissingStartDateEmployeeCount,
                    result.WarningCount);

                var nextRun = CalculateNextRun(year);
                await DelayUntilAsync(nextRun, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Yıllık izin hak edişi başarısız oldu; işlem yeniden denenecek.");
                await Task.Delay(
                    TimeSpan.FromMinutes(options.Value.RetryDelayMinutes),
                    timeProvider,
                    stoppingToken);
            }
        }
    }

    internal DateTimeOffset CalculateNextRun(int completedYear)
    {
        var nextLocalDateTime = new DateTime(
            completedYear + 1,
            1,
            1,
            0,
            0,
            0,
            DateTimeKind.Unspecified);
        return new DateTimeOffset(
            nextLocalDateTime,
            timeProvider.LocalTimeZone.GetUtcOffset(nextLocalDateTime));
    }

    private async Task<AnnualLeaveEntitlementResult> AssignCurrentYearAsync(
        int year,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<LeaveBalanceService>();
        return await service.AssignAutomaticAnnualEntitlementsAsync(
            year,
            SystemActor,
            cancellationToken);
    }

    private async Task DelayUntilAsync(
        DateTimeOffset target,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var remaining = target - timeProvider.GetUtcNow();
            if (remaining <= TimeSpan.Zero)
            {
                return;
            }

            var delay = remaining > TimeSpan.FromDays(1)
                ? TimeSpan.FromDays(1)
                : remaining;
            await Task.Delay(delay, timeProvider, cancellationToken);
        }
    }
}
