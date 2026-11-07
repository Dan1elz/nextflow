using Nextflow.Domain.Dtos.Stock;
using Nextflow.Domain.Enums;

namespace Nextflow.Domain.Models;

/// <summary>
/// Linha do livro-razão de estoque (ADR 0007). Imutável: nada muda depois de criada e nada é apagado (RN-CE14); correção é
/// um movimento novo. <see cref="Quantity"/> leva o sinal (entrada positiva, saída negativa), para o saldo ser a soma direta
/// das movimentações (RN-CE01).
/// </summary>
public class StockMovement : BaseModel
{
    public Guid VariantId { get; private set; }
    public Guid LocationId { get; private set; }
    public Guid? LotId { get; private set; }
    public MovementTypeEnum Type { get; private set; }
    public MovementReasonEnum Reason { get; private set; }
    public decimal Quantity { get; private set; }

    /// <summary>Entrada: o custo informado. Saída: o custo médio da variação no momento, quando houver.</summary>
    public decimal? UnitCost { get; private set; }

    public decimal BalanceAfter { get; private set; }
    public SourceTypeEnum SourceType { get; private set; }
    public Guid? SourceId { get; private set; }
    public Guid UserId { get; private set; }
    public string? Note { get; private set; }

    public virtual ProductVariant Variant { get; private set; } = null!;
    public virtual StockLocation Location { get; private set; } = null!;
    public virtual Lot? Lot { get; private set; }
    public virtual User User { get; private set; } = null!;

    public override string Preposition => "a";
    public override string Singular => "movimentação";
    public override string Plural => "movimentações";

    private StockMovement() { }

    public StockMovement(StockDocumentDto document, StockLineDto line, decimal? unitCost, decimal balanceAfter)
    {
        VariantId = line.VariantId;
        LocationId = line.LocationId;
        LotId = line.LotId;
        Type = line.Type;
        Reason = line.Reason;
        Quantity = Signed(line);
        UnitCost = unitCost;
        BalanceAfter = balanceAfter;
        SourceType = document.SourceType;
        SourceId = document.SourceId;
        UserId = document.UserId;
        Note = string.IsNullOrWhiteSpace(document.Note) ? null : document.Note.Trim();
    }

    /// <summary>Quantidade com o sinal do sentido: é o que o saldo soma.</summary>
    public static decimal Signed(StockLineDto line) => line.Type == MovementTypeEnum.Out ? -line.Quantity : line.Quantity;
}
