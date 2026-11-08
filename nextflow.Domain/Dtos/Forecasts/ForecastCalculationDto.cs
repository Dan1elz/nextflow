namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>
/// Números do cálculo da recomendação (T-DF3). Os gravados pelo motor (prazo, segurança, necessidade, histórico) vêm da recomendação;
/// política, vida útil e pedido mínimo/múltiplo vêm do cadastro atual do produto. Estoque reservado é 0 enquanto não há orçamento
/// (F2.5 fora do escopo).
/// </summary>
public class ForecastCalculationDto
{
    public int LeadTimeDays { get; set; }
    public string LeadTimeSource { get; set; } = string.Empty;
    public decimal SafetyStock { get; set; }
    public decimal? PurchaseNeed { get; set; }
    public decimal ReservedStock { get; set; }
    public int? TargetCoverageDays { get; set; }
    public int? ShelfLifeDays { get; set; }
    public decimal? MinOrderQty { get; set; }
    public decimal? OrderMultiple { get; set; }
    public bool BasedOnLastYear { get; set; }
    public int MonthsOfHistory { get; set; }
    public decimal NextMonthDemand { get; set; }
    public decimal NextMonthSeasonalIndex { get; set; }
}
