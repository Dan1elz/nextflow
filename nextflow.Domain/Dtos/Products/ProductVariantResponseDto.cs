using Nextflow.Domain.Dtos.Promotions;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Products;

/// <summary>
/// Linha da aba Variações (T3). <see cref="Label"/> é "P / Azul", na ordem da grade, e vazio na variação padrão oculta.
/// Disponível é o físico da empresa inteira enquanto não houver orçamento (RN-CE08); <see cref="ActivePromotion"/> é a promoção vigente (F2.3).
/// </summary>
public class ProductVariantResponseDto
{
    public Guid Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public decimal Price { get; set; }
    public decimal EffectivePrice { get; set; }
    public PromotionSummaryDto? ActivePromotion { get; set; }
    public decimal? AverageCost { get; set; }
    public decimal? LastCost { get; set; }
    public decimal? MarginPercent { get; set; }
    public decimal AvailableQuantity { get; set; }
    public bool IsActive { get; set; }

    public ProductVariantResponseDto() { }

    public ProductVariantResponseDto(ProductVariant entity, IProductPricingService pricing)
    {
        Id = entity.Id;
        Label = LabelOf(entity);
        Sku = entity.Sku;
        Barcode = entity.Barcode;
        Price = entity.Price;
        EffectivePrice = pricing.GetEffectivePrice(entity);
        ActivePromotion = pricing.GetActivePromotion(entity) is { } promotion ? new PromotionSummaryDto(promotion) : null;
        AverageCost = entity.AverageCost;
        LastCost = entity.LastCost;
        MarginPercent = AverageCost is { } cost && EffectivePrice > 0
            ? Math.Round((EffectivePrice - cost) / EffectivePrice * 100, 2, MidpointRounding.AwayFromZero)
            : null;
        AvailableQuantity = entity.Balances.Sum(b => b.Quantity);
        IsActive = entity.IsActive;
    }

    /// <summary>"P / Azul", na ordem da grade; vazio na variação padrão oculta. Usa os valores e atributos já carregados.</summary>
    public static string LabelOf(ProductVariant entity)
        => string.Join(" / ", entity.AttributeValues
            .Where(link => link.AttributeValue != null)
            .OrderBy(link => link.AttributeValue.Attribute != null ? link.AttributeValue.Attribute.SortOrder : short.MaxValue)
            .Select(link => link.AttributeValue.Value));
}
