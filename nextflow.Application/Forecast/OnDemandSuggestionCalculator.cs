namespace Nextflow.Application.Forecast;

/// <summary>Fatos de uma variação num local para a sugestão de compra sob demanda (estoque baixo do painel).</summary>
public sealed record PurchaseSuggestionInput(
    decimal Available,
    decimal MinQuantity,
    decimal Incoming,
    decimal DailySales,
    int LeadTimeDays,
    decimal? MinOrderQty,
    decimal? OrderMultiple);

/// <summary>Fatos de um lote num local para a sugestão de promoção sob demanda (lotes vencendo do painel).</summary>
public sealed record PromotionSuggestionInput(
    decimal Quantity,
    int DaysToExpire,
    decimal DailySales,
    decimal Price,
    decimal? Cost);

public sealed record PromotionSuggestion(decimal DiscountPercent, int Days, decimal MaxDiscountPercent, decimal ExpectedSales);

/// <summary>
/// Cálculo da sugestão sob demanda (RN-DF30, a aprovar): a base que a IA recebe e o resultado quando ela não responde. Mesmos limites
/// da análise (RN-DF12): compra da IA entre 0,5× e 1,5× do calculado; desconto entre 5% e o máximo que a margem permite; dias de
/// promoção entre 7 e 45, sem passar do vencimento do lote.
/// </summary>
public static class OnDemandSuggestionCalculator
{
    /// <summary>Cobertura depois que a compra chega: os dias do prazo de entrega mais esta folga.</summary>
    public const int CoverageDays = 30;

    public const int MinPromotionDays = 7;
    public const int MaxPromotionDays = 45;

    /// <summary>
    /// Quanto comprar para a variação no local: volta ao mínimo e cobre a venda do prazo de entrega mais <see cref="CoverageDays"/>
    /// dias, descontado o que já está a caminho; nunca menos do que falta para o mínimo. Arredonda ao pedido mínimo e ao múltiplo.
    /// </summary>
    public static decimal PurchaseQuantity(PurchaseSuggestionInput input)
    {
        var available = Math.Max(input.Available, 0m);
        var target = input.MinQuantity + input.DailySales * (input.LeadTimeDays + CoverageDays);
        var need = Math.Max(target - available - input.Incoming, input.MinQuantity - available);
        return need <= 0 ? 0m : RoundToSupplier(need, input.MinOrderQty, input.OrderMultiple);
    }

    /// <summary>Limita a quantidade da IA a 0,5×–1,5× da calculada (RN-DF12) e arredonda ao fornecedor.</summary>
    public static decimal ClampAiQuantity(decimal aiQuantity, decimal calculated, decimal? minOrderQty, decimal? orderMultiple)
    {
        if (calculated <= 0)
            return 0m;
        var clamped = Math.Clamp(aiQuantity, calculated * 0.5m, calculated * 1.5m);
        return RoundToSupplier(clamped, minOrderQty, orderMultiple);
    }

    public static decimal RoundToSupplier(decimal quantity, decimal? minOrderQty, decimal? orderMultiple)
    {
        var result = Math.Max(Math.Ceiling(quantity), minOrderQty ?? 0m);
        if (orderMultiple is > 0)
            result = Math.Ceiling(result / orderMultiple.Value) * orderMultiple.Value;
        return result;
    }

    /// <summary>
    /// Desconto para escoar o lote até vencer: se a venda normal até o vencimento cobre o lote, o mínimo (5%); senão cresce com a
    /// sobra (5% + sobra × 20 p.p.), em passos de 5%, até o menor entre 25% e o máximo da margem. Dura até a véspera do vencimento.
    /// </summary>
    public static PromotionSuggestion Promotion(PromotionSuggestionInput input)
    {
        var maxFraction = ForecastCalculator.MaxDiscountFraction(input.Price, input.Cost);
        var days = PromotionDays(input.DaysToExpire, input.DaysToExpire);
        var expected = Math.Round(input.DailySales * Math.Max(input.DaysToExpire, 1), 2, MidpointRounding.AwayFromZero);
        var leftover = input.Quantity <= 0 ? 0m : Math.Clamp(1 - expected / input.Quantity, 0m, 1m);
        var target = ForecastCalculator.MinDiscount + leftover * 0.20m;
        var stepped = Math.Ceiling(target * 20m) / 20m;
        var discount = ClampDiscount(stepped * 100m, maxFraction * 100m);
        return new PromotionSuggestion(discount, days, Math.Round(maxFraction * 100m, 2, MidpointRounding.AwayFromZero), expected);
    }

    /// <summary>Desconto entre 5% e o máximo da margem, sem passar de 25% no cálculo (a IA pode ir até o máximo).</summary>
    public static decimal ClampDiscount(decimal percent, decimal maxPercent, bool fromAi = false)
    {
        var min = ForecastCalculator.MinDiscount * 100m;
        var cap = fromAi ? maxPercent : Math.Min(maxPercent, ForecastCalculator.MaxSuggestedDiscount * 100m);
        return Math.Round(Math.Clamp(percent, min, Math.Max(min, cap)), 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Dias de promoção entre 7 e 45 (RN-DF12), sem passar do vencimento: lote que vence antes de 7 dias vai até ele.</summary>
    public static int PromotionDays(int requested, int daysToExpire)
    {
        var limit = Math.Max(daysToExpire, 1);
        return Math.Clamp(requested, Math.Min(MinPromotionDays, limit), Math.Min(MaxPromotionDays, limit));
    }
}
