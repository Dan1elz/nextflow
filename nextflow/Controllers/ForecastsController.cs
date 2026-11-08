using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nextflow.Domain.Dtos.Forecasts;
using Nextflow.Domain.Interfaces.Application.UseCases;
using Nextflow.Domain.Interfaces.Application.UseCases.Base;
using Nextflow.Domain.Models;
using Nextflow.Utils;

namespace Nextflow.Controllers;

/// <summary>Previsão de demanda (T-DF2, T-DF9, T-DF10; F3.3): disparo em segundo plano, andamento, histórico, recomendações e precisão.</summary>
[Authorize]
[Route("api/forecasts")]
public class ForecastsController(
    IGenerateForecastUseCase generateUseCase,
    IGetLatestForecastUseCase latestUseCase,
    IGetForecastRunUseCase getRunUseCase,
    IGetForecastProgressUseCase progressUseCase,
    IGetForecastBatchesUseCase batchesUseCase,
    IGetAllUseCase<ForecastRun, ForecastRunSummaryDto> getAllUseCase,
    IGetForecastRecommendationsUseCase recommendationsUseCase,
    IGetForecastRecommendationUseCase recommendationUseCase,
    IGetRecommendationStoreSplitUseCase storeSplitUseCase,
    IGetProductForecastUseCase productForecastUseCase,
    IApplyPurchaseRecommendationsUseCase applyPurchaseUseCase,
    IApplyPromotionRecommendationUseCase applyPromotionUseCase,
    IDismissRecommendationUseCase dismissUseCase,
    IApplyTransferRecommendationUseCase applyTransferUseCase,
    IGetForecastTopUseCase topUseCase,
    IGetForecastAccuracySummaryUseCase accuracySummaryUseCase,
    IGetForecastAccuracyProductsUseCase accuracyProductsUseCase) : ApiController
{
    /// <summary>Card "Recomendações" do painel (T-DF1, F3.5).</summary>
    [HttpGet("latest/top")]
    public async Task<IActionResult> LatestTop([FromQuery] int limit = 5, [FromQuery] Guid? storeId = null, CancellationToken ct = default)
        => Handle(await topUseCase.Execute(limit, storeId, ct));

    // ---------------------------------------------------------------- F3.6c: precisão (T-DF10), só Admin

    [HttpGet("accuracy/summary")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AccuracySummary([FromQuery] int months = 12, CancellationToken ct = default)
        => Handle(await accuracySummaryUseCase.Execute(months, ct));

    [HttpGet("accuracy/products")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AccuracyProducts(
        [FromQuery] int offset = 0, [FromQuery] int limit = 20, [FromQuery] string? filters = null, CancellationToken ct = default)
        => Handle(await accuracyProductsUseCase.Execute(offset, limit, FilterHelper.Parse(filters), ct));

    // ---------------------------------------------------------------- F3.4: detalhe e ações (T-DF3 a T-DF8)

    [HttpGet("recommendations/{id:guid}")]
    public async Task<IActionResult> Recommendation(Guid id, [FromQuery] Guid? storeId, CancellationToken ct)
        => Handle(await recommendationUseCase.Execute(id, storeId, ct));

    [HttpGet("recommendations/{id:guid}/store-split")]
    public async Task<IActionResult> StoreSplit(Guid id, CancellationToken ct)
        => Handle(await storeSplitUseCase.Execute(id, ct));

    [HttpGet("products/{productId:guid}")]
    public async Task<IActionResult> ProductForecast(Guid productId, [FromQuery] int months = 24, CancellationToken ct = default)
        => Handle(await productForecastUseCase.Execute(productId, months, ct));

    [HttpPost("recommendations/purchase")]
    public async Task<IActionResult> ApplyPurchase([FromBody] ApplyPurchaseDto dto, CancellationToken ct)
        => HandleCreated(await applyPurchaseUseCase.Execute(dto, UserId, ct));

    [HttpPost("recommendations/{id:guid}/promotion")]
    public async Task<IActionResult> ApplyPromotion(Guid id, [FromBody] ApplyPromotionDto dto, CancellationToken ct)
        => HandleCreated(await applyPromotionUseCase.Execute(id, dto, UserId, ct));

    [HttpPost("recommendations/{id:guid}/transfer")]
    public async Task<IActionResult> ApplyTransfer(Guid id, [FromBody] ApplyTransferDto? dto, CancellationToken ct)
        => HandleCreated(await applyTransferUseCase.Execute(id, dto, UserId, ct));

    [HttpPost("recommendations/{id:guid}/dismiss")]
    public async Task<IActionResult> Dismiss(Guid id, [FromBody] DismissRecommendationDto? dto, CancellationToken ct)
        => Handle(await dismissUseCase.Execute(id, dto, UserId, ct));

    // ---------------------------------------------------------------- F3.3: análise e leitura (T-DF2, T-DF9)

    [HttpPost("generate")]
    public async Task<IActionResult> Generate(CancellationToken ct)
        => HandleCreated(await generateUseCase.Execute(UserId, ct));

    [HttpGet("latest")]
    public async Task<IActionResult> Latest(CancellationToken ct)
        => Handle(await latestUseCase.Execute(ct));

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int offset = 0, [FromQuery] int limit = 20, [FromQuery] string? filters = null, CancellationToken ct = default)
        => Handle(await getAllUseCase.Execute(offset, limit, FilterHelper.Parse(filters), ct: ct));

    [HttpGet("{runId:guid}")]
    public async Task<IActionResult> GetById(Guid runId, CancellationToken ct)
        => Handle(await getRunUseCase.Execute(runId, ct));

    [HttpGet("{runId:guid}/status")]
    public async Task<IActionResult> Status(Guid runId, CancellationToken ct)
        => Handle(await progressUseCase.Execute(runId, ct));

    [HttpGet("{runId:guid}/batches")]
    public async Task<IActionResult> Batches(Guid runId, CancellationToken ct)
        => Handle(await batchesUseCase.Execute(runId, ct));

    [HttpGet("{runId:guid}/recommendations")]
    public async Task<IActionResult> Recommendations(
        Guid runId, [FromQuery] int offset = 0, [FromQuery] int limit = 20, [FromQuery] string? filters = null,
        [FromQuery] string? sort = null, [FromQuery] string? order = null, [FromQuery] Guid? storeId = null, CancellationToken ct = default)
        => Handle(await recommendationsUseCase.Execute(runId, offset, limit, FilterHelper.Parse(filters), sort, order, storeId, ct));
}
