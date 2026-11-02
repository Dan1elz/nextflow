using Nextflow.Domain.Enums;

namespace Nextflow.Domain.Dtos.Stock;

/// <summary>Linha de um documento de estoque. A quantidade é sempre positiva; o sentido vem de <see cref="Type"/>.</summary>
public class StockLineDto
{
    public Guid VariantId { get; set; }
    public Guid LocationId { get; set; }

    /// <summary>Nulo até a F1.7 (lotes).</summary>
    public Guid? LotId { get; set; }

    public MovementTypeEnum Type { get; set; }
    public MovementReasonEnum Reason { get; set; }
    public decimal Quantity { get; set; }

    /// <summary>Só a entrada informa custo, e só ela recalcula o custo médio (RN-CE31). Na saída é ignorado.</summary>
    public decimal? UnitCost { get; set; }
}
