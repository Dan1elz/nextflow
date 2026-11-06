using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nextflow.Domain.Dtos.Stock;
using Nextflow.Domain.Interfaces.Application.UseCases;
using Nextflow.Utils;

namespace Nextflow.Controllers;

/// <summary>Movimentações de estoque. Sem editar nem excluir (RN-CE14): correção é lançamento contrário.</summary>
[Authorize]
[Route("api/stock-movements")]
public class StockMovementsController(
    ICreateManualMovementUseCase createManualUseCase,
    IGetStockMovementsUseCase getAllUseCase) : ApiController
{
    /// <summary>Kardex (T8): mais recente primeiro.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int offset = 0, [FromQuery] int limit = 20, [FromQuery] string? filters = null, CancellationToken ct = default)
        => Handle(await getAllUseCase.Execute(offset, limit, FilterHelper.Parse(filters), ct));

    /// <summary>Ajuste, perda ou saldo de abertura (T9).</summary>
    [HttpPost("manual")]
    public async Task<IActionResult> CreateManual([FromBody] ManualMovementRequestDto dto, CancellationToken ct)
        => HandleNoContent(await createManualUseCase.Execute(dto, UserId, ct));
}
