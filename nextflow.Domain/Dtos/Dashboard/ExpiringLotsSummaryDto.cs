namespace Nextflow.Domain.Dtos.Dashboard;

/// <summary>Bloco "Lotes vencendo" (T-DF1): quantos lotes × locais vencem na janela, o valor a custo médio e os 5 primeiros a vencer.</summary>
public class ExpiringLotsSummaryDto
{
    public int Count { get; set; }
    public decimal TotalCostValue { get; set; }
    public List<ExpiringLotItemDto> Items { get; set; } = [];
}
