using Microsoft.Extensions.Logging;
using Nextflow.Domain.Dtos.Forecasts;
using Nextflow.Domain.Dtos.Notifications;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;

namespace Nextflow.Application.Services.Forecast;

/// <summary>
/// Um passo do job em segundo plano (F3.3): recalcula os dados derivados (F3.1, pré-requisito do motor), roda o motor (F3.2) e
/// fecha a análise. O motor já trata as próprias falhas; aqui fica a falha do recálculo e a análise órfã de uma queda da API.
/// </summary>
public class ForecastRunProcessor(
    IDerivedDataService derivedDataService,
    IForecastEngine engine,
    IForecastRunRepository runRepository,
    INotificationPublisher publisher,
    IUnitOfWork unitOfWork,
    IBusinessClock clock,
    ILogger<ForecastRunProcessor> logger) : IForecastRunProcessor
{
    public const string InterruptedMessage = "Análise interrompida: a API reiniciou antes de terminar.";
    public const string CanceledMessage = "Análise cancelada antes de terminar.";

    public async Task ProcessAsync(Guid runId, CancellationToken ct)
    {
        var run = await runRepository.GetById(runId, ct: ct);
        if (run is null || !run.IsRunning)
        {
            logger.LogWarning("Previsão {RunId}: análise inexistente ou já fechada; nada a processar.", runId);
            return;
        }

        try
        {
            await derivedDataService.RebuildAsync(ct);
        }
        catch (OperationCanceledException)
        {
            await FailAsync(runId, CanceledMessage);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Previsão {RunId}: falha ao recalcular os dados derivados.", runId);
            await FailAsync(runId, $"Falha ao recalcular os dados de venda: {ex.Message}");
            return;
        }

        var outcome = await engine.RunAsync(runId, ct);
        await NotifyAsync(outcome, ct);
    }

    /// <summary>
    /// RN-DF18 e RN-DF23: análise concluída avisa no sino de todos os usuários ativos (L-DF8). A análise já está fechada: falha aqui só
    /// vira log.
    /// </summary>
    private async Task NotifyAsync(ForecastRunOutcomeDto outcome, CancellationToken ct)
    {
        if (outcome.Status is not (ForecastRunStatusEnum.Completed or ForecastRunStatusEnum.CompletedWithoutAi))
            return;

        try
        {
            var actions = outcome.Buy + outcome.Promote;
            var message = actions == 0
                ? $"Nenhuma compra ou promoção sugerida para os {outcome.ProductsAnalyzed} produtos analisados."
                : $"{outcome.Buy} produto(s) para comprar e {outcome.Promote} para promover, de {outcome.ProductsAnalyzed} analisados.";
            if (outcome.Status == ForecastRunStatusEnum.CompletedWithoutAi)
                message += " Análise feita só com o cálculo (sem IA).";

            await publisher.PublishToAllAsync(new NewNotificationDto(
                NotificationTypeEnum.NewRecommendations, "Análise de previsão concluída", message, "/forecast"), ct);
            await unitOfWork.CommitAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            unitOfWork.Rollback();
            logger.LogError(ex, "Previsão {RunId}: análise concluída, mas a notificação falhou.", outcome.RunId);
        }
    }

    public async Task FailInterruptedAsync(CancellationToken ct)
    {
        var orphans = await runRepository.GetAllForUpdate(r => r.Status == ForecastRunStatusEnum.Running, ct);
        if (orphans.Count == 0)
            return;

        foreach (var orphan in orphans)
            orphan.Fail(InterruptedMessage, clock.UtcNow);
        await unitOfWork.CommitAsync(ct);
        logger.LogWarning("Previsão: {Count} análise(s) presa(s) em andamento fechada(s) como falha na subida da API.", orphans.Count);
    }

    /// <summary>Fecha como Failed mesmo com o token cancelado (exceção de rollback à cadeia de token, como no motor).</summary>
    private async Task FailAsync(Guid runId, string message)
    {
        unitOfWork.Rollback();
        var run = await runRepository.GetByIdForUpdate(runId, ct: CancellationToken.None);
        if (run is null || !run.IsRunning)
            return;

        run.Fail(message.Length <= 1000 ? message : message[..999] + "…", clock.UtcNow);
        await unitOfWork.CommitAsync(CancellationToken.None);
    }
}
