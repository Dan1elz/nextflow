using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nextflow.Domain.Dtos.Stock;
using Nextflow.Domain.Interfaces.Application.UseCases;

namespace Nextflow.Controllers;

/// <summary>Transferências de estoque entre locais (RN-CE62). Sem editar nem excluir: corrigir é transferir de volta.</summary>
[Authorize]
[Route("api/stock-transfers")]
public class StockTransfersController(
    ICreateStockTransferUseCase createUseCase,
    IGetStockTransferUseCase getUseCase) : ApiController
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] StockTransferRequestDto dto, CancellationToken ct)
        => HandleCreated(await createUseCase.Execute(dto, UserId, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => Handle(await getUseCase.Execute(id, ct));
}
