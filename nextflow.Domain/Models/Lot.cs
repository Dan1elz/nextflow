namespace Nextflow.Domain.Models;

/// <summary>
/// Lote de uma variação (RN-CE10, F1.7): código e validade de uma chegada. O saldo do lote fica em <see cref="StockBalance"/>
/// (variação × local × lote, RN-CE09), escrito só pelo <c>IStockLedger</c>. O código é único por variação: a mesma chegada
/// em dois locais é o mesmo lote. Imutável depois de criado, como o livro-razão que aponta para ele (RN-CE14).
/// </summary>
public class Lot : BaseModel
{
    public const int CodeMaxLength = 40;

    public Guid VariantId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public DateOnly ExpiresOn { get; private set; }

    public virtual ProductVariant Variant { get; private set; } = null!;

    public override string Preposition => "o";
    public override string Singular => "lote";
    public override string Plural => "lotes";

    private Lot() { }

    public Lot(Guid variantId, string code, DateOnly expiresOn)
    {
        VariantId = variantId;
        Code = Normalize(code);
        ExpiresOn = expiresOn;
    }

    /// <summary>Código sem espaços nas pontas e em maiúsculas: "l-01 " e "L-01" são o mesmo lote.</summary>
    public static string Normalize(string code) => code.Trim().ToUpperInvariant();
}
