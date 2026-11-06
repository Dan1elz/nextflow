namespace Nextflow.Domain.Models;

/// <summary>
/// Mínimo de uma variação num local (RN-CE33). Sem linha para o local, vale o mínimo padrão do produto
/// (<see cref="Product.DefaultMinQuantity"/>). Gravado pelo formulário do produto (T2), que substitui a tabela inteira.
/// </summary>
public class StockLocationMinimum : BaseModel
{
    public Guid VariantId { get; private set; }
    public Guid LocationId { get; private set; }
    public decimal MinQuantity { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public virtual ProductVariant Variant { get; private set; } = null!;
    public virtual StockLocation Location { get; private set; } = null!;

    public override string Preposition => "o";
    public override string Singular => "mínimo por local";
    public override string Plural => "mínimos por local";

    private StockLocationMinimum() { }

    public StockLocationMinimum(Guid variantId, Guid locationId, decimal minQuantity)
    {
        VariantId = variantId;
        LocationId = locationId;
        MinQuantity = minQuantity;
    }

    public void Update(decimal minQuantity)
    {
        if (MinQuantity == minQuantity)
            return;

        MinQuantity = minQuantity;
        UpdatedAt = DateTime.UtcNow;
    }
}
