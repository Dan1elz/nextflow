using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nextflow.Domain.Interfaces.Application.Services;

namespace Nextflow.Infrastructure.Services.Forecast;

/// <summary>
/// Job da análise em segundo plano (RN-DF23, L-DF1): lê a fila e processa uma análise por vez, cada uma num escopo de DI próprio.
/// Na subida, fecha como falha a análise que ficou em andamento por uma queda da API, senão o índice único travaria a próxima.
/// </summary>
public sealed class ForecastRunWorker(IServiceScopeFactory scopeFactory, IForecastQueue queue, ILogger<ForecastRunWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IForecastRunProcessor>().FailInterruptedAsync(stoppingToken);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Previsão: não foi possível fechar as análises interrompidas na subida.");
        }

        await foreach (var runId in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IForecastRunProcessor>().ProcessAsync(runId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Previsão {RunId}: erro inesperado no job da análise.", runId);
            }
        }
    }
}
