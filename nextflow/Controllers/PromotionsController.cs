using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nextflow.Application.Utils;
using Nextflow.Attributes;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.UseCases;
using Nextflow.Domain.Interfaces.UseCases.Base;
using Nextflow.Domain.Models;
using Nextflow.Dtos;
using Nextflow.Utils;

namespace Nextflow.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class PromotionsController(
    ICreatePromotionUseCase createUseCase,
    IDeleteUseCase<Promotion> deleteUseCase,
    IGetAllUseCase<Promotion, PromotionResponseDto> getAllUseCase,
    IGetByIdUseCase<PromotionResponseDto> getByIdUseCase
) : ControllerBase
{
    [HttpPost]
    [RoleAuthorize(RoleEnum.Admin)]
    public async Task<IActionResult> Create([FromBody] PromotionRequestDto request, CancellationToken ct)
    {
        var dto = new CreatePromotionDto
        {
            ProductId = request.ProductId,
            DiscountPercent = request.DiscountPercent,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Reason = request.Reason,
            Source = PromotionSource.Manual,
            ForecastRecommendationId = null,
            CreatedByUserId = TokenHelper.GetUserId(User)
        };

        var result = await createUseCase.Execute(dto, ct);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, new ApiResponse<PromotionResponseDto>
        {
            Status = 201,
            Message = "Promoção criada com sucesso.",
            Data = result
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int offset = 0, [FromQuery] int limit = 10, [FromQuery] string? filters = null, CancellationToken ct = default)
    {
        var filtersDict = FilterHelper.Parse(filters);
        return Ok(new ApiResponse<ApiResponseTable<PromotionResponseDto>>
        {
            Status = 200,
            Message = "Promoções encontradas com sucesso.",
            Data = await getAllUseCase.Execute(offset, limit, filtersDict, ct)
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct)
    {
        return Ok(new ApiResponse<PromotionResponseDto>
        {
            Status = 200,
            Message = "Promoção encontrada com sucesso.",
            Data = await getByIdUseCase.Execute(id, ct)
        });
    }

    /// <summary>Cancela a promoção (IsActive = false).</summary>
    [HttpDelete("{id:guid}")]
    [RoleAuthorize(RoleEnum.Admin)]
    public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken ct)
    {
        await deleteUseCase.Execute(id, ct, TokenHelper.GetUserId(User));
        return NoContent();
    }
}
