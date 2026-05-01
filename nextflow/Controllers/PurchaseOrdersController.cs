using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nextflow.Application.Utils;
using Nextflow.Attributes;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.UseCases;
using Nextflow.Domain.Models;
using Nextflow.Domain.Interfaces.UseCases.Base;
using Nextflow.Utils;

namespace Nextflow.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class PurchaseOrdersController(
    ICreateUseCase<CreatePurchaseOrderDto, PurchaseOrderResponseDto> createUseCase,
    IGetAllUseCase<PurchaseOrder, PurchaseOrderResponseDto> getAllUseCase,
    IGetByIdUseCase<PurchaseOrderResponseDto> getByIdUseCase,
    IUpdateUseCase<UpdatePurchaseOrderDto, PurchaseOrderResponseDto> updateUseCase,
    IDeleteUseCase<PurchaseOrder> deleteUseCase
) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePurchaseOrderDto dto, CancellationToken ct)
    {
        var result = await createUseCase.Execute(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int offset = 0, [FromQuery] int limit = 10, [FromQuery] string? filters = null, CancellationToken ct = default)
    {
        var filtersDict = FilterHelper.Parse(filters);
        var result = await getAllUseCase.Execute(offset, limit, filtersDict, ct);
        return Ok(new ApiResponse<ApiResponseTable<PurchaseOrderResponseDto>>
        {
            Status = 200,
            Message = "Pedidos de compra recuperados com sucesso.",
            Data = result
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct)
    {
        var result = await getByIdUseCase.Execute(id, ct);
        return Ok(new ApiResponse<PurchaseOrderResponseDto>
        {
            Status = 200,
            Message = "Pedido de compra recuperado com sucesso.",
            Data = result
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdatePurchaseOrderDto dto, CancellationToken ct)
    {
        dto.UserId = TokenHelper.GetUserId(User);
        var result = await updateUseCase.Execute(id, dto, ct);
        return Ok(new ApiResponse<PurchaseOrderResponseDto>
        {
            Status = 200,
            Message = "Pedido de compra atualizado com sucesso.",
            Data = result
        });
    }

    [HttpDelete("{id:guid}")]
    [RoleAuthorize(RoleEnum.Admin)]
    public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken ct)
    {
        await deleteUseCase.Execute(id, ct);
        return Ok(new ApiResponse<string>
        {
            Status = 200,
            Message = "Pedido de compra excluído com sucesso.",
            Data = string.Empty
        });
    }
}
