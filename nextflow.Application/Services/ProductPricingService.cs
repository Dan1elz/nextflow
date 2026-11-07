using Nextflow.Domain.Interfaces.Utils;
using Nextflow.Domain.Models;

namespace Nextflow.Application.Services;

public class ProductPricingService : IProductPricingService
{
    public Promotion? GetActivePromotion(Product product, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(product);
        return product.GetActivePromotion(date);
    }

    public decimal GetEffectivePrice(Product product, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(product);
        return GetActivePromotion(product, date)?.CalculatePromotionalPrice(product.Price) ?? product.Price;
    }
}
