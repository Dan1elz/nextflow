using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nextflow.Application.Utils;
using Nextflow.Attributes;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.UseCases;
using Nextflow.Domain.Interfaces.UseCases.Base;
using Nextflow.Domain.Models;

namespace Nextflow.Controllers;

/// <summary>Previsão de demanda com IA (spec <c>docs/AI_FORECAST_SPEC.md</c>, seção 7.1).</summary>
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ForecastsController(
    IGenerateForecastUseCase generateUseCase,
    IGetLatestForecastUseCase getLatestUseCase,
    IGetAllUseCase<ForecastRun, ForecastRunSummaryDto> getAllUseCase,
    IGetByIdUseCase<ForecastRunDto> getByIdUseCase,
    IGetProductForecastUseCase getProductForecastUseCase,
    IApplyPurchaseRecommendationsUseCase applyPurchaseUseCase,
    IApplyPromotionRecommendationUseCase applyPromotionUseCase,
    IDismissRecommendationUseCase dismissUseCase
) : ControllerBase
{
    /// <summary>Gera uma nova análise (cálculo + IA). Pode levar até ~60s.</summary>
    [HttpPost("generate")]
    [RoleAuthorize(RoleEnum.Admin)]
    public async Task<IActionResult> Generate([FromBody] GenerateForecastRequestDto dto, CancellationToken ct)
    {
        var result = await generateUseCase.Execute(TokenHelper.GetUserId(User), dto, ct);
        return Ok(new ApiResponse<ForecastRunDto>
        {
            Status = 200,
            Message = result.Status switch
            {
                ForecastRunStatus.Completed => "Análise de previsão gerada com sucesso.",
                ForecastRunStatus.CompletedWithoutAi => "Análise de previsão gerada sem IA (apenas cálculo estatístico).",
                _ => "Não foi possível gerar a análise de previsão."
            },
            Data = result
        });
    }

    /// <summary>Última análise (data = null quando nunca houve uma).</summary>
    [HttpGet("latest")]
    public async Task<IActionResult> GetLatest(CancellationToken ct)
    {
        var result = await getLatestUseCase.Execute(ct);
        return Ok(new ApiResponse<ForecastRunDto?>
        {
            Status = 200,
            Message = result != null ? "Última análise de previsão encontrada com sucesso." : "Nenhuma análise de previsão foi gerada ainda.",
            Data = result
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int offset = 0, [FromQuery] int limit = 10, CancellationToken ct = default)
    {
        return Ok(new ApiResponse<ApiResponseTable<ForecastRunSummaryDto>>
        {
            Status = 200,
            Message = "Análises de previsão encontradas com sucesso.",
            Data = await getAllUseCase.Execute(Math.Max(0, offset), Math.Clamp(limit, 1, 100), null, ct)
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct)
    {
        return Ok(new ApiResponse<ForecastRunDto>
        {
            Status = 200,
            Message = "Análise de previsão encontrada com sucesso.",
            Data = await getByIdUseCase.Execute(id, ct)
        });
    }

    [HttpGet("products/{productId:guid}")]
    public async Task<IActionResult> GetProductForecast([FromRoute] Guid productId, [FromQuery] int months = 24, CancellationToken ct = default)
    {
        return Ok(new ApiResponse<ProductForecastDto>
        {
            Status = 200,
            Message = "Previsão do produto encontrada com sucesso.",
            Data = await getProductForecastUseCase.Execute(productId, months, ct)
        });
    }

    /// <summary>Aplica recomendações "Comprar": 1 pedido de compra (Orçamento) por fornecedor.</summary>
    [HttpPost("recommendations/purchase")]
    public async Task<IActionResult> ApplyPurchase([FromBody] ApplyPurchaseRequestDto dto, CancellationToken ct)
    {
        var result = await applyPurchaseUseCase.Execute(TokenHelper.GetUserId(User), dto, ct);
        return Ok(new ApiResponse<ApplyPurchaseResultDto>
        {
            Status = 200,
            Message = result.PurchaseOrders.Count == 1
                ? "Pedido de compra gerado com sucesso."
                : $"{result.PurchaseOrders.Count} pedidos de compra gerados com sucesso.",
            Data = result
        });
    }

    /// <summary>Aplica uma recomendação "Promover" criando a promoção.</summary>
    [HttpPost("recommendations/{id:guid}/promotion")]
    [RoleAuthorize(RoleEnum.Admin)]
    public async Task<IActionResult> ApplyPromotion([FromRoute] Guid id, [FromBody] ApplyPromotionRequestDto dto, CancellationToken ct)
    {
        return Ok(new ApiResponse<ForecastRecommendationDto>
        {
            Status = 200,
            Message = "Promoção criada a partir da recomendação com sucesso.",
            Data = await applyPromotionUseCase.Execute(TokenHelper.GetUserId(User), id, dto, ct)
        });
    }

    /// <summary>Descarta uma recomendação pendente. Body opcional: <c>{ "reason": "..." }</c>.</summary>
    [HttpPost("recommendations/{id:guid}/dismiss")]
    public async Task<IActionResult> Dismiss(
        [FromRoute] Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] DismissRecommendationRequestDto? dto,
        CancellationToken ct)
    {
        return Ok(new ApiResponse<ForecastRecommendationDto>
        {
            Status = 200,
            Message = "Recomendação descartada com sucesso.",
            Data = await dismissUseCase.Execute(id, dto?.Reason, ct)
        });
    }
}
