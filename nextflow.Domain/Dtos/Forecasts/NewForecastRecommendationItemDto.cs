namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>Parte da quantidade de compra que cabe a uma variação, pela proporção histórica de venda da grade (RN-DF21).</summary>
public record NewForecastRecommendationItemDto(Guid VariantId, decimal Quantity, decimal SharePercent);
