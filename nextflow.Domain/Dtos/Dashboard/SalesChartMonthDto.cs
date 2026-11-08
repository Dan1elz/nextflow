namespace Nextflow.Domain.Dtos.Dashboard;

/// <summary>Um mês do gráfico do painel (T-DF1).</summary>
public class SalesChartMonthDto
{
    /// <summary>"aaaa-mm".</summary>
    public string Month { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal? RevenueLastYear { get; set; }
}
