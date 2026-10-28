using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Exceptions;
using Nextflow.Domain.Models.Base;

namespace Nextflow.Domain.Models;

/// <summary>Recomendação (Comprar/Promover/Manter) gerada para um produto em uma análise de previsão.</summary>
[Table("forecast_recommendations")]
public class ForecastRecommendation : BaseModel
{
    [ForeignKey("forecast_runs"), Required(ErrorMessage = "A análise de previsão é obrigatória.")]
    public Guid ForecastRunId { get; private set; }
    public virtual ForecastRun? ForecastRun { get; private set; }

    [ForeignKey("products"), Required(ErrorMessage = "O Produto é obrigatório.")]
    public Guid ProductId { get; private set; }
    public virtual Product? Product { get; private set; }

    public RecommendationAction Action { get; private set; }
    public RecommendationPriority Priority { get; private set; }
    public ForecastConfidence Confidence { get; private set; }
    public RecommendationStatus Status { get; private set; } = RecommendationStatus.Pending;

    /// <summary>Quantidade sugerida (Comprar).</summary>
    public decimal? SuggestedQuantity { get; private set; }

    /// <summary>Desconto sugerido em % (Promover).</summary>
    public decimal? SuggestedDiscountPercent { get; private set; }

    /// <summary>Duração sugerida da promoção em dias (Promover).</summary>
    public int? SuggestedPromotionDays { get; private set; }

    /// <summary>Data-limite para comprar considerando o lead time (Comprar).</summary>
    public DateOnly? Deadline { get; private set; }

    [Required(ErrorMessage = "O motivo é obrigatório."), StringLength(1000, ErrorMessage = "O motivo não pode exceder 1000 caracteres.")]
    public string Reason { get; private set; } = string.Empty;

    public decimal CurrentStock { get; private set; }
    public decimal IncomingStock { get; private set; }
    public decimal ProjectedDemand { get; private set; }
    public decimal NextMonthDemand { get; private set; }
    public decimal? CoverageDays { get; private set; }
    public decimal NextMonthSeasonalIndex { get; private set; }

    public DateTime? AppliedAt { get; private set; }
    public Guid? AppliedByUserId { get; private set; }

    [ForeignKey("purchase_orders")]
    public Guid? PurchaseOrderId { get; private set; }
    public virtual PurchaseOrder? PurchaseOrder { get; private set; }

    [ForeignKey("promotions")]
    public Guid? PromotionId { get; private set; }
    public virtual Promotion? Promotion { get; private set; }

    [StringLength(255, ErrorMessage = "O motivo do descarte não pode exceder 255 caracteres.")]
    public string? DismissReason { get; private set; }

    public override string Preposition => "a";
    public override string Singular => "recomendação";
    public override string Plural => "recomendações";

    private ForecastRecommendation() : base() { }

    public ForecastRecommendation(CreateForecastRecommendationDto dto) : base()
    {
        ForecastRunId = dto.ForecastRunId;
        ProductId = dto.ProductId;
        Action = dto.Action;
        Priority = dto.Priority;
        Confidence = dto.Confidence;
        SuggestedQuantity = dto.SuggestedQuantity;
        SuggestedDiscountPercent = dto.SuggestedDiscountPercent;
        SuggestedPromotionDays = dto.SuggestedPromotionDays;
        Deadline = dto.Deadline;
        Reason = dto.Reason.Length > 1000 ? dto.Reason[..1000] : dto.Reason;
        CurrentStock = dto.CurrentStock;
        IncomingStock = dto.IncomingStock;
        ProjectedDemand = dto.ProjectedDemand;
        NextMonthDemand = dto.NextMonthDemand;
        CoverageDays = dto.CoverageDays;
        NextMonthSeasonalIndex = dto.NextMonthSeasonalIndex;
    }

    public bool IsPending => Status == RecommendationStatus.Pending;

    /// <summary>Marca como aplicada (pedido de compra e/ou promoção gerados a partir dela).</summary>
    public void MarkApplied(Guid userId, Guid? purchaseOrderId = null, Guid? promotionId = null)
    {
        EnsurePending();
        Status = RecommendationStatus.Applied;
        AppliedAt = DateTime.UtcNow;
        AppliedByUserId = userId;
        PurchaseOrderId = purchaseOrderId;
        PromotionId = promotionId;
    }

    /// <summary>Marca como descartada pelo usuário.</summary>
    public void Dismiss(string? reason)
    {
        EnsurePending();
        Status = RecommendationStatus.Dismissed;
        DismissReason = string.IsNullOrWhiteSpace(reason)
            ? null
            : (reason.Trim().Length > 255 ? reason.Trim()[..255] : reason.Trim());
    }

    /// <summary>Expira uma recomendação pendente (substituída por uma análise mais recente).</summary>
    public void Expire()
    {
        if (Status == RecommendationStatus.Pending)
            Status = RecommendationStatus.Expired;
    }

    private void EnsurePending()
    {
        if (Status != RecommendationStatus.Pending)
            throw new BadRequestException("Apenas recomendações pendentes podem ser aplicadas ou descartadas.");
    }
}
