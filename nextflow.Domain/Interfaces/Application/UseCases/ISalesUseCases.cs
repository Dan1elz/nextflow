using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Sales;

namespace Nextflow.Domain.Interfaces.Application.UseCases;

/// <summary>
/// Seletor de venda (T-VD1): variações por nome, SKU ou código de barras, com preço efetivo e disponível no local de venda
/// da loja (sem <c>locationId</c>, o padrão da loja). Código exato vem primeiro.
/// </summary>
public interface IGetVariantSaleOptionsUseCase
{
    Task<ResultDto<List<VariantSaleOptionDto>>> Execute(
        string? search, Guid storeId, Guid? locationId, int limit, CancellationToken ct = default);
}
