using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Nextflow.Application.Filters;
using Nextflow.Application.Forecast;
using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Forecasts;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Application.UseCases;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Forecast;

/// <summary>Leituras comuns dos cabeçalhos de análise: contagens das recomendações e a próxima execução agendada (RN-DF18).</summary>
internal static class ForecastRunReading
{
    public const string NotFound = "Análise de previsão não encontrada.";

    public static Func<IQueryable<ForecastRun>, IQueryable<ForecastRun>> Include() => q => q.Include(r => r.RequestedBy);

    /// <summary>Próximo horário da análise automática, em UTC; nulo com o agendamento desligado.</summary>
    public static async Task<DateTime?> NextScheduledAtAsync(IForecastSettingsRepository settingsRepository, IBusinessClock clock, CancellationToken ct)
    {
        var settings = await settingsRepository.Get(_ => true, ct: ct) ?? new ForecastSettings();
        return NextScheduledAt(settings, clock);
    }

    public static DateTime? NextScheduledAt(ForecastSettings settings, IBusinessClock clock)
    {
        if (!settings.ScheduleEnabled)
            return null;
        var now = clock.Now;
        return ForecastSchedule.ToUtc(ForecastSchedule.NextSlot(now, settings.Weekday, settings.Time), now, clock.UtcNow);
    }

    public static async Task<ForecastRunSummaryDto> SummaryAsync(
        ForecastRun run,
        IForecastRecommendationRepository recommendationRepository,
        IForecastSettingsRepository settingsRepository,
        IBusinessClock clock,
        CancellationToken ct)
    {
        var counts = await recommendationRepository.GetCountsByRun([run.Id], ct);
        return new ForecastRunSummaryDto(run, counts.GetValueOrDefault(run.Id), await NextScheduledAtAsync(settingsRepository, clock, ct));
    }
}

/// <summary>
/// "Gerar nova análise" (T-DF2, RN-DF18): sem parâmetro de horizonte (RN-DF17 é automático). Abre a análise em <c>Running</c> e
/// enfileira; a resposta volta na hora e o front acompanha por <c>/status</c> (L-DF1). Já havendo uma em andamento, 409 (RN-DF23).
/// </summary>
public class GenerateForecastUseCase(
    IForecastRunLauncher launcher,
    IForecastRunRepository runRepository,
    IForecastRecommendationRepository recommendationRepository,
    IForecastSettingsRepository settingsRepository,
    IBusinessClock clock,
    IResultFactory<ForecastRunSummaryDto> resultFactory) : IGenerateForecastUseCase
{
    public const string AlreadyRunning = "Já existe uma análise em andamento. Aguarde ela terminar.";

    public async Task<ResultDto<ForecastRunSummaryDto>> Execute(Guid? userId, CancellationToken ct = default)
    {
        if (userId is null)
            return resultFactory.Failure("Usuário não identificado.", 401);

        var run = await launcher.StartAsync(ForecastTriggerEnum.Manual, userId, ct);
        if (run is null)
            return resultFactory.Failure(AlreadyRunning, 409);

        // Relê com quem pediu (o job pode já ter avançado: o cabeçalho mostra o estado do momento).
        var current = await runRepository.GetById(run.Id, ForecastRunReading.Include(), ct) ?? run;
        return resultFactory.Success(await ForecastRunReading.SummaryAsync(current, recommendationRepository, settingsRepository, clock, ct));
    }
}

/// <summary>Cabeçalho da última análise (qualquer status), ou nulo se nunca rodou (T-DF2).</summary>
public class GetLatestForecastUseCase(
    IForecastRunRepository runRepository,
    IForecastRecommendationRepository recommendationRepository,
    IForecastSettingsRepository settingsRepository,
    IBusinessClock clock,
    IResultFactory<ForecastRunSummaryDto?> resultFactory) : IGetLatestForecastUseCase
{
    public async Task<ResultDto<ForecastRunSummaryDto?>> Execute(CancellationToken ct = default)
    {
        var latest = (await runRepository.GetAll(_ => true, 0, 1, ForecastRunReading.Include(),
            q => q.OrderByDescending(r => r.StartedAt).ThenByDescending(r => r.CreatedAt), ct)).FirstOrDefault();

        return resultFactory.Success(latest is null
            ? null
            : await ForecastRunReading.SummaryAsync(latest, recommendationRepository, settingsRepository, clock, ct));
    }
}

/// <summary>Uma análise pelo id (T-DF9).</summary>
public class GetForecastRunUseCase(
    IForecastRunRepository runRepository,
    IForecastRecommendationRepository recommendationRepository,
    IForecastSettingsRepository settingsRepository,
    IBusinessClock clock,
    IResultFactory<ForecastRunSummaryDto> resultFactory) : IGetForecastRunUseCase
{
    public async Task<ResultDto<ForecastRunSummaryDto>> Execute(Guid runId, CancellationToken ct = default)
    {
        var run = await runRepository.GetById(runId, ForecastRunReading.Include(), ct);
        return run is null
            ? resultFactory.Failure(ForecastRunReading.NotFound, 404)
            : resultFactory.Success(await ForecastRunReading.SummaryAsync(run, recommendationRepository, settingsRepository, clock, ct));
    }
}

/// <summary>Andamento da análise (L-DF1): status, lotes e percentual.</summary>
public class GetForecastProgressUseCase(
    IForecastRunRepository runRepository,
    IResultFactory<ForecastRunProgressDto> resultFactory) : IGetForecastProgressUseCase
{
    public async Task<ResultDto<ForecastRunProgressDto>> Execute(Guid runId, CancellationToken ct = default)
    {
        var run = await runRepository.GetById(runId, ct: ct);
        return run is null
            ? resultFactory.Failure(ForecastRunReading.NotFound, 404)
            : resultFactory.Success(new ForecastRunProgressDto(run));
    }
}

/// <summary>Lotes da IA da análise, na ordem de envio (T-DF9).</summary>
public class GetForecastBatchesUseCase(
    IForecastRunRepository runRepository,
    IForecastAiBatchRepository batchRepository,
    IResultFactory<List<ForecastAiBatchDto>> resultFactory) : IGetForecastBatchesUseCase
{
    public async Task<ResultDto<List<ForecastAiBatchDto>>> Execute(Guid runId, CancellationToken ct = default)
    {
        if (!await runRepository.Exists(r => r.Id == runId, ct))
            return resultFactory.Failure(ForecastRunReading.NotFound, 404);

        var batches = await batchRepository.GetAll(b => b.ForecastRunId == runId, 0, int.MaxValue, orderBy: q => q.OrderBy(b => b.BatchNumber), ct: ct);
        return resultFactory.Success([.. batches.Select(b => new ForecastAiBatchDto(b))]);
    }
}

/// <summary>
/// Histórico de análises (T-DF9), mais recente primeiro. Filtros: <c>status</c>, <c>trigger</c> e o período <c>from</c>/<c>to</c>
/// sobre a data de referência.
/// </summary>
public class GetAllForecastRunsUseCase(
    IForecastRunRepository runRepository,
    IForecastRecommendationRepository recommendationRepository,
    IForecastSettingsRepository settingsRepository,
    IBusinessClock clock,
    IResultFactory<PaginationDto<ForecastRunSummaryDto>> resultFactory)
    : GetAllUseCaseBase<ForecastRun, IForecastRunRepository, ForecastRunSummaryDto>(runRepository, resultFactory)
{
    private Dictionary<Guid, ForecastRecommendationCountsDto> _counts = [];
    private DateTime? _nextScheduledAt;

    protected override Expression<Func<ForecastRun, bool>> BuildFilterExpression(IReadOnlyDictionary<string, string>? filters)
    {
        var set = new FilterSet(filters);
        var builder = new FilterExpressionBuilder<ForecastRun>()
            .WhereEnumEquals(set, "status", r => r.Status)
            .WhereEnumEquals(set, "trigger", r => r.Trigger)
            .WhereDateOnlyGte(set, "from", r => r.ReferenceDate)
            .WhereDateOnlyLte(set, "to", r => r.ReferenceDate);
        return builder.Predicate;
    }

    protected override Func<IQueryable<ForecastRun>, IQueryable<ForecastRun>> GetInclude() => ForecastRunReading.Include();

    protected override Func<IQueryable<ForecastRun>, IOrderedQueryable<ForecastRun>> GetOrderBy()
        => q => q.OrderByDescending(r => r.StartedAt).ThenByDescending(r => r.CreatedAt).ThenBy(r => r.Id);

    protected override async Task PrepareMapping(IReadOnlyList<ForecastRun> items, CancellationToken ct)
    {
        _counts = await recommendationRepository.GetCountsByRun([.. items.Select(r => r.Id)], ct);
        _nextScheduledAt = await ForecastRunReading.NextScheduledAtAsync(settingsRepository, clock, ct);
    }

    protected override ForecastRunSummaryDto MapToResponseDto(ForecastRun entity)
        => new(entity, _counts.GetValueOrDefault(entity.Id), _nextScheduledAt);
}
