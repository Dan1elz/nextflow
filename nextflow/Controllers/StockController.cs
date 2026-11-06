using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nextflow.Domain.Interfaces.Application.UseCases;

namespace Nextflow.Controllers;

/// <summary>Consultas de saldo fora do produto. F2.1: disponibilidade do aviso de falta (T-VD1a).</summary>
[Authorize]
[Route("api/stock")]
public class StockController(IGetStockAvailabilityUseCase getAvailabilityUseCase) : ApiController
{
    /// <summary>Onde a variação tem disponível, sem o local excluído (o de venda).</summary>
    [HttpGet("availability")]
    public async Task<IActionResult> GetAvailability(
        [FromQuery] Guid variantId, [FromQuery] Guid? excludeLocationId = null, CancellationToken ct = default)
        => Handle(await getAvailabilityUseCase.Execute(variantId, excludeLocationId, ct));
}
