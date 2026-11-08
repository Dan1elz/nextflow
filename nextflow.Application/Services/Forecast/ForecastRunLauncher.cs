using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nextflow.Domain.Dtos.Forecasts;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.Services.Forecast;

/// <summary>
/// Abre a análise em <c>Running</c> e enfileira (RN-DF18, RN-DF23). A checagem antes do commit dá a resposta rápida; o índice único
/// <c>ux_forecast_runs_running</c> fecha a corrida entre dois disparos simultâneos (o segundo também volta nulo).
/// </summary>
public class ForecastRunLauncher(
    IForecastRunRepository runRepository,
    IUnitOfWork unitOfWork,
    IForecastQueue queue,
    IBusinessClock clock,
    ILogger<ForecastRunLauncher> logger) : IForecastRunLauncher
{
    public async Task<ForecastRun?> StartAsync(ForecastTriggerEnum trigger, Guid? requestedById, CancellationToken ct = default)
    {
        if (await runRepository.Exists(r => r.Status == ForecastRunStatusEnum.Running, ct))
            return null;

        var run = new ForecastRun(new StartForecastRunDto(trigger, requestedById), clock.Today, clock.UtcNow);
        await runRepository.AddAsync(run, ct);
        try
        {
            await unitOfWork.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            unitOfWork.Rollback();
            logger.LogInformation("Previsão: disparo {Trigger} ignorado, outra análise começou ao mesmo tempo.", trigger);
            return null;
        }

        await queue.EnqueueAsync(run.Id, ct);
        logger.LogInformation("Previsão {RunId}: análise {Trigger} enfileirada.", run.Id, trigger);
        return run;
    }
}
