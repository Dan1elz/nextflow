using System.Text;
using Nextflow.Domain.Enums;

namespace Nextflow.Application.Forecast;

/// <summary>
/// Motivos e resumo em pt-BR gerados por template (sem IA), sempre citando os números do cálculo. Usados quando a IA está
/// indisponível, quando ela não cobre um produto, ou como base dos ajustes dos guardrails (RN-DF13).
/// </summary>
public static class ForecastReasonTemplates
{
    public const string SamePeriodNote = "baseado no mesmo período do ano anterior";

    public static string Build(ProductForecastResult r, ForecastCalculationContext ctx)
    {
        var input = r.Input;
        var unit = PtBr.Unit(input.SaleUnit);

        if (!r.HasSales)
            return "Sem vendas confirmadas nos últimos 24 meses completos: não há base para projetar a demanda. Nenhuma ação sugerida.";

        var sb = new StringBuilder();
        var stock = PtBr.Compact(input.CurrentStock);
        var horizon = HorizonText(ctx);
        var next = r.Projections[0];

        switch (r.Action)
        {
            case RecommendationActionEnum.Buy:
                sb.Append($"Estoque de {stock} {unit}");
                if (input.IncomingStock > 0)
                    sb.Append($" (+{PtBr.Compact(input.IncomingStock)} a chegar)");
                sb.Append(r.CoverageDays.HasValue
                    ? $" cobre cerca de {PtBr.Compact(r.CoverageDays.Value)} dias, contra prazo de entrega de {input.LeadTimeDays} dias."
                    : ".");
                sb.Append($" Demanda projetada de {PtBr.Compact(next.Quantity)} {unit} em {PtBr.MonthName(next.Month.Month)}");
                sb.Append($" (índice sazonal {PtBr.Number(next.SeasonalIndex, 2)}) e {PtBr.Compact(r.ProjectedDemand)} {unit} {horizon}.");
                sb.Append($" Sugestão: comprar {PtBr.Compact(r.SuggestedQuantity ?? 0)} {unit}");
                if (r.Deadline.HasValue)
                    sb.Append($" até {PtBr.Date(r.Deadline.Value)}");
                sb.Append('.');
                break;

            case RecommendationActionEnum.Promote when r.PromotionTrigger == PromotionTrigger.Validity:
                sb.Append($"Validade em {PtBr.Date(input.NextExpiry!.Value)} ({r.DaysUntilValidity} dias): o lote de {PtBr.Compact(input.NextExpiryQuantity)} {unit}");
                sb.Append($" supera a demanda projetada até lá ({PtBr.Compact(r.DemandUntilValidity ?? 0)} {unit}).");
                sb.Append($" Sugestão: desconto de {PtBr.Percent(r.SuggestedDiscountPercent ?? 0)} por {r.SuggestedPromotionDays} dias para girar o estoque antes do vencimento.");
                break;

            case RecommendationActionEnum.Promote:
                var ratio = r.ProjectedDemand > 0 ? input.CurrentStock / r.ProjectedDemand : 0;
                sb.Append($"Estoque de {stock} {unit}");
                sb.Append(r.ProjectedDemand > 0
                    ? $" equivale a {PtBr.Compact(ratio)}x a demanda projetada {horizon} ({PtBr.Compact(r.ProjectedDemand)} {unit}),"
                    : $" sem demanda relevante projetada {horizon},");
                sb.Append($" em baixa temporada (índice sazonal médio {PtBr.Number(r.AverageHorizonIndex, 2)}).");
                sb.Append($" Sugestão: desconto de {PtBr.Percent(r.SuggestedDiscountPercent ?? 0)} por {r.SuggestedPromotionDays} dias.");
                break;

            default:
                sb.Append($"Estoque de {stock} {unit}");
                if (input.IncomingStock > 0)
                    sb.Append($" + {PtBr.Compact(input.IncomingStock)} a chegar");
                // A necessidade de compra considera só a janela de compra (passo 9): o estoque pode cobrir essa janela sem cobrir
                // todo o horizonte, e nesse caso o texto não pode afirmar que cobre.
                var coversHorizon = input.CurrentStock + input.IncomingStock >= r.ProjectedDemand;
                if (coversHorizon)
                    sb.Append($" atende a demanda projetada de {PtBr.Compact(r.ProjectedDemand)} {unit} {horizon}");
                else
                    sb.Append($" atende a demanda prevista até a próxima reposição ({PtBr.Compact(r.DemandDuringLeadTimeWindow)} {unit} em " +
                              $"{r.PurchaseWindowDays} dias), mas não toda a demanda projetada {horizon} " +
                              $"({PtBr.Compact(r.ProjectedDemand)} {unit})");
                if (r.CoverageDays.HasValue)
                    sb.Append($" (cobertura de ~{PtBr.Compact(r.CoverageDays.Value)} dias; prazo de entrega de {input.LeadTimeDays} dias)");
                sb.Append('.');

                if (r.BlockedByDiscontinued)
                    sb.Append($" Produto fora de linha: compra de {PtBr.Compact(r.SuppressedPurchaseNeed)} {unit} não recomendada.");
                else if (r.BlockedByNoDemand)
                    sb.Append($" Sem ação: nenhuma demanda prevista nos próximos {r.PurchaseWindowDays} dias; o estoque mínimo sozinho " +
                              $"não justifica comprar {PtBr.Compact(r.SuppressedPurchaseNeed)} {unit}.");
                else if (r.BlockedByOffSeason)
                    sb.Append(OffSeasonSentence(r));
                else if (r.PromotionTrigger != PromotionTrigger.None)
                {
                    if (input.ActivePromotionDiscountPercent.HasValue)
                        sb.Append($" Há excesso de estoque, mas o produto já está com promoção vigente de {PtBr.Percent(input.ActivePromotionDiscountPercent.Value)}" +
                                  (input.ActivePromotionEndDate.HasValue ? $" até {PtBr.Date(input.ActivePromotionEndDate.Value)}." : "."));
                    else if (r.MaxDiscountPercent < ForecastCalculator.MinDiscount * 100)
                        sb.Append($" Há excesso de estoque, mas a margem (custo {PtBr.Number(input.CostPrice ?? 0, 2)} / preço {PtBr.Number(input.Price, 2)}) não permite o desconto mínimo de 5%.");
                }
                else
                {
                    sb.Append(coversHorizon
                        ? " Nenhuma ação necessária."
                        : " Nenhuma compra necessária agora; reavalie nas próximas análises.");
                }
                break;
        }

        if (r.UsedSamePeriodLastYear)
            sb.Append(' ').Append(SamePeriodSentence(r));

        if (r.Confidence == ForecastConfidenceEnum.Low)
            sb.Append($" Confiança baixa: apenas {r.MonthsOfHistory} meses de histórico.");

        return sb.ToString();
    }

    /// <summary>Frase da F3.7: produto de temporada fora da época, compra suspensa até a época se aproximar.</summary>
    public static string OffSeasonSentence(ProductForecastResult r)
        => r.OffSeasonByBurst
            ? " Produto de temporada sem venda nos 2 últimos meses (vendas concentradas em poucos meses e menos de 12 meses de histórico): " +
              "estoque mínimo e demanda residual não geram compra até voltar a vender."
            : $" Produto de temporada fora da época (índice sazonal médio de {PtBr.Number(r.WindowAverageIndex, 2)} nos próximos {r.PurchaseWindowDays} dias): " +
           "estoque mínimo e demanda residual não geram compra; a compra volta a ser sugerida quando a época se aproximar.";

    /// <summary>Frase da Regra 5b (projeção pelo mesmo mês do ano anterior).</summary>
    public static string SamePeriodSentence(ProductForecastResult r)
    {
        var trendPercent = (r.AnnualTrend - 1m) * 100m;
        var trend = trendPercent == 0
            ? "sem ajuste de tendência"
            : $"tendência anual de {(trendPercent > 0 ? "+" : "")}{PtBr.Compact(trendPercent)}%";
        return $"Cálculo {SamePeriodNote} ({trend}), pois o produto é sazonal e quase não vendeu nos últimos 6 meses.";
    }

    /// <summary>"nos próximos 3 meses" / "no próximo mês".</summary>
    public static string HorizonText(ForecastCalculationContext ctx)
        => ctx.HorizonMonths == 1 ? "no próximo mês" : $"nos próximos {ctx.HorizonMonths} meses";

    /// <summary>"novembro/2026 a janeiro/2027".</summary>
    public static string HorizonRange(ForecastCalculationContext ctx)
        => ctx.HorizonMonths == 1
            ? PtBr.MonthYear(ctx.Horizon[0])
            : $"{PtBr.MonthYear(ctx.Horizon[0])} a {PtBr.MonthYear(ctx.Horizon[^1])}";

    /// <summary>Resumo geral (sem IA).</summary>
    public static string BuildSummary(
        ForecastCalculationContext ctx,
        int productsAnalyzed,
        IReadOnlyCollection<(RecommendationActionEnum Action, RecommendationPriorityEnum Priority)> recommendations,
        IReadOnlyList<ForecastEvent> events)
    {
        var buy = recommendations.Count(r => r.Action == RecommendationActionEnum.Buy);
        var buyHigh = recommendations.Count(r => r.Action == RecommendationActionEnum.Buy && r.Priority == RecommendationPriorityEnum.High);
        var promote = recommendations.Count(r => r.Action == RecommendationActionEnum.Promote);
        var hold = recommendations.Count(r => r.Action == RecommendationActionEnum.Hold);

        var sb = new StringBuilder();
        sb.Append($"Análise de {productsAnalyzed} produto{(productsAnalyzed == 1 ? "" : "s")} para {HorizonRange(ctx)}: ");
        sb.Append($"{buy} para comprar");
        if (buyHigh > 0)
            sb.Append($" ({buyHigh} com prioridade alta)");
        sb.Append($", {promote} para promover e {hold} sem ação.");

        if (events.Count > 0)
            sb.Append(" Eventos comerciais no período: ")
              .Append(string.Join(", ", events.Select(e => $"{e.Name} ({PtBr.ShortDate(e.Date)})")))
              .Append('.');

        sb.Append(" Recomendações geradas pelo cálculo estatístico (sazonalidade + estoque + prazo de entrega).");
        return sb.ToString();
    }
}
