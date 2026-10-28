using Nextflow.Domain.Dtos;

namespace Nextflow.Domain.Interfaces.UseCases;

/// <summary>Reconstrói o agregado <c>product_sales_monthly</c> (vendas confirmadas, últimos 36 meses).</summary>
public interface IRebuildProductSalesMonthlyUseCase
{
    /// <summary>Retorna a quantidade de linhas (produto × mês) gravadas.</summary>
    Task<int> Execute(CancellationToken ct);
}

/// <summary>Gera uma nova análise de previsão de demanda (cálculo + IA + guardrails + persistência).</summary>
public interface IGenerateForecastUseCase
{
    Task<ForecastRunDto> Execute(Guid userId, GenerateForecastRequestDto dto, CancellationToken ct);
}

/// <summary>Última análise (ou null quando nunca houve uma).</summary>
public interface IGetLatestForecastUseCase
{
    Task<ForecastRunDto?> Execute(CancellationToken ct);
}

/// <summary>Histórico, projeção, sazonalidade e última recomendação de um produto.</summary>
public interface IGetProductForecastUseCase
{
    Task<ProductForecastDto> Execute(Guid productId, int months, CancellationToken ct);
}

/// <summary>Aplica recomendações "Comprar": um pedido de compra (Orçamento) por fornecedor.</summary>
public interface IApplyPurchaseRecommendationsUseCase
{
    Task<ApplyPurchaseResultDto> Execute(Guid userId, ApplyPurchaseRequestDto dto, CancellationToken ct);
}

/// <summary>Aplica uma recomendação "Promover" criando a promoção correspondente.</summary>
public interface IApplyPromotionRecommendationUseCase
{
    Task<ForecastRecommendationDto> Execute(Guid userId, Guid recommendationId, ApplyPromotionRequestDto dto, CancellationToken ct);
}

/// <summary>Descarta uma recomendação pendente.</summary>
public interface IDismissRecommendationUseCase
{
    Task<ForecastRecommendationDto> Execute(Guid recommendationId, string? reason, CancellationToken ct);
}
