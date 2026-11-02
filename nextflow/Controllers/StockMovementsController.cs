using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nextflow.Domain.Dtos.Stock;
using Nextflow.Domain.Interfaces.Application.UseCases;

namespace Nextflow.Controllers;

/// <summary>Movimentações de estoque. Sem editar nem excluir (RN-CE14): correção é lançamento contrário.</summary>
[Authorize]
[Route("api/stock-movements")]
public class StockMovementsController(ICreateManualMovementUseCase createManualUseCase) : ApiController
{
    /// <summary>Ajuste, perda ou saldo de abertura (T9).</summary>
    [HttpPost("manual")]
    public async Task<IActionResult> CreateManual([FromBody] ManualMovementRequestDto dto, CancellationToken ct)
        => HandleNoContent(await createManualUseCase.Execute(dto, UserId, ct));
}
