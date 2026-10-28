using Nextflow.Application.Forecast;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Exceptions;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Interfaces.UseCases;
using Nextflow.Domain.Utils;

namespace Nextflow.Application.UseCases.Forecasts;

/// <summary>
/// Dados de previsão de um produto para os gráficos: histórico mensal (meses completos, zerados preenchidos),
/// projeção do último run que incluiu o produto, perfil sazonal e última recomendação.
/// O histórico vem de <c>product_sales_monthly</c>; se a tabela ainda estiver vazia (nenhuma análise gerada),
/// é calculado direto dos pedidos confirmados.
/// </summary>
public class GetProductForecastUseCase(
    IProductRepository productRepository,
    IProductSalesMonthlyRepository salesMonthlyRepository,
    IProductSeasonalityRepository seasonalityRepository,
    IForecastProjectionRepository projectionRepository,
    IForecastRecommendationRepository recommendationRepository
) : IGetProductForecastUseCase
{
    public const int DefaultMonths = 24;
    public const int MaxMonths = 36;

    public async Task<ProductForecastDto> Execute(Guid productId, int months, CancellationToken ct)
    {
        months = months <= 0 ? DefaultMonths : Math.Min(months, MaxMonths);

        var product = await productRepository.GetByIdAsync(productId, ct)
            ?? throw new NotFoundException("Produto não encontrado.");

        var today = BusinessClock.Today;
        var lastMonth = YearMonth.From(today).AddMonths(-1);
        var firstMonth = lastMonth.AddMonths(-(months - 1));

        // Histórico (últimos N meses completos)
        List<(int Year, int Month, decimal Quantity, decimal Revenue)> rows;
        if (await salesMonthlyRepository.ExistsAsync(_ => true, ct))
        {
            rows = [.. (await salesMonthlyRepository.GetAllAsync(
                    s => s.ProductId == productId &&
                         (s.Year > firstMonth.Year || (s.Year == firstMonth.Year && s.Month >= firstMonth.Month)),
                    0, int.MaxValue, ct))
                .Select(s => (s.Year, s.Month, s.QuantitySold, s.Revenue))];
        }
        else
        {
            var fromUtc = BusinessClock.ToUtc(firstMonth.FirstDay, TimeOnly.MinValue);
            rows = [.. (await salesMonthlyRepository.AggregateConfirmedSalesAsync(fromUtc, productId, ct))
                .Select(s => (s.Year, s.Month, s.QuantitySold, s.Revenue))];
        }

        var byMonth = rows
            .GroupBy(r => new YearMonth(r.Year, r.Month))
            .ToDictionary(g => g.Key, g => (Quantity: g.Sum(x => x.Quantity), Revenue: g.Sum(x => x.Revenue)));

        var history = Enumerable.Range(0, months)
            .Select(i => firstMonth.AddMonths(i))
            .Select(m => new ForecastHistoryPointDto
            {
                Year = m.Year,
                Month = m.Month,
                Label = m.Label,
                QuantitySold = byMonth.TryGetValue(m, out var v) ? v.Quantity : 0,
                Revenue = byMonth.TryGetValue(m, out var r) ? Math.Round(r.Revenue, 2) : 0
            })
            .ToList();

        // Projeção do último run que incluiu o produto
        var projection = new List<ForecastProjectionPointDto>();
        var latestProjection = (await projectionRepository.GetAllAsync(p => p.ProductId == productId, 0, 1, ct)).FirstOrDefault();
        if (latestProjection != null)
        {
            projection = [.. (await projectionRepository.GetAllAsync(
                    p => p.ProductId == productId && p.ForecastRunId == latestProjection.ForecastRunId, 0, int.MaxValue, ct))
                .OrderBy(p => p.Year).ThenBy(p => p.Month)
                .Select(p => new ForecastProjectionPointDto
                {
                    Year = p.Year,
                    Month = p.Month,
                    Label = new YearMonth(p.Year, p.Month).Label,
                    ProjectedQuantity = p.ProjectedQuantity
                })];
        }

        var seasonality = (await seasonalityRepository.GetAllAsync(s => s.ProductId == productId, 0, 12, ct))
            .OrderBy(s => s.Month)
            .Select(s => new SeasonalityPointDto { Month = s.Month, SeasonalIndex = s.SeasonalIndex })
            .ToList();

        var latestRecommendation = (await recommendationRepository.GetAllAsync(
                r => r.ProductId == productId, 0, 1, ct, query => ForecastMappings.IncludeProduct(query, today)))
            .FirstOrDefault();

        return new ProductForecastDto
        {
            ProductId = product.Id,
            ProductName = product.Name,
            History = history,
            Projection = projection,
            Seasonality = seasonality,
            LatestRecommendation = latestRecommendation != null ? new ForecastRecommendationDto(latestRecommendation) : null
        };
    }
}
