using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nextflow.Application.Utils;
using Nextflow.Attributes;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.UseCases;
using Nextflow.Domain.Interfaces.UseCases.Base;
using Nextflow.Domain.Models;
using Nextflow.Utils;

namespace Nextflow.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class OrdersController(
    ICreateOrderUseCase createUseCase,
    IUpdateUseCase<UpdateOrderDto, OrderResponseDto> updateUseCase,
    IUpdateStatusByOrderIdUseCase updateStatusUseCase,
    IGetAllUseCase<Order, OrderResponseDto> getAllOrdersUseCase,
    IGetByIdUseCase<OrderResponseDto> getOrderByIdUseCase
) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderDto dto, CancellationToken ct)
    {
        dto.UserId = TokenHelper.GetUserId(this.User);
        var entity = await createUseCase.Execute(dto, ct);

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateOrderDto dto, CancellationToken ct)
    {
        dto.UserId = TokenHelper.GetUserId(this.User);
        return Ok(new ApiResponse<OrderResponseDto>
        {
            Status = 200,
            Message = "Pedido atualizado com sucesso.",
            Data = await updateUseCase.Execute(id, dto, ct)
        });
    }

    [HttpDelete("{id:guid}")]
    [RoleAuthorize(RoleEnum.Admin)]
    public async Task<IActionResult> Delete([FromRoute] Guid id, [FromBody] CancelOrderDto dto, CancellationToken ct)
    {
        var userId = TokenHelper.GetUserId(this.User);
        await updateStatusUseCase.Execute(id, userId, OrderStatus.Canceled, dto.Reason, ct);

        return NoContent();
    }

    [HttpPatch("{id:guid}/refund")]
    [RoleAuthorize(RoleEnum.Admin)]
    public async Task<IActionResult> Refund([FromRoute] Guid id, [FromBody] CancelOrderDto dto, CancellationToken ct)
    {
        var userId = TokenHelper.GetUserId(this.User);
        await updateStatusUseCase.Execute(id, userId, OrderStatus.Refunded, dto.Reason, ct);

        return Ok(new ApiResponse<string>
        {
            Status = 200,
            Message = "Pedido reembolsado com sucesso.",
            Data = ""
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int offset = 0, [FromQuery] int limit = 10, [FromQuery] string? filters = null, CancellationToken ct = default)
    {
        var filtersDict = FilterHelper.EnsureDefault(FilterHelper.Parse(filters), "isActive", "true");
        return Ok(new ApiResponse<ApiResponseTable<OrderResponseDto>>
        {
            Status = 200,
            Message = "Pedidos encontrados com sucesso.",
            Data = await getAllOrdersUseCase.Execute(offset, limit, filtersDict, ct)
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct)
    {
        return Ok(new ApiResponse<OrderResponseDto>
        {
            Status = 200,
            Message = "Pedido encontrado com sucesso.",
            Data = await getOrderByIdUseCase.Execute(id, ct)
        });
    }
}