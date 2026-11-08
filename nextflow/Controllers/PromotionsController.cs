using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nextflow.Domain.Dtos.Promotions;
using Nextflow.Domain.Interfaces.Application.UseCases;
using Nextflow.Domain.Interfaces.Application.UseCases.Base;
using Nextflow.Domain.Models;
using Nextflow.Utils;

namespace Nextflow.Controllers;

/// <summary>Promoções (T-VD7, F2.3). Cancelar é inativar (RN-VD12); só a agendada se edita; não há reativar.</summary>
[Authorize]
[Route("api/promotions")]
public class PromotionsController(
    IGetAllUseCase<Promotion, PromotionListItemDto> getAllUseCase,
    IGetPromotionPreviewUseCase previewUseCase,
    ICreateUseCase<PromotionRequestDto, PromotionListItemDto> createUseCase,
    IUpdatePromotionUseCase updateUseCase,
    ICancelPromotionUseCase cancelUseCase) : ApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int offset = 0, [FromQuery] int limit = 20, [FromQuery] string? filters = null, CancellationToken ct = default)
        => Handle(await getAllUseCase.Execute(offset, limit, FilterHelper.Parse(filters), ct: ct));

    [HttpGet("preview")]
    public async Task<IActionResult> Preview(
        [FromQuery] Guid productId, [FromQuery] Guid? variantId, [FromQuery] decimal discountPercent, CancellationToken ct = default)
        => Handle(await previewUseCase.Execute(productId, variantId, discountPercent, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PromotionRequestDto dto, CancellationToken ct)
        => HandleCreated(await createUseCase.Execute(dto, UserId, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] PromotionRequestDto dto, CancellationToken ct)
        => Handle(await updateUseCase.Execute(id, dto, ct));

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
        => HandleNoContent(await cancelUseCase.Execute(id, ct));
}
