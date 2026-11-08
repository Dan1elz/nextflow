using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nextflow.Domain.Interfaces.Application.Services;

namespace Nextflow.Infrastructure.Services.Notifications;

/// <summary>
/// Relógio do resumo diário (L-DF4): a cada 30 minutos pede ao <see cref="INotificationDigestService"/> (escopo próprio) para
/// publicar o resumo do dia, que só sai uma vez por dia de negócio, a partir das 07:00.
/// </summary>
public sealed class NotificationDigestScheduler(IServiceScopeFactory scopeFactory, ILogger<NotificationDigestScheduler> logger) : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromMinutes(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Tick);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<INotificationDigestService>().RunAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Notificações: falha ao publicar o resumo diário.");
            }
        }
    }
}
