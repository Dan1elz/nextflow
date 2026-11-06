using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nextflow.Domain.Interfaces.Application.UseCases;

namespace Nextflow.Controllers;

/// <summary>Vendas. F2.1: opções do seletor de venda (T-VD1).</summary>
[Authorize]
[Route("api/sales")]
public class SalesController(IGetVariantSaleOptionsUseCase getVariantOptionsUseCase) : ApiController
{
    /// <summary>Variações por nome, SKU ou código de barras, com preço efetivo e disponível no local de venda.</summary>
    [HttpGet("variant-options")]
    public async Task<IActionResult> GetVariantOptions(
        [FromQuery] string? search, [FromQuery] Guid storeId, [FromQuery] Guid? locationId = null,
        [FromQuery] int limit = 20, CancellationToken ct = default)
        => Handle(await getVariantOptionsUseCase.Execute(search, storeId, locationId, limit, ct));
}
