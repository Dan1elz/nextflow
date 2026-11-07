using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Orders;
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

/// <summary>Venda rápida de balcão (T-VD2): pedido já Pago, com baixa pelo livro-razão, numa chamada só.</summary>
public interface ICreateQuickSaleUseCase
{
    Task<ResultDto<OrderDetailDto>> Execute(QuickSaleDto? dto, Guid userId, CancellationToken ct = default);
}

/// <summary>Prévia da venda rápida: preço efetivo, totais, falta pagar e troco, sem gravar.</summary>
public interface IPreviewQuickSaleUseCase
{
    Task<ResultDto<QuickSalePreviewDto>> Execute(QuickSaleDto? dto, Guid userId, CancellationToken ct = default);
}
