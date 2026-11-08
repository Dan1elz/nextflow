using Nextflow.Domain.Enums;

namespace Nextflow.Application.Forecast;

/// <summary>Decisão final de um produto (cálculo + IA depois dos guardrails), pronta para virar <c>ForecastRecommendation</c>.</summary>
public sealed class ForecastDecision
{
    public RecommendationActionEnum Action { get; init; }
    public RecommendationPriorityEnum Priority { get; init; }
    public decimal? SuggestedQuantity { get; init; }
    public decimal? SuggestedDiscountPercent { get; init; }
    public int? SuggestedPromotionDays { get; init; }
    public DateOnly? Deadline { get; init; }
    public string Reason { get; init; } = string.Empty;

    /// <summary>true quando o motivo ou a decisão veio da IA (mesmo que ajustado pelos guardrails).</summary>
    public bool FromAi { get; init; }

    /// <summary>true quando a IA mudou algo do cálculo: ação, prioridade, quantidade, desconto ou dias (selo "IA ajustou", T-DF2).</summary>
    public bool AiAdjusted { get; init; }
}
