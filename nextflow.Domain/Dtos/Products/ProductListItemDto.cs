using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Products;

/// <summary>
/// Linha da listagem (T1), uma por produto. <see cref="AvailableQuantity"/> é o físico na loja do filtro (ou na empresa),
/// enquanto não houver orçamento (RN-CE08). <see cref="HasLowStock"/>: alguma variação abaixo do mínimo efetivo em algum
/// local do mesmo recorte (RN-CE33);
/// <see cref="ActivePromotionPercent"/> é o maior desconto vigente entre as variações não removidas (F2.3).
/// </summary>
public class ProductListItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? ImageUrl { get; set; }
    public string ProductType { get; set; } = string.Empty;
    public string Lifecycle { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? CategoryPath { get; set; }
    public bool HasVariants { get; set; }
    public int VariantCount { get; set; }
    public decimal MinEffectivePrice { get; set; }
    public decimal MaxEffectivePrice { get; set; }
    public decimal? ActivePromotionPercent { get; set; }
    public decimal AvailableQuantity { get; set; }
    public string SaleUnit { get; set; } = string.Empty;
    public short DecimalPlaces { get; set; }
    public bool HasLowStock { get; set; }

    public ProductListItemDto() { }

    public ProductListItemDto(Product entity, string? imageUrl, IProductPricingService pricing, decimal availableQuantity = 0, bool hasLowStock = false)
    {
        var prices = entity.Variants.Where(v => v.IsActive).Select(pricing.GetEffectivePrice).ToList();

        Id = entity.Id;
        Name = entity.Name;
        Brand = entity.Brand;
        ImageUrl = imageUrl;
        ProductType = entity.ProductType.GetApiName();
        Lifecycle = entity.Lifecycle.GetApiName();
        IsActive = entity.IsActive;
        CategoryPath = MainCategoryPath(entity);
        HasVariants = entity.HasVariants;
        VariantCount = prices.Count;
        MinEffectivePrice = prices.DefaultIfEmpty(0).Min();
        MaxEffectivePrice = prices.DefaultIfEmpty(0).Max();
        AvailableQuantity = availableQuantity;
        SaleUnit = entity.SaleUnit.GetApiName();
        DecimalPlaces = entity.DecimalPlaces;
        HasLowStock = hasLowStock;
        ActivePromotionPercent = ActivePercentOf(entity, pricing);
    }

    /// <summary>Maior desconto vigente entre as variações não removidas; nulo sem promoção (RN-VD12).</summary>
    public static decimal? ActivePercentOf(Product entity, IProductPricingService pricing)
        => entity.Variants
            .Where(v => v.RemovedOn == null)
            .Select(v => pricing.GetActivePromotion(v)?.DiscountPercent)
            .Max();

    /// <summary>RN-CE52: categoria principal é a mais específica; entre iguais, a primeira pelo caminho.</summary>
    private static string? MainCategoryPath(Product entity)
        => entity.Categories
            .Where(link => link.Category != null)
            .Select(link => link.Category)
            .OrderBy(category => category.ParentId is null)
            .ThenBy(Path, StringComparer.OrdinalIgnoreCase)
            .Select(Path)
            .FirstOrDefault();

    private static string Path(Category category)
        => category.Parent is null ? category.Name : $"{category.Parent.Name} › {category.Name}";
}
