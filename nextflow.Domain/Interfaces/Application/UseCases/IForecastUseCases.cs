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
        string? sort = null, string? order = null, Guid? storeId = null, CancellationToken ct = default);
}

/// <summary>Detalhe da recomendação (T-DF3).</summary>
public interface IGetForecastRecommendationUseCase
{
    Task<ResultDto<ForecastRecommendationDetailDto>> Execute(Guid recommendationId, Guid? storeId = null, CancellationToken ct = default);
}

/// <summary>Previsão do produto para a aba "Previsão" (T-DF5), sem recomendação obrigatória.</summary>
public interface IGetProductForecastUseCase
{
    Task<ResultDto<ProductForecastDto>> Execute(Guid productId, int months, CancellationToken ct = default);
}

/// <summary>Aplicar "Comprar" (T-DF4): um pedido de compra em Orçamento por fornecedor (RN-DF15).</summary>
public interface IApplyPurchaseRecommendationsUseCase
{
    Task<ResultDto<ApplyPurchaseResultDto>> Execute(ApplyPurchaseDto? dto, Guid? userId, CancellationToken ct = default);
}

/// <summary>Aplicar "Promover" (T-DF6): cria a promoção ligada à recomendação.</summary>
public interface IApplyPromotionRecommendationUseCase
{
    Task<ResultDto<ApplyPromotionResultDto>> Execute(Guid recommendationId, ApplyPromotionDto? dto, Guid? userId, CancellationToken ct = default);
}

/// <summary>Descartar a recomendação (T-DF8).</summary>
public interface IDismissRecommendationUseCase
{
    Task<ResultDto<ForecastRecommendationListItemDto>> Execute(Guid recommendationId, DismissRecommendationDto? dto, Guid? userId, CancellationToken ct = default);
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

/// <summary>Visão geral da precisão da previsão (T-DF10, F3.6c): por mês, classe ABC e categoria. Só Admin.</summary>
public interface IGetForecastAccuracySummaryUseCase
{
    Task<ResultDto<ForecastAccuracySummaryDto>> Execute(int months, CancellationToken ct = default);
}

/// <summary>Tabela de precisão por produto (T-DF10, F3.6c), pior erro médio primeiro. Só Admin.</summary>
public interface IGetForecastAccuracyProductsUseCase
{
    Task<ResultDto<PaginationDto<ForecastAccuracyProductDto>>> Execute(
        int offset, int limit, IReadOnlyDictionary<string, string>? filters, CancellationToken ct = default);
}
