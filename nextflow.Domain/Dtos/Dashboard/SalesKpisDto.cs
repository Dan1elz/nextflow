namespace Nextflow.Domain.Dtos.Dashboard;

/// <summary>
/// KPIs de venda do mês corrente (T-DF1), por loja ou da empresa. Margem bruta = receita − custo médio congelado dos itens
/// (<c>order_items.unit_cost</c>). As comparações são nulas quando o mês de referência não teve venda.
/// </summary>
public class SalesKpisDto
{
    /// <summary>"aaaa-mm".</summary>
    public string Month { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int OrderCount { get; set; }
    public decimal AverageTicket { get; set; }
    public decimal GrossMargin { get; set; }
    public decimal GrossMarginPercent { get; set; }
    public decimal? RevenueVsLastYearPercent { get; set; }
    public decimal? RevenueVsLastMonthPercent { get; set; }
}
