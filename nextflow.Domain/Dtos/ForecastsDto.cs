using System.ComponentModel.DataAnnotations;
using Nextflow.Domain.Attributes;
using Nextflow.Domain.Dtos.Base;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;
using Nextflow.Domain.Utils;

namespace Nextflow.Domain.Dtos;

/// <summary>Dados para criar uma <c>ForecastRecommendation</c> (uso interno do módulo de previsão).</summary>
public class CreateForecastRecommendationDto : BaseDto
{
    public Guid ForecastRunId { get; set; }
    public Guid ProductId { get; set; }
    public RecommendationAction Action { get; set; }
    public RecommendationPriority Priority { get; set; }
    public ForecastConfidence Confidence { get; set; }
    public decimal? SuggestedQuantity { get; set; }
    public decimal? SuggestedDiscountPercent { get; set; }
    public int? SuggestedPromotionDays { get; set; }
    public DateOnly? Deadline { get; set; }
    public string Reason { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public decimal IncomingStock { get; set; }
    public decimal ProjectedDemand { get; set; }
    public decimal NextMonthDemand { get; set; }
    public decimal? CoverageDays { get; set; }
    public decimal NextMonthSeasonalIndex { get; set; }
}

// ------------------------------------------------------------------ requisições (API)

/// <summary>Body de <c>POST /api/forecasts/generate</c>.</summary>
public class GenerateForecastRequestDto : BaseDto
{
    [Range(1, 3, ErrorMessage = "O horizonte deve estar entre 1 e 3 meses.")]
    public int HorizonMonths { get; set; } = 3;
}

/// <summary>Body de <c>POST /api/forecasts/recommendations/purchase</c>.</summary>
public class ApplyPurchaseRequestDto : BaseDto
{
    public List<ApplyPurchaseItemDto> Items { get; set; } = [];

    [StringLength(500, ErrorMessage = "A observação não pode exceder 500 caracteres.")]
    public string? Note { get; set; }
}

public class ApplyPurchaseItemDto : BaseDto
{
    [NotEmptyGuid(ErrorMessage = "A recomendação é obrigatória.")]
    public Guid RecommendationId { get; set; }

    [Range(0.001, double.MaxValue, ErrorMessage = "A quantidade deve ser maior que zero.")]
    public decimal Quantity { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "O preço de custo deve ser maior que zero.")]
    public decimal CostPrice { get; set; }
}

/// <summary>Body de <c>POST /api/forecasts/recommendations/{id}/promotion</c>.</summary>
public class ApplyPromotionRequestDto : BaseDto
{
    [Required(ErrorMessage = "O percentual de desconto é obrigatório."), Range(1, 90, ErrorMessage = "O percentual de desconto deve estar entre 1% e 90%.")]
    public decimal DiscountPercent { get; set; }

    [Required(ErrorMessage = "A data de início é obrigatória.")]
    public DateOnly StartDate { get; set; }

    [Required(ErrorMessage = "A data de término é obrigatória.")]
    public DateOnly EndDate { get; set; }

    [StringLength(255, ErrorMessage = "O motivo não pode exceder 255 caracteres.")]
    public string? Reason { get; set; }
}

/// <summary>Body (opcional) de <c>POST /api/forecasts/recommendations/{id}/dismiss</c>.</summary>
public class DismissRecommendationRequestDto
{
    public string? Reason { get; set; }
}

// ------------------------------------------------------------------ respostas (API)

public class ForecastRunCountsDto
{
    public int Buy { get; set; }
    public int Promote { get; set; }
    public int Hold { get; set; }

    /// <summary>Recomendações acionáveis (Comprar/Promover) ainda pendentes.</summary>
    public int Pending { get; set; }

    public ForecastRunCountsDto() { }

    public ForecastRunCountsDto(IEnumerable<ForecastRecommendation> recommendations)
    {
        foreach (var r in recommendations)
        {
            switch (r.Action)
            {
                case RecommendationAction.Buy: Buy++; break;
                case RecommendationAction.Promote: Promote++; break;
                default: Hold++; break;
            }

            if (r.Status == RecommendationStatus.Pending && r.Action != RecommendationAction.Hold)
                Pending++;
        }
    }
}

public class ForecastRunSummaryDto
{
    public Guid Id { get; set; }
    public DateTime CreateAt { get; set; }
    public Guid RequestedByUserId { get; set; }
    public string RequestedByName { get; set; } = string.Empty;
    public int HorizonMonths { get; set; }
    public ForecastRunStatus Status { get; set; }
    public string? AiProvider { get; set; }
    public string? AiModel { get; set; }
    public string? Summary { get; set; }
    public string? ErrorMessage { get; set; }
    public int DurationMs { get; set; }
    public int ProductsAnalyzed { get; set; }
    public ForecastRunCountsDto Counts { get; set; } = new();

    public ForecastRunSummaryDto() { }

    /// <summary>Requer <c>RequestedByUser</c> e <c>Recommendations</c> carregados (para nome e contagens).</summary>
    public ForecastRunSummaryDto(ForecastRun entity)
    {
        Id = entity.Id;
        CreateAt = entity.CreateAt;
        RequestedByUserId = entity.RequestedByUserId;
        RequestedByName = entity.RequestedByUser != null
            ? $"{entity.RequestedByUser.Name} {entity.RequestedByUser.LastName}".Trim()
            : string.Empty;
        HorizonMonths = entity.HorizonMonths;
        Status = entity.Status;
        AiProvider = entity.AiProvider;
        AiModel = entity.AiModel;
        Summary = entity.Summary;
        ErrorMessage = entity.ErrorMessage;
        DurationMs = entity.DurationMs;
        ProductsAnalyzed = entity.ProductsAnalyzed;
        Counts = new ForecastRunCountsDto(entity.Recommendations);
    }
}

public class ForecastRunDto : ForecastRunSummaryDto
{
    /// <summary>Ordenadas: Pendentes primeiro, depois prioridade (desc), Comprar/Promover antes de Manter.</summary>
    public List<ForecastRecommendationDto> Recommendations { get; set; } = [];

    public ForecastRunDto() { }

    public ForecastRunDto(ForecastRun entity) : base(entity)
    {
        Recommendations = [.. ForecastRecommendationDto.Sort(entity.Recommendations).Select(r => new ForecastRecommendationDto(r))];
    }
}

/// <summary>Snapshot do produto (dados atuais) embutido na recomendação.</summary>
public class ForecastRecommendationProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public UnitType UnitType { get; set; }
    public decimal Price { get; set; }
    public decimal EffectivePrice { get; set; }
    public decimal? CostPrice { get; set; }
    public decimal Quantity { get; set; }
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;

    public ForecastRecommendationProductDto() { }

    /// <summary>Requer <c>Supplier</c> e as promoções vigentes carregadas para o preço efetivo.</summary>
    public ForecastRecommendationProductDto(Product entity)
    {
        Id = entity.Id;
        Name = entity.Name;
        ProductCode = entity.ProductCode;
        Brand = entity.Brand;
        UnitType = entity.UnitType;
        Price = entity.Price;
        EffectivePrice = entity.GetEffectivePrice(BusinessClock.Today);
        CostPrice = entity.CostPrice;
        Quantity = entity.Quantity;
        SupplierId = entity.SupplierId;
        SupplierName = entity.Supplier?.Name ?? string.Empty;
    }
}

public class ForecastRecommendationDto
{
    public Guid Id { get; set; }
    public Guid ForecastRunId { get; set; }
    public Guid ProductId { get; set; }
    public ForecastRecommendationProductDto? Product { get; set; }
    public RecommendationAction Action { get; set; }
    public RecommendationPriority Priority { get; set; }
    public ForecastConfidence Confidence { get; set; }
    public RecommendationStatus Status { get; set; }
    public decimal? SuggestedQuantity { get; set; }
    public decimal? SuggestedDiscountPercent { get; set; }
    public int? SuggestedPromotionDays { get; set; }
    public DateOnly? Deadline { get; set; }
    public string Reason { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public decimal IncomingStock { get; set; }
    public decimal ProjectedDemand { get; set; }
    public decimal NextMonthDemand { get; set; }
    public decimal? CoverageDays { get; set; }
    public decimal NextMonthSeasonalIndex { get; set; }
    public DateTime? AppliedAt { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    public Guid? PromotionId { get; set; }
    public string? DismissReason { get; set; }

    public ForecastRecommendationDto() { }

    public ForecastRecommendationDto(ForecastRecommendation entity)
    {
        Id = entity.Id;
        ForecastRunId = entity.ForecastRunId;
        ProductId = entity.ProductId;
        Product = entity.Product != null ? new ForecastRecommendationProductDto(entity.Product) : null;
        Action = entity.Action;
        Priority = entity.Priority;
        Confidence = entity.Confidence;
        Status = entity.Status;
        SuggestedQuantity = entity.SuggestedQuantity;
        SuggestedDiscountPercent = entity.SuggestedDiscountPercent;
        SuggestedPromotionDays = entity.SuggestedPromotionDays;
        Deadline = entity.Deadline;
        Reason = entity.Reason;
        CurrentStock = entity.CurrentStock;
        IncomingStock = entity.IncomingStock;
        ProjectedDemand = entity.ProjectedDemand;
        NextMonthDemand = entity.NextMonthDemand;
        CoverageDays = entity.CoverageDays;
        NextMonthSeasonalIndex = entity.NextMonthSeasonalIndex;
        AppliedAt = entity.AppliedAt;
        PurchaseOrderId = entity.PurchaseOrderId;
        PromotionId = entity.PromotionId;
        DismissReason = entity.DismissReason;
    }

    /// <summary>
    /// Ordenação do contrato (spec 7.1): Pendentes primeiro, depois prioridade (desc),
    /// Comprar/Promover antes de Manter; desempate por ação (Comprar, Promover), cobertura (asc) e nome.
    /// </summary>
    public static IEnumerable<ForecastRecommendation> Sort(IEnumerable<ForecastRecommendation> recommendations) => recommendations
        .OrderBy(r => r.Status == RecommendationStatus.Pending ? 0 : 1)
        .ThenByDescending(r => r.Priority)
        .ThenBy(r => r.Action == RecommendationAction.Hold ? 1 : 0)
        .ThenBy(r => r.Action)
        .ThenBy(r => r.CoverageDays ?? decimal.MaxValue)
        .ThenBy(r => r.Product?.Name);
}

public class ForecastHistoryPointDto
{
    public int Year { get; set; }
    public int Month { get; set; }

    /// <summary>"MM/yyyy"</summary>
    public string Label { get; set; } = string.Empty;
    public decimal QuantitySold { get; set; }
    public decimal Revenue { get; set; }
}

public class ForecastProjectionPointDto
{
    public int Year { get; set; }
    public int Month { get; set; }

    /// <summary>"MM/yyyy"</summary>
    public string Label { get; set; } = string.Empty;
    public decimal ProjectedQuantity { get; set; }
}

public class SeasonalityPointDto
{
    public int Month { get; set; }
    public decimal SeasonalIndex { get; set; }
}

public class ProductForecastDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public List<ForecastHistoryPointDto> History { get; set; } = [];
    public List<ForecastProjectionPointDto> Projection { get; set; } = [];
    public List<SeasonalityPointDto> Seasonality { get; set; } = [];
    public ForecastRecommendationDto? LatestRecommendation { get; set; }
}

public class ApplyPurchaseResultDto
{
    /// <summary>1 pedido de compra (status Orçamento) por fornecedor.</summary>
    public List<PurchaseOrderResponseDto> PurchaseOrders { get; set; } = [];

    /// <summary>Recomendações já marcadas como aplicadas.</summary>
    public List<ForecastRecommendationDto> Recommendations { get; set; } = [];
}

// ------------------------------------------------------------------ consultas agregadas (repositórios)

/// <summary>Vendas confirmadas agregadas por produto e mês (mês no fuso de negócio).</summary>
public class ProductMonthlySalesAggregateDto
{
    public Guid ProductId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal QuantitySold { get; set; }
    public decimal Revenue { get; set; }
    public int OrdersCount { get; set; }
}

/// <summary>Amostra de lead time real (pedido de compra recebido).</summary>
public class PurchaseLeadTimeSampleDto
{
    public Guid SupplierId { get; set; }
    public DateTime CreateAt { get; set; }
    public DateTime ReceivedAt { get; set; }
}
