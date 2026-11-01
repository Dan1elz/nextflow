using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Products;

/// <summary>
/// Linha da aba Preços (T3). <see cref="VariantLabel"/> é "P / Azul" e vem vazio na variação oculta, ou na removida que perdeu o
/// valor da grade; <see cref="Sku"/> e <see cref="VariantRemoved"/> identificam a linha nesses casos (RN-CE58).
/// </summary>
public class PriceHistoryResponseDto
{
    public Guid Id { get; set; }
    public string VariantLabel { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public bool VariantRemoved { get; set; }
    public decimal Price { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public string ChangedByName { get; set; } = string.Empty;

    public PriceHistoryResponseDto() { }

    public PriceHistoryResponseDto(PriceHistory entity, ProductVariant variant)
    {
        Id = entity.Id;
        VariantLabel = string.Join(" / ", variant.AttributeValues
            .Where(link => link.AttributeValue != null)
            .OrderBy(link => link.AttributeValue.Attribute != null ? link.AttributeValue.Attribute.SortOrder : short.MaxValue)
            .Select(link => link.AttributeValue.Value));
        Sku = variant.Sku;
        VariantRemoved = variant.RemovedOn != null;
        Price = entity.Price;
        ValidFrom = entity.ValidFrom;
        ValidTo = entity.ValidTo;
        ChangedByName = entity.ChangedBy != null ? entity.ChangedBy.FullName : string.Empty;
    }
}
