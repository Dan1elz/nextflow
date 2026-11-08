namespace Nextflow.Domain.Models;

/// <summary>
/// Transferência de estoque entre locais da empresa (RN-CE62): saída na origem e entrada no destino no mesmo commit, pelo
/// livro-razão. Num passo só (sem "em trânsito"). Não muda depois de gravada: corrigir é transferir de volta.
/// </summary>
public class StockTransfer : BaseModel
{
    public int Number { get; private set; }
    public Guid FromLocationId { get; private set; }
    public Guid ToLocationId { get; private set; }
    public string? Note { get; private set; }
    public Guid CreatedById { get; private set; }
    /// <summary>Recomendação "Transferir" da previsão que gerou a transferência (F3.4b); nula na transferência manual.</summary>
    public Guid? ForecastRecommendationId { get; private set; }

    public virtual StockLocation FromLocation { get; private set; } = null!;
    public virtual StockLocation ToLocation { get; private set; } = null!;
    public virtual User CreatedBy { get; private set; } = null!;
    public virtual ICollection<StockTransferItem> Items { get; private set; } = [];

    public override string Preposition => "a";
    public override string Singular => "transferência";
    public override string Plural => "transferências";

    private StockTransfer() { }

    public StockTransfer(Guid fromLocationId, Guid toLocationId, string? note, Guid createdById, Guid? forecastRecommendationId,
        IEnumerable<(Guid VariantId, decimal Quantity)> items)
    {
        FromLocationId = fromLocationId;
        ToLocationId = toLocationId;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        CreatedById = createdById;
        ForecastRecommendationId = forecastRecommendationId;
        foreach (var (variantId, quantity) in items)
            Items.Add(new StockTransferItem(Id, variantId, quantity));
    }
}

/// <summary>Variação e quantidade transferidas; os lotes ficam nas movimentações (a saída é por FEFO).</summary>
public class StockTransferItem : BaseModel
{
    public Guid TransferId { get; private set; }
    public Guid VariantId { get; private set; }
    public decimal Quantity { get; private set; }

    public virtual StockTransfer Transfer { get; private set; } = null!;
    public virtual ProductVariant Variant { get; private set; } = null!;

    public override string Preposition => "o";
    public override string Singular => "item da transferência";
    public override string Plural => "itens da transferência";

    private StockTransferItem() { }

    public StockTransferItem(Guid transferId, Guid variantId, decimal quantity)
    {
        TransferId = transferId;
        VariantId = variantId;
        Quantity = quantity;
    }
}
