using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Models;

namespace Nextflow.Application.Services;

/// <summary>
/// Preço efetivo = preço de tabela com a promoção vigente, se houver (RN-VD02, RN-VD12). Pela RN-VD11 há no máximo uma
/// vigente por variação; se o banco tiver duas, vale o maior desconto.
/// </summary>
public class ProductPricingService(IBusinessClock clock) : IProductPricingService
{
    public decimal GetEffectivePrice(ProductVariant variant)
        => GetActivePromotion(variant) is { } promotion
            ? GetPromotionalPrice(variant.Price, promotion.DiscountPercent)
            : variant.Price;

    public Promotion? GetActivePromotion(ProductVariant variant)
    {
        if (variant.Product == null)
            return null;

        var today = clock.Today;
        return variant.Product.Promotions
            .Where(p => p.AppliesTo(variant.Id) && p.IsCurrentOn(today))
            .OrderByDescending(p => p.DiscountPercent)
            .FirstOrDefault();
    }

    public decimal GetPromotionalPrice(decimal listPrice, decimal discountPercent)
        => Math.Round(listPrice * (100m - discountPercent) / 100m, 2, MidpointRounding.AwayFromZero);
}
