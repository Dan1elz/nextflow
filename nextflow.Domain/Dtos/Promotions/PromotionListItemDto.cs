using Nextflow.Domain.Dtos.Products;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Promotions;

/// <summary>
/// Linha da listagem de promoções (T-VD7). Status calculado pela data de negócio (RN-VD12). As faixas de preço cobrem as
/// variações do escopo (a variação, ou as não removidas do produto); o preço promocional vem do <c>IProductPricingService</c>.
/// </summary>
public class PromotionListItemDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public Guid? VariantId { get; set; }
    /// <summary>Nulo quando a promoção é do produto inteiro ou da variação padrão oculta.</summary>
    public string? VariantLabel { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal MinListPrice { get; set; }
    public decimal MaxListPrice { get; set; }
    public decimal MinPromoPrice { get; set; }
    public decimal MaxPromoPrice { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public Guid? ForecastRecommendationId { get; set; }
    public string? Reason { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public bool BelowCostConfirmed { get; set; }
    public bool IsActive { get; set; }

    public PromotionListItemDto() { }

    public PromotionListItemDto(Promotion entity, IProductPricingService pricing, DateOnly today)
    {
        var listPrices = ScopeOf(entity).Select(v => v.Price).ToList();
        var promoPrices = listPrices.Select(price => pricing.GetPromotionalPrice(price, entity.DiscountPercent)).ToList();

        Id = entity.Id;
        ProductId = entity.ProductId;
        ProductName = entity.Product != null ? entity.Product.Name : string.Empty;
        VariantId = entity.VariantId;
        VariantLabel = entity.Variant != null && ProductVariantResponseDto.LabelOf(entity.Variant) is { Length: > 0 } label ? label : null;
        DiscountPercent = entity.DiscountPercent;
        MinListPrice = listPrices.DefaultIfEmpty(0).Min();
        MaxListPrice = listPrices.DefaultIfEmpty(0).Max();
        MinPromoPrice = promoPrices.DefaultIfEmpty(0).Min();
        MaxPromoPrice = promoPrices.DefaultIfEmpty(0).Max();
        StartDate = entity.StartDate;
        EndDate = entity.EndDate;
        Status = entity.StatusOn(today).GetApiName();
        Source = entity.Source.GetApiName();
        ForecastRecommendationId = entity.ForecastRecommendationId;
        Reason = entity.Reason;
        CreatedByName = entity.CreatedBy != null ? entity.CreatedBy.FullName : string.Empty;
        BelowCostConfirmed = entity.BelowCostConfirmed;
        IsActive = entity.IsActive;
    }

    private static IEnumerable<ProductVariant> ScopeOf(Promotion entity)
        => entity.Variant != null ? [entity.Variant]
            : entity.Product != null ? entity.Product.Variants.Where(v => v.RemovedOn == null)
            : [];
}
