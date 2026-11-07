using Nextflow.Domain.Enums;

namespace Nextflow.Application.Forecast;

/// <summary>
/// Guardrails obrigatórios (spec 6.5, RN-DF10 a RN-DF12) aplicados à resposta da IA, sempre contra o cálculo determinístico:
/// produto ausente na resposta usa o cálculo; Comprar sem necessidade calculada vira Manter; quantidade de compra limitada a
/// [0,5×; 1,5×] da necessidade; desconto limitado a [5%; máximo]; dias de promoção a [7; 45]; confiança sempre do cálculo.
/// Coerência: Promover só quando o cálculo permite; Manter não substitui uma compra de prioridade Alta do cálculo; a IA nunca
/// compra produto fora de linha nem de temporada fora da época (o cálculo já suspendeu a compra, então a necessidade é zero).
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
        Priority = calc.Action == RecommendationActionEnum.Hold ? RecommendationPriorityEnum.Low : calc.Priority,
        SuggestedQuantity = calc.SuggestedQuantity,
        SuggestedDiscountPercent = calc.SuggestedDiscountPercent,
        SuggestedPromotionDays = calc.SuggestedPromotionDays,
        Deadline = calc.Deadline,
        Reason = Truncate(string.IsNullOrWhiteSpace(note) ? calc.Reason : $"{note} {calc.Reason}"),
        FromAi = false,
        AiAdjusted = false
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
        var unit = PtBr.Unit(calc.Input.SaleUnit);

        switch (action.Value)
        {
            case RecommendationActionEnum.Buy:
                {
                    if (calc.PurchaseNeed <= 0)
                    {
                        // RN-DF11: Comprar da IA sem necessidade calculada vira Manter.
                        var note = $"Ajuste automático: a IA sugeriu comprar, mas o cálculo não indica necessidade de compra " +
                                   $"(estoque de {PtBr.Compact(calc.Input.CurrentStock)} {unit}" +
                                   (calc.Input.IncomingStock > 0 ? $" + {PtBr.Compact(calc.Input.IncomingStock)} a chegar" : "") +
                                   $" para demanda projetada de {PtBr.Compact(calc.ProjectedDemand)} {unit}).";
                        return Build(calc, HoldDecision(), $"{note} {aiReason ?? calc.Reason}", fromAi: true);
                    }

                    // RN-DF12: quantidade entre 0,5× e 1,5× da calculada.
                    var need = calc.PurchaseNeed;
                    var requested = ai.Quantidade is > 0 ? ai.Quantidade.Value : need;
                    var clamped = Math.Ceiling(Math.Clamp(requested, MinQuantityFactor * need, MaxQuantityFactor * need));
                    // RN-CE27/28/30: depois do clamp, a mesma regra do cálculo (vida útil, pedido mínimo e múltiplo do fornecedor).
                    var quantity = ForecastCalculator.AdjustPurchase(clamped, calc.ShelfLifeDemand, calc.Input.CurrentStock + calc.Input.IncomingStock, calc.Input.MinOrderQty, calc.Input.OrderMultiple);
                    var reason = aiReason ?? calc.Reason;
                    if (ai.Quantidade is > 0 && Math.Ceiling(ai.Quantidade.Value) != clamped)
                        reason += $" (Quantidade ajustada de {PtBr.Compact(ai.Quantidade.Value)} para {PtBr.Compact(clamped)} {unit}: limite de 50% a 150% da necessidade calculada de {PtBr.Compact(need)} {unit}.)";
                    if (quantity != clamped)
                        reason += $" (Quantidade final de {PtBr.Compact(quantity)} {unit} pelo pedido mínimo, múltiplo ou vida útil.)";

                    return Build(calc, new ForecastDecision
                    {
                        Action = RecommendationActionEnum.Buy,
                        Priority = aiPriority,
                        SuggestedQuantity = quantity,
                        Deadline = calc.Deadline
                    }, reason, fromAi: true);
                }

            case RecommendationActionEnum.Promote:
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

                    var maxDiscount = Math.Clamp(calc.MaxDiscountPercent, ForecastCalculator.MinDiscount * 100m, ForecastCalculator.AbsoluteMaxDiscount * 100m);
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

                    return Build(calc, new ForecastDecision
                    {
                        Action = RecommendationActionEnum.Promote,
                        Priority = aiPriority,
                        SuggestedDiscountPercent = discount,
                        SuggestedPromotionDays = days
                    }, reason, fromAi: true);
                }

            default:
                {
                    if (calc.Action == RecommendationActionEnum.Buy && calc.Priority == RecommendationPriorityEnum.High)
                        return FromCalculation(calc, "Ajuste automático: a IA sugeriu manter, mas a cobertura do estoque é menor que o prazo de entrega; mantida a compra.");

                    return Build(calc, HoldDecision(), aiReason ?? calc.Reason, fromAi: aiReason != null);
                }
        }
    }

    private static ForecastDecision HoldDecision() => new()
    {
        Action = RecommendationActionEnum.Hold,
        Priority = RecommendationPriorityEnum.Low
    };

    private static ForecastDecision Build(ProductForecastResult calc, ForecastDecision decision, string reason, bool fromAi)
    {
        // Regra 5b: garante a menção ao "mesmo período do ano anterior" mesmo quando o motivo vem da IA.
        if (calc.UsedSamePeriodLastYear && !reason.Contains("ano anterior", StringComparison.OrdinalIgnoreCase))
            reason = $"{reason} {ForecastReasonTemplates.SamePeriodSentence(calc)}";

        var priority = decision.Action == RecommendationActionEnum.Hold ? RecommendationPriorityEnum.Low : decision.Priority;
        var calcPriority = calc.Action == RecommendationActionEnum.Hold ? RecommendationPriorityEnum.Low : calc.Priority;

        return new ForecastDecision
        {
            Action = decision.Action,
            Priority = priority,
            SuggestedQuantity = decision.SuggestedQuantity,
            SuggestedDiscountPercent = decision.SuggestedDiscountPercent,
            SuggestedPromotionDays = decision.SuggestedPromotionDays,
            Deadline = decision.Deadline,
            Reason = Truncate(reason),
            FromAi = fromAi,
            AiAdjusted = decision.Action != calc.Action
                || priority != calcPriority
                || decision.SuggestedQuantity != calc.SuggestedQuantity
                || decision.SuggestedDiscountPercent != calc.SuggestedDiscountPercent
                || decision.SuggestedPromotionDays != calc.SuggestedPromotionDays
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
