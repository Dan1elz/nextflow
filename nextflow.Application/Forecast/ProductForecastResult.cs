using Nextflow.Domain.Enums;

namespace Nextflow.Application.Forecast;

/// <summary>Resultado determinístico do cálculo para um produto (a IA só o revisa; ADR 0005).</summary>
public sealed class ProductForecastResult
{
    public required ForecastProductInput Input { get; init; }

    public bool HasSales { get; init; }
    public int MonthsOfHistory { get; init; }
    public ForecastConfidenceEnum Confidence { get; init; }

    /// <summary>Índice sazonal final por mês-calendário (posição 0 = janeiro).</summary>
    public decimal[] SeasonalIndex { get; init; } = [];

    public bool HasProductSeasonality { get; init; }
    public bool HasCategorySeasonality { get; init; }

    /// <summary>Nível dessazonalizado (un./mês).</summary>
    public decimal Level { get; init; }

    /// <summary>RN-DF04: projeção pelo mesmo mês do ano anterior (produto sazonal sem vendas recentes).</summary>
    public bool UsedSamePeriodLastYear { get; init; }

    /// <summary>Tendência anual da Regra 5b (últimos 12 meses ÷ 12 anteriores, limitada a [0,8; 1,25]).</summary>
    public decimal AnnualTrend { get; init; } = 1m;

    public IReadOnlyList<MonthlyProjection> Projections { get; init; } = [];
    public decimal ProjectedDemand { get; init; }
    public decimal NextMonthDemand { get; init; }
    public decimal NextMonthSeasonalIndex { get; init; }
    public decimal AverageHorizonIndex { get; init; }
    public decimal? CoverageDays { get; init; }

    /// <summary>Dias da janela de compra: prazo de entrega + cobertura alvo (30 dias sem política, RN-DF06).</summary>
    public int PurchaseWindowDays { get; init; }
    public decimal DemandDuringLeadTimeWindow { get; init; }
    public decimal SafetyStock { get; init; }

    /// <summary>Necessidade de compra arredondada para cima e ajustada a vida útil, pedido mínimo e múltiplo (0 = sem compra).</summary>
    public decimal PurchaseNeed { get; init; }

    /// <summary>Demanda até o fim da vida útil (RN-CE28), para os guardrails da IA reaplicarem o mesmo limite; nulo sem vida útil.</summary>
    public decimal? ShelfLifeDemand { get; init; }

    /// <summary>F3.7: produto de temporada fora da época na janela de compra; a compra fica suspensa até a época se aproximar.</summary>
    public bool OffSeasonGuardActive { get; init; }
    public decimal WindowAverageIndex { get; init; }

    /// <summary>A trava valeu pela concentração das vendas (item novo, sem índice sazonal), não pelo índice.</summary>
    public bool OffSeasonByBurst { get; init; }

    /// <summary>A compra que o cálculo sem as travas indicaria e que a F3.7 ou o fora de linha suspenderam.</summary>
    public decimal SuppressedPurchaseNeed { get; init; }
    public bool BlockedByDiscontinued { get; init; }
    public bool BlockedByOffSeason { get; init; }

    public int? DaysUntilValidity { get; init; }
    public decimal? DemandUntilValidity { get; init; }

    /// <summary>Desconto máximo (%) que preserva 10% de margem sobre o custo.</summary>
    public decimal MaxDiscountPercent { get; init; }

    /// <summary>Promoção permitida: sem promoção vigente, sem necessidade de compra e desconto máximo ≥ 5%.</summary>
    public bool CanPromote { get; init; }

    public PromotionTrigger PromotionTrigger { get; init; }

    public RecommendationActionEnum Action { get; init; } = RecommendationActionEnum.Hold;
    public RecommendationPriorityEnum Priority { get; init; } = RecommendationPriorityEnum.Low;
    public decimal? SuggestedQuantity { get; init; }
    public decimal? SuggestedDiscountPercent { get; init; }
    public int? SuggestedPromotionDays { get; init; }
    public DateOnly? Deadline { get; init; }

    /// <summary>Motivo em pt-BR por template (usado sem IA ou quando a IA não cobre o produto).</summary>
    public string Reason { get; internal set; } = string.Empty;
}
