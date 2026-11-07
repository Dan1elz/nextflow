using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nextflow.Domain.Dtos.Orders;
using Nextflow.Domain.Interfaces.Application.UseCases;

namespace Nextflow.Controllers;

/// <summary>Vendas. F2.1: opções do seletor de venda (T-VD1). F2.4: venda rápida de balcão (T-VD2) e a prévia dela.</summary>
[Authorize]
[Route("api/sales")]
public class SalesController(
    IGetVariantSaleOptionsUseCase getVariantOptionsUseCase,
    ICreateQuickSaleUseCase createQuickSaleUseCase,
    IPreviewQuickSaleUseCase previewQuickSaleUseCase) : ApiController
{
    /// <summary>Variações por nome, SKU ou código de barras, com preço efetivo e disponível no local de venda.</summary>
    [HttpGet("variant-options")]
    public async Task<IActionResult> GetVariantOptions(
        [FromQuery] string? search, [FromQuery] Guid storeId, [FromQuery] Guid? locationId = null,
        [FromQuery] int limit = 20, CancellationToken ct = default)
        => Handle(await getVariantOptionsUseCase.Execute(search, storeId, locationId, limit, ct));

    /// <summary>Conclui a venda de balcão: pedido Pago e baixa do estoque no local de venda.</summary>
    [HttpPost("quick")]
    public async Task<IActionResult> CreateQuick([FromBody] QuickSaleDto dto, CancellationToken ct)
        => HandleCreated(await createQuickSaleUseCase.Execute(dto, UserId, ct));

    /// <summary>Totais, falta pagar e troco do carrinho como a venda cobraria agora; não grava.</summary>
    [HttpPost("quick/preview")]
    public async Task<IActionResult> PreviewQuick([FromBody] QuickSaleDto dto, CancellationToken ct)
        => Handle(await previewQuickSaleUseCase.Execute(dto, UserId, ct));
}
