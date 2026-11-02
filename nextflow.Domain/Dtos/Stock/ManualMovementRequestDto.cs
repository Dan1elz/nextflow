using Nextflow.Domain.Enums;

namespace Nextflow.Domain.Dtos.Stock;

/// <summary>
/// Ajuste, perda ou saldo de abertura lançado à mão (contrato T9, <c>ManualMovementDto</c>). A quantidade vem positiva; o
/// sentido vem de <see cref="Type"/>. Só o saldo de abertura informa custo, que entra no custo médio (L5).
/// </summary>
public class ManualMovementRequestDto
{
    public Guid VariantId { get; set; }
    public Guid LocationId { get; set; }

    /// <summary>Recusado até a F1.7 (lotes).</summary>
    public Guid? LotId { get; set; }

    /// <summary>Recusado até a F1.7 (lotes).</summary>
    public ManualMovementLotDto? NewLot { get; set; }

    public MovementTypeEnum Type { get; set; }
    public MovementReasonEnum Reason { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitCost { get; set; }
    public string? Note { get; set; }
}
