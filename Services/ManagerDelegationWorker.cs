namespace IK.Web.Services;

public sealed class ManagerDelegationWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<ManagerDelegationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<ManagerDelegationService>();
                var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
                await service.ReconcileAsync(today, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Yönetici vekâleti uzlaştırması başarısız oldu.");
            }

            var now = timeProvider.GetLocalNow();
            var nextRun = now.Date.AddDays(1);
            var delay = nextRun - now;
            if (delay <= TimeSpan.Zero)
            {
                delay = TimeSpan.FromMinutes(1);
            }

            await Task.Delay(delay, timeProvider, stoppingToken);
        }
    }
}
