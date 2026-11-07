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

    /// <summary>Lote (F1.7): obrigatório (existente ou novo) se o produto controla lote; na saída, nulo baixa por FEFO.</summary>
    public Guid? LotId { get; set; }

    /// <summary>Lote novo (código + validade), só na entrada; exclui <see cref="LotId"/>.</summary>
    public ManualMovementLotDto? NewLot { get; set; }

    public MovementTypeEnum Type { get; set; }
    public MovementReasonEnum Reason { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitCost { get; set; }
    public string? Note { get; set; }
}
