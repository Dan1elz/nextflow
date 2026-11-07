using Nextflow.Domain.Models;

namespace Nextflow.Domain.Interfaces.Application.Services;

/// <summary>
/// Único lugar que calcula preço efetivo e promocional; o front nunca calcula (RN-VD02). Trabalha sobre o grafo já
/// carregado: quem chama inclui <c>Product.Promotions</c> ativas no <c>GetInclude()</c>, e a data vem do relógio de negócio.
/// </summary>
public interface IProductPricingService
{
    decimal GetEffectivePrice(ProductVariant variant);

    /// <summary>Promoção vigente hoje que vale para a variação (do produto inteiro ou dela), ou nulo (RN-VD12).</summary>
    Promotion? GetActivePromotion(ProductVariant variant);

    /// <summary>Preço de tabela com o desconto, em duas casas, meio para cima.</summary>
    decimal GetPromotionalPrice(decimal listPrice, decimal discountPercent);
}
