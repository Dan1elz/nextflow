using Nextflow.Domain.Dtos.Forecasts;
using Nextflow.Domain.Enums;

namespace Nextflow.Domain.Models;

/// <summary>
/// Recomendação por produto de uma análise (RN-DF21). Nasce <c>Pending</c>; a nova análise expira as pendentes da anterior
/// (RN-DF14). O documento gerado ao aplicar (pedido de compra ou promoção) fica nas FKs <c>Applied*</c>, preenchidas na F3.4.
/// </summary>
public class ForecastRecommendation : BaseModel
{
    public Guid ForecastRunId { get; private set; }
    public Guid ProductId { get; private set; }
    public RecommendationActionEnum Action { get; private set; }
    public RecommendationPriorityEnum Priority { get; private set; }
    /// <summary>Sempre do cálculo: a IA não altera a confiança (RN-DF10).</summary>
    public ForecastConfidenceEnum Confidence { get; private set; }
    public RecommendationStatusEnum Status { get; private set; } = RecommendationStatusEnum.Pending;
    public decimal? SuggestedQuantity { get; private set; }
    /// <summary>Quantidade do cálculo antes do ajuste da IA (0,5× a 1,5×, RN-DF12): a T-DF3 mostra as duas.</summary>
    public decimal? CalculatedQuantity { get; private set; }
    public decimal? SuggestedDiscountPercent { get; private set; }
    public int? SuggestedPromotionDays { get; private set; }
    public DateOnly? Deadline { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string ReasonCalculated { get; private set; } = string.Empty;
    public bool AiAdjusted { get; private set; }
    public decimal CurrentStock { get; private set; }
    public decimal IncomingStock { get; private set; }
    public decimal ProjectedDemand { get; private set; }
    public decimal NextMonthDemand { get; private set; }
    public decimal? CoverageDays { get; private set; }
    public decimal NextMonthSeasonalIndex { get; private set; }
    public decimal SafetyStock { get; private set; }
    public int LeadTimeDays { get; private set; }
    public LeadTimeSourceEnum LeadTimeSource { get; private set; }
    public short MonthsOfHistory { get; private set; }
    /// <summary>Projeção pelo mesmo mês do ano anterior (RN-DF04).</summary>
    public bool BasedOnLastYear { get; private set; }
    public DateTime? AppliedAt { get; private set; }
    public Guid? AppliedById { get; private set; }
    public Guid? AppliedPurchaseOrderId { get; private set; }
    public Guid? AppliedPromotionId { get; private set; }
    public string? DismissReason { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public virtual ForecastRun ForecastRun { get; private set; } = null!;
    public virtual Product Product { get; private set; } = null!;
    public virtual PurchaseOrder? AppliedPurchaseOrder { get; private set; }
    public virtual Promotion? AppliedPromotion { get; private set; }
    public virtual ICollection<ForecastRecommendationItem> Items { get; private set; } = [];

    public override string Preposition => "a";
    public override string Singular => "recomendação";
    public override string Plural => "recomendações";

    private ForecastRecommendation() { }

    public ForecastRecommendation(NewForecastRecommendationDto dto)
    {
        ForecastRunId = dto.ForecastRunId;
        ProductId = dto.ProductId;
        Action = dto.Action;
        Priority = dto.Priority;
        Confidence = dto.Confidence;
        SuggestedQuantity = dto.SuggestedQuantity;
        CalculatedQuantity = dto.CalculatedQuantity;
        SuggestedDiscountPercent = dto.SuggestedDiscountPercent;
        SuggestedPromotionDays = dto.SuggestedPromotionDays;
        Deadline = dto.Deadline;
        Reason = dto.Reason;
        ReasonCalculated = dto.ReasonCalculated;
        AiAdjusted = dto.AiAdjusted;
        CurrentStock = dto.CurrentStock;
        IncomingStock = dto.IncomingStock;
        ProjectedDemand = dto.ProjectedDemand;
        NextMonthDemand = dto.NextMonthDemand;
        CoverageDays = dto.CoverageDays;
        NextMonthSeasonalIndex = dto.NextMonthSeasonalIndex;
        SafetyStock = dto.SafetyStock;
        LeadTimeDays = dto.LeadTimeDays;
        LeadTimeSource = dto.LeadTimeSource;
        MonthsOfHistory = dto.MonthsOfHistory;
        BasedOnLastYear = dto.BasedOnLastYear;

        foreach (var item in dto.Items)
            Items.Add(new ForecastRecommendationItem(Id, item));
    }

    public bool IsPending => Status == RecommendationStatusEnum.Pending;

    /// <summary>Nova análise expira as pendentes da anterior (RN-DF14); recomendação já aplicada ou descartada não muda.</summary>
    public void Expire(DateTime utcNow)
    {
        if (!IsPending)
            return;

        Status = RecommendationStatusEnum.Expired;
        UpdatedAt = utcNow;
    }
}
