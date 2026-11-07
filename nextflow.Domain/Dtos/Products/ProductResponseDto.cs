using Nextflow.Domain.Dtos.Categories;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Products;

/// <summary>
/// Cabeçalho da tela T3. Disponível é o físico da empresa inteira, somado entre as variações ativas (RN-CE08, sem orçamento
/// ainda); custo médio é a faixa entre as variações ativas que já têm custo (RN-CE31). Promoção: maior desconto vigente entre
/// as variações não removidas (F2.3); classe ABC e cobertura alvo, na F1.8.
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
    public List<Option> Events { get; set; } = [];

    public ProductResponseDto() { }

    public ProductResponseDto(Product entity, string? imageUrl, IProductPricingService pricing)
    {
        var active = entity.Variants.Where(v => v.IsActive).ToList();
        var prices = active.Select(pricing.GetEffectivePrice).ToList();
        var costs = active.Where(v => v.AverageCost != null).Select(v => v.AverageCost!.Value).ToList();

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
        ActivePromotionPercent = ProductListItemDto.ActivePercentOf(entity, pricing);
        MinAverageCost = costs.Count > 0 ? costs.Min() : null;
        MaxAverageCost = costs.Count > 0 ? costs.Max() : null;
        AvailableQuantity = entity.Variants.SelectMany(v => v.Balances).Sum(b => b.Quantity);
        VariantCount = prices.Count;
        Events = [.. entity.Events.Where(link => link.Event != null).Select(link => new Option(link.EventId.ToString(), link.Event.Name)).OrderBy(e => e.Label)];
    }
}
