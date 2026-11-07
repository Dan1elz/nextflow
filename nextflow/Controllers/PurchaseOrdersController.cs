using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nextflow.Domain.Dtos.PurchaseOrders;
using Nextflow.Domain.Interfaces.Application.UseCases;
using Nextflow.Domain.Interfaces.Application.UseCases.Base;
using Nextflow.Domain.Models;
using Nextflow.Utils;

namespace Nextflow.Controllers;

/// <summary>Pedido de compra (T-CP1 a T-CP5).</summary>
[Authorize]
[Route("api/purchase-orders")]
public class PurchaseOrdersController(
    IGetPurchaseOrdersUseCase getAllUseCase,
    IGetByIdUseCase<PurchaseOrder, PurchaseOrderDetailDto> getByIdUseCase,
    IGetPurchaseOrderFormUseCase getFormUseCase,
    IGetPurchaseItemSuggestionUseCase getItemSuggestionUseCase,
    IGetPurchaseVariantOptionsUseCase getVariantOptionsUseCase,
    ICreateUseCase<SavePurchaseOrderDto, PurchaseOrderDetailDto> createUseCase,
    IUpdateUseCase<SavePurchaseOrderDto, PurchaseOrderDetailDto> updateUseCase,
    ISendPurchaseOrderUseCase sendUseCase,
    ICancelPurchaseOrderUseCase cancelUseCase,
    IReceivePurchaseOrderUseCase receiveUseCase) : ApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int offset = 0, [FromQuery] int limit = 20, [FromQuery] string? filters = null,
        [FromQuery] string? sort = null, [FromQuery] string? order = null, [FromQuery] Guid? storeId = null,
        CancellationToken ct = default)
        => Handle(await getAllUseCase.Execute(offset, limit, FilterHelper.Parse(filters), sort, order, storeId, ct));

    [HttpGet("variant-options")]
    public async Task<IActionResult> VariantOptions(
        [FromQuery] Guid supplierId, [FromQuery] string? search = null, [FromQuery] bool onlyLinked = true,
        [FromQuery] int limit = 20, CancellationToken ct = default)
        => Handle(await getVariantOptionsUseCase.Execute(supplierId, search, onlyLinked, limit, ct));

    [HttpGet("item-suggestion")]
    public async Task<IActionResult> ItemSuggestion(
        [FromQuery] Guid supplierId, [FromQuery] Guid variantId, [FromQuery] Guid storeId, CancellationToken ct)
        => Handle(await getItemSuggestionUseCase.Execute(supplierId, variantId, storeId, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => Handle(await getByIdUseCase.Execute(id, ct));

    [HttpGet("{id:guid}/form")]
    public async Task<IActionResult> GetForm(Guid id, CancellationToken ct)
        => Handle(await getFormUseCase.Execute(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SavePurchaseOrderDto dto, CancellationToken ct)
        => HandleCreated(await createUseCase.Execute(dto, UserId, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SavePurchaseOrderDto dto, CancellationToken ct)
        => Handle(await updateUseCase.Execute(id, dto, UserId, ct));

    [HttpPost("{id:guid}/send")]
    public async Task<IActionResult> Send(Guid id, [FromBody] PurchaseOrderVersionDto dto, CancellationToken ct)
        => Handle(await sendUseCase.Execute(id, dto, ct));

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelPurchaseOrderDto dto, CancellationToken ct)
        => Handle(await cancelUseCase.Execute(id, dto, ct));

    [HttpPost("{id:guid}/receive")]
    public async Task<IActionResult> Receive(Guid id, [FromBody] ReceivePurchaseOrderDto dto, CancellationToken ct)
        => Handle(await receiveUseCase.Execute(id, dto, UserId, ct));
}
