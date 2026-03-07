using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Interfaces.UseCases;
using Nextflow.Domain.Interfaces.UseCases.Base;
using Nextflow.Domain.Models;
using Nextflow.Application.Utils;
using Nextflow.Utils;

namespace Nextflow.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class StockMovementsController(
    ICreateStockMovementUseCase createUseCase,
    IDeleteUseCase<StockMovement> deleteUseCase,
    IGetAllUseCase<StockMovement, StockMovementResponseDto> getAllStockMovementsUseCase,
    IGetByIdUseCase<StockMovementResponseDto> getStockMovementByIdUseCase
) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateStockMovementDto dto, CancellationToken ct)
    {
        dto.UserId = TokenHelper.GetUserId(this.User);

        var entity = await createUseCase.Execute(dto, ct);

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken ct)
    {
        await deleteUseCase.Execute(id, ct);

        return NoContent();
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int offset = 0, [FromQuery] int limit = 10, [FromQuery] string? filters = null, CancellationToken ct = default)
    {
        var filtersDict = FilterHelper.EnsureDefault(FilterHelper.Parse(filters), "isActive", "true");
        return Ok(new ApiResponse<ApiResponseTable<StockMovementResponseDto>>
        {
            Status = 200,
            Message = "Movimentações de estoque encontradas com sucesso.",
            Data = await getAllStockMovementsUseCase.Execute(offset, limit, filtersDict, ct)
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct)
    {
        return Ok(new ApiResponse<StockMovementResponseDto>
        {
            Status = 200,
            Message = "Movimentação de estoque encontrada com sucesso.",
            Data = await getStockMovementByIdUseCase.Execute(id, ct)
        });
    }
}