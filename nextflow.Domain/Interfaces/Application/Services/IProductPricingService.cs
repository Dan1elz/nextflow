using Nextflow.Domain.Models;

namespace Nextflow.Domain.Interfaces.Application.Services;

/// <summary>Único lugar que calcula preço efetivo; o front nunca calcula (RN-VD02). Trabalha sobre o grafo já carregado.</summary>
public interface IProductPricingService
{
    decimal GetEffectivePrice(ProductVariant variant);
}
