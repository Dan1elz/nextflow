using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nextflow.Domain.Interfaces.Application.Services;

namespace Nextflow.Infrastructure.Services.Forecast;

/// <summary>
/// Relógio do agendamento semanal (RN-DF18): a cada minuto pede ao <see cref="IForecastScheduleService"/> (escopo próprio) para
/// conferir se passou um horário agendado. Na subida, o momento atual conta como já tratado (sem disparo ao ligar a API).
/// </summary>
public sealed class ForecastScheduler(IServiceScopeFactory scopeFactory, IBusinessClock clock, ILogger<ForecastScheduler> logger) : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var handled = clock.Now;
        using var timer = new PeriodicTimer(Tick);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                handled = await scope.ServiceProvider.GetRequiredService<IForecastScheduleService>().CheckAsync(handled, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Previsão: falha ao conferir o agendamento semanal.");
            }
        }
    }
}
