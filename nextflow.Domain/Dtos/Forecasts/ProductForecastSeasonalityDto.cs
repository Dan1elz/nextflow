namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>
/// Índice sazonal de um mês do calendário (T-DF3). O do produto vem da F3.1 (só com 12 meses ou mais de histórico); o da categoria
/// não é gravado (o motor o recalcula a cada análise) e vem nulo.
/// </summary>
public class ProductForecastSeasonalityDto
{
    public int Month { get; set; }
    public decimal? ProductIndex { get; set; }
    public decimal? CategoryIndex { get; set; }
}
