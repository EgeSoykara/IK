using Microsoft.Extensions.Options;

namespace IK.Web.Services;

public sealed class DailyLeaveEntitlementWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IOptions<DailyLeaveEntitlementWorkerOptions> options,
    ILogger<DailyLeaveEntitlementWorker> logger) : BackgroundService
{
    internal const string SystemActor = "daily-leave-entitlement-worker";

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        RunAsync(ReconcileAsync, stoppingToken);

    internal async Task RunAsync(
        Func<DateOnly, CancellationToken, Task<DailyLeaveEntitlementResult>> reconcileAsync,
        CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Günlük izin hak ediş çalışanı yapılandırma ile devre dışı.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var localNow = timeProvider.GetLocalNow();
                var processingDate = DateOnly.FromDateTime(localNow.DateTime);
                var result = await reconcileAsync(processingDate, stoppingToken);

                logger.LogInformation(
                    "Günlük izin hak ediş uzlaştırması tamamlandı. Tarih={Date}, Oluşturulan={Created}, Güncellenen={Updated}, Değişmeyen={Unchanged}, UygunOlmayan={Ineligible}, BaşlangıçTarihiEksik={MissingStartDate}, UyarıDeğişimi={WarningCount}",
                    result.ProcessingDate,
                    result.CreatedCount,
                    result.UpdatedCount,
                    result.UnchangedCount,
                    result.IneligibleCount,
                    result.MissingStartDateEmployeeCount,
                    result.WarningCount);

                await DelayUntilAsync(CalculateNextRun(localNow), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Günlük izin hak ediş uzlaştırması başarısız oldu; işlem yeniden denenecek.");
                await Task.Delay(
                    TimeSpan.FromMinutes(options.Value.RetryDelayMinutes),
                    timeProvider,
                    stoppingToken);
            }
        }
    }

    internal DateTimeOffset CalculateNextRun(DateTimeOffset localNow)
    {
        var nextDate = DateOnly.FromDateTime(localNow.DateTime).AddDays(1);
        var nextLocalDateTime = nextDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(
            nextLocalDateTime,
            timeProvider.LocalTimeZone.GetUtcOffset(nextLocalDateTime));
    }

    private async Task<DailyLeaveEntitlementResult> ReconcileAsync(
        DateOnly processingDate,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<LeaveBalanceService>();
        return await service.ReconcileAutomaticEntitlementsAsync(
            processingDate,
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

            var delay = remaining > TimeSpan.FromHours(6)
                ? TimeSpan.FromHours(6)
                : remaining;
            await Task.Delay(delay, timeProvider, cancellationToken);
        }
    }
}
