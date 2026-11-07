using Nextflow.Domain.Enums;

namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>
/// Recomendação por produto já decidida (cálculo + IA depois dos guardrails). <c>Reason</c> é o texto final (da IA ou do
/// template); <c>ReasonCalculated</c> é sempre o do cálculo; <c>AiAdjusted</c> marca o que a IA mudou (T-DF3).
/// </summary>
public record NewForecastRecommendationDto(
    Guid ForecastRunId,
    Guid ProductId,
    RecommendationActionEnum Action,
    RecommendationPriorityEnum Priority,
    ForecastConfidenceEnum Confidence,
    decimal? SuggestedQuantity,
    decimal? CalculatedQuantity,
    decimal? SuggestedDiscountPercent,
    int? SuggestedPromotionDays,
    DateOnly? Deadline,
    string Reason,
    string ReasonCalculated,
    bool AiAdjusted,
    decimal CurrentStock,
    decimal IncomingStock,
    decimal ProjectedDemand,
    decimal NextMonthDemand,
    decimal? CoverageDays,
    decimal NextMonthSeasonalIndex,
    decimal SafetyStock,
    int LeadTimeDays,
    LeadTimeSourceEnum LeadTimeSource,
    short MonthsOfHistory,
    bool BasedOnLastYear,
    IReadOnlyList<NewForecastRecommendationItemDto> Items);
