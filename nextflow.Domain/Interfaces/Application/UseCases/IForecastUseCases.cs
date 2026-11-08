using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Forecasts;

namespace Nextflow.Domain.Interfaces.Application.UseCases;

/// <summary>Botão "Gerar nova análise" (T-DF2): abre a análise em segundo plano; 409 se já houver uma em andamento (RN-DF23).</summary>
public interface IGenerateForecastUseCase
{
    Task<ResultDto<ForecastRunSummaryDto>> Execute(Guid? userId, CancellationToken ct = default);
}

/// <summary>Cabeçalho da última análise (T-DF2); <c>null</c> quando nunca rodou.</summary>
public interface IGetLatestForecastUseCase
{
    Task<ResultDto<ForecastRunSummaryDto?>> Execute(CancellationToken ct = default);
}

/// <summary>Uma análise (T-DF9, abrir a análise do histórico).</summary>
public interface IGetForecastRunUseCase
{
    Task<ResultDto<ForecastRunSummaryDto>> Execute(Guid runId, CancellationToken ct = default);
}

/// <summary>Andamento para o polling da T-DF2 (L-DF1).</summary>
public interface IGetForecastProgressUseCase
{
    Task<ResultDto<ForecastRunProgressDto>> Execute(Guid runId, CancellationToken ct = default);
}

/// <summary>Lotes da IA de uma análise (T-DF9).</summary>
public interface IGetForecastBatchesUseCase
{
    Task<ResultDto<List<ForecastAiBatchDto>>> Execute(Guid runId, CancellationToken ct = default);
}

/// <summary>Tabela de recomendações de uma análise, com filtros e ordenação (T-DF2).</summary>
public interface IGetForecastRecommendationsUseCase
{
    Task<ResultDto<PaginationDto<ForecastRecommendationListItemDto>>> Execute(
        Guid runId, int offset, int limit, IReadOnlyDictionary<string, string>? filters,
        string? sort = null, string? order = null, CancellationToken ct = default);
}

/// <summary>Agendamento da análise (T-DF11).</summary>
public interface IGetForecastSettingsUseCase
{
    Task<ResultDto<ForecastSettingsDto>> Execute(CancellationToken ct = default);
}

/// <summary>Salva o agendamento da análise (T-DF11).</summary>
public interface IUpdateForecastSettingsUseCase
{
    Task<ResultDto<ForecastSettingsDto>> Execute(ForecastSettingsRequestDto dto, Guid? userId, CancellationToken ct = default);
}
