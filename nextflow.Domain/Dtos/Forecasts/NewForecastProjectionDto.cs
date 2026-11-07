namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>Demanda projetada de um produto num mês do horizonte (gráfico da T-DF3 e base da precisão, RN-DF19).</summary>
public record NewForecastProjectionDto(Guid ForecastRunId, Guid ProductId, short Year, short Month, decimal ProjectedQuantity);
