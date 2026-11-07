using Nextflow.Domain.Dtos.Stock;

namespace Nextflow.Domain.Models;

/// <summary>
/// Saldo físico de uma variação num local (e lote, a partir da F1.7). Escrito só pelo <c>IStockLedger</c> (RN-CE14) e sempre
/// igual à soma das movimentações da mesma chave (RN-CE01). <see cref="Version"/> é o token de concorrência otimista: duas
/// gravações no mesmo saldo, uma passa e a outra recebe 409.
/// </summary>
public class StockBalance : BaseModel
{
    public Guid VariantId { get; private set; }
    public Guid LocationId { get; private set; }
    public Guid? LotId { get; private set; }
    public decimal Quantity { get; private set; }
    public int Version { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public virtual ProductVariant Variant { get; private set; } = null!;
    public virtual StockLocation Location { get; private set; } = null!;
    public virtual Lot? Lot { get; private set; }

    public override string Preposition => "o";
    public override string Singular => "saldo";
    public override string Plural => "saldos";

    private StockBalance() { }

    public StockBalance(StockLineDto line)
    {
        VariantId = line.VariantId;
        LocationId = line.LocationId;
        LotId = line.LotId;
    }

    /// <summary>Soma a quantidade com sinal (entrada positiva, saída negativa) e avança a versão.</summary>
    public void Apply(decimal signedQuantity)
    {
        Quantity += signedQuantity;
        Version++;
        UpdatedAt = DateTime.UtcNow;
    }
}
