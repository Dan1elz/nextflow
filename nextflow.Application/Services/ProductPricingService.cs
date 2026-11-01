using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Models;

namespace Nextflow.Application.Services;

public class ProductPricingService : IProductPricingService
{
    /// <summary>F2.3 entra aqui: a promoção vigente, carregada no <c>GetInclude()</c> de quem chama, reduz o preço (RN-VD02).</summary>
    public decimal GetEffectivePrice(ProductVariant variant) => variant.Price;
}
