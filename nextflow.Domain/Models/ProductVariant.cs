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
    public DateTime? UpdatedAt { get; private set; }

    public virtual Product Product { get; private set; } = null!;
    public virtual ICollection<ProductVariantValue> AttributeValues { get; private set; } = [];

    public override string Preposition => "a";
    public override string Singular => "variação";
    public override string Plural => "variações";

    private ProductVariant() { }

    public ProductVariant(ProductVariantRequestDto dto, Guid productId, short sortOrder)
    {
        ProductId = productId;
        Apply(dto, sortOrder);
    }

    /// <summary>A F1.4 grava <c>price_history</c> quando o preço mudar (RN-CE25).</summary>
    public void Update(ProductVariantRequestDto dto, short sortOrder)
    {
        Apply(dto, sortOrder);
        UpdatedAt = DateTime.UtcNow;
    }

    public void SyncValues(List<Guid> attributeValueIds)
    {
        foreach (var link in AttributeValues.Where(link => !attributeValueIds.Contains(link.AttributeValueId)).ToList())
            AttributeValues.Remove(link);

        var linked = AttributeValues.Select(link => link.AttributeValueId).ToHashSet();
        foreach (var valueId in attributeValueIds.Where(id => !linked.Contains(id)))
            AttributeValues.Add(new ProductVariantValue(Id, valueId));
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
