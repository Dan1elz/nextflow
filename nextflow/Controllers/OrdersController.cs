using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nextflow.Domain.Dtos.Orders;
using Nextflow.Domain.Interfaces.Application.UseCases;
using Nextflow.Domain.Interfaces.Application.UseCases.Base;
using Nextflow.Domain.Models;
using Nextflow.Utils;

namespace Nextflow.Controllers;

/// <summary>Pedidos e vendas: listagem (T-VD3) e detalhe (T-VD5). Orçamento e pagamento chegam com a F2.5.</summary>
[Authorize]
[Route("api/orders")]
public class OrdersController(
    IGetOrdersUseCase getAllUseCase,
    IGetByIdUseCase<Order, OrderDetailDto> getByIdUseCase) : ApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int offset = 0, [FromQuery] int limit = 20, [FromQuery] string? filters = null,
        [FromQuery] string? sort = null, [FromQuery] string? order = null, [FromQuery] Guid? storeId = null,
        CancellationToken ct = default)
        => Handle(await getAllUseCase.Execute(offset, limit, FilterHelper.Parse(filters), sort, order, storeId, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => Handle(await getByIdUseCase.Execute(id, ct));
}
