using Nextflow.Domain.Models;

namespace Nextflow.Domain.Interfaces.Utils;

/// <summary>
/// Cálculo do preço efetivo de venda (com promoção vigente).
/// Usa as promoções já carregadas em <c>Product.Promotions</c> — carregue-as com
/// <c>IncludeCurrentPromotions(BusinessClock.Today)</c> antes de chamar.
/// </summary>
public interface IProductPricingService
{
    decimal GetEffectivePrice(Product product, DateOnly date);
    Promotion? GetActivePromotion(Product product, DateOnly date);
}
