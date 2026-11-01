namespace Nextflow.Domain.Models;

/// <summary>
/// Faixa de preço de uma variação (RN-CE25, RN-CE58). Imutável: preço, início e autor nunca mudam e a linha nunca é apagada.
/// A única escrita depois de criada é <see cref="Close"/>, que preenche o fim da faixa uma vez, quando o preço troca.
/// Duas trocas no mesmo dia dão duas linhas, sem perder o autor de nenhuma (RN-CE59).
/// </summary>
public class PriceHistory : BaseModel
{
    public Guid VariantId { get; private set; }
    public decimal Price { get; private set; }
    public DateOnly ValidFrom { get; private set; }
    public DateOnly? ValidTo { get; private set; }
    public Guid ChangedById { get; private set; }

    public virtual ProductVariant Variant { get; private set; } = null!;
    public virtual User ChangedBy { get; private set; } = null!;

    public override string Preposition => "o";
    public override string Singular => "histórico de preço";
    public override string Plural => "históricos de preço";

    private PriceHistory() { }

    public PriceHistory(Guid variantId, decimal price, DateOnly validFrom, Guid changedById)
    {
        VariantId = variantId;
        Price = price;
        ValidFrom = validFrom;
        ChangedById = changedById;
    }

    public void Close(DateOnly validTo)
    {
        if (ValidTo is null)
            ValidTo = validTo;
    }
}
