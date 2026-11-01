using Nextflow.Domain.Dtos.Categories;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Products;

/// <summary>
/// Cabeçalho da tela T3. Provisório, declarado no contrato: disponível e custo entram na F1.5; promoção, na F2.3. Eventos
/// (F1.4), classe ABC e cobertura alvo (F1.8) ainda não existem e ficam fora.
/// </summary>
public class ProductResponseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public string ProductType { get; set; } = string.Empty;
    public string Lifecycle { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateOnly? LaunchedOn { get; set; }
    public List<CategoryOptionDto> Categories { get; set; } = [];
    public Option? Collection { get; set; }
    public string SaleUnit { get; set; } = string.Empty;
    public short DecimalPlaces { get; set; }
    public bool AllowsNegative { get; set; }
    public bool TracksLots { get; set; }
    public int? ShelfLifeDays { get; set; }
    public bool HasVariants { get; set; }
    public decimal? MinAverageCost { get; set; }
    public decimal? MaxAverageCost { get; set; }
    public decimal MinEffectivePrice { get; set; }
    public decimal MaxEffectivePrice { get; set; }
    public decimal? ActivePromotionPercent { get; set; }
    public decimal AvailableQuantity { get; set; }
    public int VariantCount { get; set; }

    public ProductResponseDto() { }

    public ProductResponseDto(Product entity, string? imageUrl, IProductPricingService pricing)
    {
        var prices = entity.Variants.Where(v => v.IsActive).Select(pricing.GetEffectivePrice).ToList();

        Id = entity.Id;
        Name = entity.Name;
        Brand = entity.Brand;
        Description = entity.Description;
        ImageUrl = imageUrl;
        ProductType = entity.ProductType.GetApiName();
        Lifecycle = entity.Lifecycle.GetApiName();
        IsActive = entity.IsActive;
        LaunchedOn = entity.LaunchedOn;
        Categories = [.. entity.Categories.Where(link => link.Category != null).Select(link => new CategoryOptionDto(link.Category)).OrderBy(c => c.Path)];
        Collection = entity.Collection != null ? new Option(entity.Collection.Id.ToString(), entity.Collection.Name) : null;
        SaleUnit = entity.SaleUnit.GetApiName();
        DecimalPlaces = entity.DecimalPlaces;
        AllowsNegative = entity.AllowsNegative;
        TracksLots = entity.TracksLots;
        ShelfLifeDays = entity.ShelfLifeDays;
        HasVariants = entity.HasVariants;
        MinEffectivePrice = prices.DefaultIfEmpty(0).Min();
        MaxEffectivePrice = prices.DefaultIfEmpty(0).Max();
        VariantCount = prices.Count;
    }
}
