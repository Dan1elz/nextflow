namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>Erro médio dos produtos medidos num mês (T-DF10); nulo e zero produtos quando o mês não teve medição.</summary>
public class ForecastAccuracyMonthDto
{
    /// <summary>"aaaa-mm".</summary>
    public string Month { get; set; } = string.Empty;
    public decimal? MapePercent { get; set; }
    public int ProductsMeasured { get; set; }
}
