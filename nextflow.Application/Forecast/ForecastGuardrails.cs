using Nextflow.Domain.Enums;

namespace Nextflow.Application.Forecast;

/// <summary>Decisão final de um produto (cálculo + IA após guardrails), pronta para virar <c>ForecastRecommendation</c>.</summary>
public sealed class ForecastDecision
{
    public RecommendationAction Action { get; init; }
    public RecommendationPriority Priority { get; init; }
    public decimal? SuggestedQuantity { get; init; }
    public decimal? SuggestedDiscountPercent { get; init; }
    public int? SuggestedPromotionDays { get; init; }
    public DateOnly? Deadline { get; init; }
    public string Reason { get; init; } = string.Empty;

    /// <summary>true quando o motivo/decisão veio da IA (mesmo que ajustado pelos guardrails).</summary>
    public bool FromAi { get; init; }
}

/// <summary>
/// Guardrails obrigatórios (spec 6.5) aplicados à resposta da IA, sempre contra o cálculo determinístico:
/// <list type="bullet">
/// <item>produtoId desconhecido → ignorado; produto sem item na resposta → usa o cálculo;</item>
/// <item>Comprar com necessidade calculada = 0 → rebaixado para Manter (motivo registra o ajuste);</item>
/// <item>quantidade de compra limitada a [0,5×; 1,5×] da necessidade calculada;</item>
/// <item>desconto limitado a [5%; desconto máximo]; dias de promoção a [7; 45];</item>
/// <item>confiança sempre do cálculo.</item>
/// </list>
/// Guardrails adicionais (coerência): Promover só quando o cálculo permite (sem promoção vigente, sem necessidade de
/// compra e margem para ≥ 5%); Manter não substitui uma compra de prioridade Alta do cálculo (risco de ruptura
/// antes da reposição).
/// </summary>
public static class ForecastGuardrails
{
    public const decimal MinQuantityFactor = 0.5m;
    public const decimal MaxQuantityFactor = 1.5m;
    public const int MinPromotionDays = 7;
    public const int MaxPromotionDays = 45;
    public const int MaxReasonLength = 1000;

    /// <summary>Decisão 100% do cálculo (sem IA), com o motivo de template.</summary>
    public static ForecastDecision FromCalculation(ProductForecastResult calc, string? note = null) => new()
    {
        Action = calc.Action,
        Priority = calc.Action == RecommendationAction.Hold ? RecommendationPriority.Low : calc.Priority,
        SuggestedQuantity = calc.SuggestedQuantity,
        SuggestedDiscountPercent = calc.SuggestedDiscountPercent,
        SuggestedPromotionDays = calc.SuggestedPromotionDays,
        Deadline = calc.Deadline,
        Reason = Truncate(string.IsNullOrWhiteSpace(note) ? calc.Reason : $"{note} {calc.Reason}"),
        FromAi = false
    };

    public static ForecastDecision Apply(ProductForecastResult calc, AiRecommendationItem? ai)
    {
        if (ai == null || !calc.HasSales)
            return FromCalculation(calc);

        var action = ForecastAiPrompt.ParseAction(ai.Acao);
        if (action == null)
            return FromCalculation(calc);

        var aiPriority = ForecastAiPrompt.ParsePriority(ai.Prioridade) ?? calc.Priority;
        var aiReason = string.IsNullOrWhiteSpace(ai.Motivo) ? null : ai.Motivo.Trim();
        var unit = PtBr.Unit(calc.Input.UnitType);

        switch (action.Value)
        {
            case RecommendationAction.Buy:
            {
                if (calc.PurchaseNeed <= 0)
                {
                    var hold = HoldDecision();
                    var note = $"Ajuste automático: a IA sugeriu comprar, mas o cálculo não indica necessidade de compra " +
                               $"(estoque de {PtBr.Compact(calc.Input.CurrentStock)} {unit}" +
                               (calc.Input.IncomingStock > 0 ? $" + {PtBr.Compact(calc.Input.IncomingStock)} a chegar" : "") +
                               $" para demanda projetada de {PtBr.Compact(calc.ProjectedDemand)} {unit}).";
                    return Build(hold, $"{note} {aiReason ?? calc.Reason}", fromAi: true);
                }

                var need = calc.PurchaseNeed;
                var requested = ai.Quantidade is > 0 ? ai.Quantidade.Value : need;
                var quantity = Math.Ceiling(Math.Clamp(requested, MinQuantityFactor * need, MaxQuantityFactor * need));
                var reason = aiReason ?? calc.Reason;
                if (ai.Quantidade is > 0 && Math.Ceiling(ai.Quantidade.Value) != quantity)
                    reason += $" (Quantidade ajustada de {PtBr.Compact(ai.Quantidade.Value)} para {PtBr.Compact(quantity)} {unit}: limite de 50% a 150% da necessidade calculada de {PtBr.Compact(need)} {unit}.)";

                return Build(new ForecastDecision
                {
                    Action = RecommendationAction.Buy,
                    Priority = aiPriority,
                    SuggestedQuantity = quantity,
                    Deadline = calc.Deadline
                }, reason, fromAi: true, calc);
            }

            case RecommendationAction.Promote:
            {
                if (!calc.CanPromote)
                {
                    var why = calc.PurchaseNeed > 0
                        ? "o cálculo indica necessidade de compra"
                        : calc.Input.ActivePromotionDiscountPercent.HasValue
                            ? "o produto já possui promoção vigente"
                            : "a margem não permite o desconto mínimo de 5%";
                    return FromCalculation(calc, $"Ajuste automático: a IA sugeriu promover, mas {why}; mantida a sugestão do cálculo.");
                }

                var maxDiscount = Math.Max(ForecastCalculator.MinDiscount * 100m, calc.MaxDiscountPercent);
                var requestedDiscount = NormalizePercent(ai.DescontoPct)
                    ?? calc.SuggestedDiscountPercent
                    ?? ForecastCalculator.SuggestedDiscountPercent(calc.MaxDiscountPercent / 100m);
                var discount = Math.Round(Math.Clamp(requestedDiscount, ForecastCalculator.MinDiscount * 100m, maxDiscount), 2, MidpointRounding.AwayFromZero);

                var requestedDays = ai.DiasPromocao is > 0
                    ? (int)Math.Round(ai.DiasPromocao.Value, MidpointRounding.AwayFromZero)
                    : calc.SuggestedPromotionDays ?? 21;
                var days = Math.Clamp(requestedDays, MinPromotionDays, MaxPromotionDays);

                var reason = aiReason ?? calc.Reason;
                if (NormalizePercent(ai.DescontoPct) is { } asked && Math.Round(asked, 2) != discount)
                    reason += $" (Desconto ajustado de {PtBr.Percent(asked)} para {PtBr.Percent(discount)}: limite de 5% a {PtBr.Percent(calc.MaxDiscountPercent)}, preservando 10% de margem sobre o custo.)";

                return Build(new ForecastDecision
                {
                    Action = RecommendationAction.Promote,
                    Priority = aiPriority,
                    SuggestedDiscountPercent = discount,
                    SuggestedPromotionDays = days
                }, reason, fromAi: true, calc);
            }

            default:
            {
                if (calc.Action == RecommendationAction.Buy && calc.Priority == RecommendationPriority.High)
                    return FromCalculation(calc, "Ajuste automático: a IA sugeriu manter, mas a cobertura do estoque é menor que o prazo de entrega; mantida a compra.");

                return Build(HoldDecision(), aiReason ?? calc.Reason, fromAi: aiReason != null, calc);
            }
        }
    }

    private static ForecastDecision HoldDecision() => new()
    {
        Action = RecommendationAction.Hold,
        Priority = RecommendationPriority.Low
    };

    private static ForecastDecision Build(ForecastDecision decision, string reason, bool fromAi, ProductForecastResult? calc = null)
    {
        // Regra 5b: garante a menção ao "mesmo período do ano anterior" mesmo quando o motivo vem da IA
        if (calc is { UsedSamePeriodLastYear: true } && !reason.Contains("ano anterior", StringComparison.OrdinalIgnoreCase))
            reason = $"{reason} {ForecastReasonTemplates.SamePeriodSentence(calc)}";

        return new ForecastDecision
        {
            Action = decision.Action,
            Priority = decision.Action == RecommendationAction.Hold ? RecommendationPriority.Low : decision.Priority,
            SuggestedQuantity = decision.SuggestedQuantity,
            SuggestedDiscountPercent = decision.SuggestedDiscountPercent,
            SuggestedPromotionDays = decision.SuggestedPromotionDays,
            Deadline = decision.Deadline,
            Reason = Truncate(reason),
            FromAi = fromAi
        };
    }

    /// <summary>Aceita 15 (= 15%) e também 0,15 (fração) vindo do modelo.</summary>
    private static decimal? NormalizePercent(decimal? value)
    {
        if (value is not > 0)
            return null;
        return value.Value < 1m ? value.Value * 100m : value.Value;
    }

    private static string Truncate(string value)
    {
        value = value.Trim();
        return value.Length <= MaxReasonLength ? value : value[..(MaxReasonLength - 1)].TrimEnd() + "…";
    }
}
