using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Products;

/// <summary>
/// Linha da aba Variações (T3). <see cref="Label"/> é "P / Azul", na ordem da grade, e vazio na variação padrão oculta.
/// Provisório: disponível e custo entram na F1.5; promoção, na F2.3.
/// </summary>
public class ProductVariantResponseDto
{
    public Guid Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public decimal Price { get; set; }
    public decimal EffectivePrice { get; set; }
    public decimal? AverageCost { get; set; }
    public decimal? LastCost { get; set; }
    public decimal? MarginPercent { get; set; }
    public decimal AvailableQuantity { get; set; }
    public bool IsActive { get; set; }

    public ProductVariantResponseDto() { }

    public ProductVariantResponseDto(ProductVariant entity, IProductPricingService pricing)
    {
        Id = entity.Id;
        Label = string.Join(" / ", entity.AttributeValues
            .Where(link => link.AttributeValue != null)
            .OrderBy(link => link.AttributeValue.Attribute != null ? link.AttributeValue.Attribute.SortOrder : short.MaxValue)
            .Select(link => link.AttributeValue.Value));
        Sku = entity.Sku;
        Barcode = entity.Barcode;
        Price = entity.Price;
        EffectivePrice = pricing.GetEffectivePrice(entity);
        IsActive = entity.IsActive;
    }
}
