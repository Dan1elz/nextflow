namespace Nextflow.Domain.Dtos.Dashboard;

/// <summary>Bloco "Estoque baixo" (T-DF1): total de variações × locais abaixo do mínimo e as 5 mais críticas.</summary>
public class LowStockSummaryDto
{
    public int Count { get; set; }
    public List<LowStockItemDto> Items { get; set; } = [];
}
