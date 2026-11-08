using Nextflow.Domain.Dtos.DerivedData;

namespace Nextflow.Domain.Models;

/// <summary>
/// Saldo de fechamento de um dia por variação × local, reconstruído das movimentações (F3.1). Zero = dia sem estoque. Derivada:
/// o livro-razão continua sendo a fonte e só o <c>IStockLedger</c> grava saldo.
/// </summary>
public class StockDailySnapshot : BaseModel
{
    public Guid VariantId { get; private set; }
    public Guid LocationId { get; private set; }
    public DateOnly Day { get; private set; }
    public decimal ClosingQuantity { get; private set; }

    public virtual ProductVariant Variant { get; private set; } = null!;
    public virtual StockLocation Location { get; private set; } = null!;

    public override string Preposition => "o";
    public override string Singular => "saldo diário";
    public override string Plural => "saldos diários";

    private StockDailySnapshot() { }

    public StockDailySnapshot(StockDailySnapshotDto dto)
    {
        VariantId = dto.VariantId;
        LocationId = dto.LocationId;
        Day = dto.Day;
        ClosingQuantity = dto.ClosingQuantity;
    }
}
