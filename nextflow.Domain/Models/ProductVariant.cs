using Nextflow.Domain.Dtos.Products;

namespace Nextflow.Domain.Models;

/// <summary>Unidade de estoque, venda e compra (ADR 0008). Custo médio e último custo entram na F1.5, escritos só pelo estoque.</summary>
public class ProductVariant : BaseModel
{
    public Guid ProductId { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public string? Barcode { get; private set; }
    public decimal Price { get; private set; }
    public short SortOrder { get; private set; }
    public bool IsActive { get; private set; } = true;
    /// <summary>Saiu do formulário: fica inativa e fora das telas de variação, para o histórico de preço não se perder (RN-CE58).</summary>
    public DateOnly? RemovedOn { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public virtual Product Product { get; private set; } = null!;
    public virtual ICollection<ProductVariantValue> AttributeValues { get; private set; } = [];
    public virtual ICollection<PriceHistory> PriceHistory { get; private set; } = [];

    public override string Preposition => "a";
    public override string Singular => "variação";
    public override string Plural => "variações";

    private ProductVariant() { }

    public ProductVariant(ProductVariantRequestDto dto, Guid productId, short sortOrder, Guid changedById, DateOnly today)
    {
        ProductId = productId;
        Apply(dto, sortOrder);
        PriceHistory.Add(new PriceHistory(Id, Price, today, changedById));
    }

    /// <summary>Preço igual não grava nada; preço diferente fecha a faixa aberta e abre outra (RN-CE25, RN-CE59).</summary>
    public void Update(ProductVariantRequestDto dto, short sortOrder, Guid changedById, DateOnly today)
    {
        var previousPrice = Price;
        Apply(dto, sortOrder);
        UpdatedAt = DateTime.UtcNow;

        if (Price == previousPrice)
            return;

        CloseCurrentPrice(today);
        PriceHistory.Add(new PriceHistory(Id, Price, today, changedById));
    }

    /// <summary>Remoção sem apagar: toda variação gravada tem histórico de preço, e histórico não se apaga (RN-CE58).</summary>
    public void Remove(DateOnly today)
    {
        IsActive = false;
        RemovedOn = today;
        UpdatedAt = DateTime.UtcNow;
        CloseCurrentPrice(today);
    }

    public void SyncValues(List<Guid> attributeValueIds)
    {
        foreach (var link in AttributeValues.Where(link => !attributeValueIds.Contains(link.AttributeValueId)).ToList())
            AttributeValues.Remove(link);

        var linked = AttributeValues.Select(link => link.AttributeValueId).ToHashSet();
        foreach (var valueId in attributeValueIds.Where(id => !linked.Contains(id)))
            AttributeValues.Add(new ProductVariantValue(Id, valueId));
    }

    private void CloseCurrentPrice(DateOnly today)
    {
        foreach (var current in PriceHistory.Where(entry => entry.ValidTo is null))
            current.Close(today);
    }

    private void Apply(ProductVariantRequestDto dto, short sortOrder)
    {
        Sku = dto.Sku.Trim();
        Barcode = string.IsNullOrWhiteSpace(dto.Barcode) ? null : dto.Barcode.Trim();
        Price = dto.Price;
        SortOrder = sortOrder;
        IsActive = dto.IsActive;
    }
}
