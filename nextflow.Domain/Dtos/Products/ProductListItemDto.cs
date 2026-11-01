using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Products;

/// <summary>
/// Linha da listagem (T1), uma por produto. Provisório, declarado no contrato: <see cref="AvailableQuantity"/> e
/// <see cref="HasLowStock"/> entram na F1.5; <see cref="ActivePromotionPercent"/>, na F2.3.
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

    public ProductListItemDto(Product entity, string? imageUrl, IProductPricingService pricing)
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
        SaleUnit = entity.SaleUnit.GetApiName();
        DecimalPlaces = entity.DecimalPlaces;
    }

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
