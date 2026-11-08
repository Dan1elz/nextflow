namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>
/// Um mês completo de um produto para a precisão (RN-DF19, F3.6b): a projeção de referência (última análise concluída antes do mês),
/// o vendido e os dias sem estoque de <c>product_sales_monthly</c>.
/// </summary>
public record ForecastAccuracyRowDto(Guid ProductId, short Year, short Month, decimal Projected, decimal Actual, short DaysOutOfStock);
