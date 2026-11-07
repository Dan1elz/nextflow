using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nextflow.Domain.Interfaces.Application.UseCases;

namespace Nextflow.Controllers;

/// <summary>Consultas de saldo fora do produto. F2.1: disponibilidade do aviso de falta (T-VD1a); F1.7: lotes do ajuste.</summary>
[Authorize]
[Route("api/stock")]
public class StockController(
    IGetStockAvailabilityUseCase getAvailabilityUseCase,
    IGetLotOptionsUseCase getLotOptionsUseCase) : ApiController
{
    /// <summary>Onde a variação tem disponível, sem o local excluído (o de venda).</summary>
    [HttpGet("availability")]
    public async Task<IActionResult> GetAvailability(
        [FromQuery] Guid variantId, [FromQuery] Guid? excludeLocationId = null, CancellationToken ct = default)
        => Handle(await getAvailabilityUseCase.Execute(variantId, excludeLocationId, ct));

    /// <summary>Lotes da variação com o saldo no local (ajuste manual, F1.7).</summary>
    [HttpGet("lots/options")]
    public async Task<IActionResult> GetLotOptions([FromQuery] Guid variantId, [FromQuery] Guid locationId, CancellationToken ct = default)
        => Handle(await getLotOptionsUseCase.Execute(variantId, locationId, ct));
}
