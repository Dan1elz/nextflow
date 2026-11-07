using Nextflow.Domain.Enums;

namespace Nextflow.Application.Forecast;

/// <summary>
/// Cálculo determinístico da previsão (spec <c>AI_FORECAST_SPEC.md</c> §5 e Regra 5b, RN-DF03 a RN-DF09, RN-DF17, RN-DF21 fica no
/// <see cref="VariantAllocator"/>). Classe pura: sem EF, sem I/O, sem relógio; tudo vem por parâmetro.
/// </summary>
/// <remarks>
/// Passos por produto: meses de histórico; índice sazonal (produto da F3.1 combinado com o da categoria por <c>w = min(1, meses/24)</c>,
/// limitado a [0,05; 5]); nível dessazonalizado (média dos últimos 6 meses de qtd ÷ max(índice; 0,2)); Regra 5b (produto sazonal sem
/// vendas recentes projeta pelo mesmo mês do ano anterior × tendência); projeção do horizonte; confiança; cobertura; necessidade de
/// compra; promoção, desconto e prioridade; produto sem venda fica em Manter.
/// F3.7: produto de temporada fora da época não vira "Comprar" por mínimo ou por demanda residual (ver <see cref="OffSeasonGuardActive"/>).
/// </remarks>
public static class ForecastCalculator
{
    public const decimal MinSeasonalIndex = 0.05m;
    public const decimal MaxSeasonalIndex = 5m;
    public const decimal LevelIndexFloor = 0.2m;
    public const int LevelMonths = 6;

    /// <summary>Regra 5b: fração máxima das vendas dos últimos 12 meses que caiu nos últimos 6 para ativar o fallback (RN-DF04).</summary>
    public const decimal SamePeriodThreshold = 0.15m;
    public const decimal MinAnnualTrend = 0.8m;
    public const decimal MaxAnnualTrend = 1.25m;

    /// <summary>Cobertura somada ao prazo de entrega na janela de compra quando não há política de estoque (RN-DF06).</summary>
    public const int PurchaseWindowExtraDays = 30;
    public const decimal SafetyStockFactor = 0.25m;

    public const decimal ExcessStockFactor = 1.5m;
    public const decimal LowSeasonIndex = 0.9m;
    public const int ValidityWindowDays = 60;

    public const decimal MinDiscount = 0.05m;
    public const decimal MaxSuggestedDiscount = 0.25m;
    public const decimal DefaultMaxDiscount = 0.30m;

    /// <summary>Teto do desconto de promoção (a tabela de promoções aceita de 1 a 90, RN-VD10).</summary>
    public const decimal AbsoluteMaxDiscount = 0.90m;
    public const decimal MinMarginOverCost = 1.10m;
    public const int MinPromotionDays = 14;
    public const int MaxPromotionDays = 30;

    /// <summary>F3.7: produto é de temporada quando o pico do índice sazonal chega a este valor.</summary>
    public const decimal SeasonalPeakIndex = 2m;

    /// <summary>F3.7, item novo sem índice: histórico mínimo e parcela das vendas nos 2 maiores meses para valer como "concentrado".</summary>
    public const int BurstMinMonths = 4;
    public const decimal BurstShare = 0.6m;

    /// <summary>F3.7: fora da época quando o índice médio da janela de compra fica abaixo deste valor.</summary>
    public const decimal OffSeasonIndex = 0.5m;

    public static ProductForecastResult Calculate(ForecastProductInput input, ForecastCalculationContext ctx, ForecastOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(ctx);
        options ??= ForecastOptions.Default;

        var series = NormalizeSeries(input.MonthlySales);
        var months = Math.Clamp(input.MonthsOfHistory, 0, ForecastCalculationContext.HistoryWindowMonths);
        var hasSales = months > 0 && series.Any(q => q > 0);

        // RN-DF09: sem venda no período, Manter com confiança Baixa (e fora da IA).
        if (!hasSales)
            return NoSalesResult(input, ctx);

        // RN-DF03: índice do produto (F3.1) combinado com o da categoria.
        var seasonal = CombineIndexes(input.ProductSeasonalIndex, input.CategorySeasonalIndex, months);

        // 5. Nível dessazonalizado (últimos 6 meses ou os disponíveis)
        var levelMonths = Math.Min(LevelMonths, months);
        decimal levelSum = 0;
        for (var i = series.Length - levelMonths; i < series.Length; i++)
        {
            var month = ctx.MonthAt(i);
            levelSum += series[i] / Math.Max(seasonal[month.Month - 1], LevelIndexFloor);
        }
        var level = levelMonths > 0 ? levelSum / levelMonths : 0;

        // 5b. RN-DF04: produto sazonal sem vendas recentes projeta pelo mesmo período do ano anterior × tendência.
        var last12 = SumLast(series, 12);
        var last6 = SumLast(series, 6);
        var useLastYear = months >= 12 && last12 > 0 && last6 < SamePeriodThreshold * last12;
        var trend = 1m;
        if (useLastYear && months > 12)
        {
            var previous12 = series.Take(series.Length - 12).TakeLast(12).Sum();
            if (previous12 > 0)
                trend = Math.Clamp(last12 / previous12, MinAnnualTrend, MaxAnnualTrend);
        }

        decimal Project(YearMonth month)
        {
            if (!useLastYear)
                return level * seasonal[month.Month - 1];

            var source = month.AddMonths(-12);
            while (source > ctx.LastCompleteMonth)
                source = source.AddMonths(-12);
            var index = series.Length - 1 - YearMonth.MonthsBetween(source, ctx.LastCompleteMonth);
            return index >= 0 ? series[index] * trend : 0;
        }

        // 6. Projeção do horizonte (a partir do próximo mês; sempre até o horizonte inteiro, L-DF2)
        var projections = ctx.Horizon
            .Select(m => new MonthlyProjection(m, Round(Project(m), 2), Round(seasonal[m.Month - 1], 3)))
            .ToList();
        var projectedDemand = projections.Sum(p => p.Quantity);
        var nextMonth = projections[0];
        var nextMonthDemand = nextMonth.Quantity;
        var averageHorizonIndex = Round(projections.Average(p => p.SeasonalIndex), 3);

        // Demanda entre hoje (inclusive) e hoje + N dias, usando a fração de cada mês.
        decimal DemandOverDays(int days)
        {
            decimal total = 0;
            var cursor = ctx.Today;
            var remaining = days;
            while (remaining > 0)
            {
                var month = YearMonth.From(cursor);
                var available = month.DaysInMonth - cursor.Day + 1;
                var take = Math.Min(remaining, available);
                total += Project(month) * take / month.DaysInMonth;
                remaining -= take;
                cursor = cursor.AddDays(take);
            }
            return total;
        }

        // Índice sazonal médio dos dias da janela (F3.7).
        decimal AverageIndexOverDays(int days)
        {
            decimal total = 0;
            var cursor = ctx.Today;
            var remaining = days;
            while (remaining > 0)
            {
                var month = YearMonth.From(cursor);
                var take = Math.Min(remaining, month.DaysInMonth - cursor.Day + 1);
                total += seasonal[month.Month - 1] * take;
                remaining -= take;
                cursor = cursor.AddDays(take);
            }
            return days > 0 ? total / days : 1m;
        }

        // 7. RN-DF05: confiança pelos meses de histórico.
        var confidence = months switch
        {
            < 6 => ForecastConfidenceEnum.Low,
            < 18 => ForecastConfidenceEnum.Medium,
            _ => ForecastConfidenceEnum.High
        };

        // 8. Cobertura
        var stock = Math.Max(0, input.CurrentStock);
        var incoming = Math.Max(0, input.IncomingStock);
        decimal? coverage = nextMonthDemand > 0 ? Round(Math.Min(stock / (nextMonthDemand / 30m), 9999m), 1) : null;

        // 9. RN-DF06: necessidade de compra. A cobertura alvo da política ABC (RN-CE29) substitui os 30 dias fixos quando existe.
        var leadTime = Math.Max(1, input.LeadTimeDays);
        var windowDays = leadTime + (input.TargetCoverageDays is > 0 ? input.TargetCoverageDays.Value : PurchaseWindowExtraDays);
        var windowDemand = DemandOverDays(windowDays);
        var safetyDaysStock = input.SafetyDays is > 0 ? input.SafetyDays.Value * nextMonthDemand / 30m : 0m;
        var safetyStock = Math.Max(Math.Max(input.MinStock, SafetyStockFactor * nextMonthDemand), safetyDaysStock);
        var rawNeed = windowDemand + safetyStock - stock - incoming;
        decimal? shelfLifeDemand = input.ShelfLifeDays is > 0 ? DemandOverDays(input.ShelfLifeDays.Value) : null;
        var fullNeed = AdjustPurchase(rawNeed > 0 ? Math.Ceiling(rawNeed) : 0, shelfLifeDemand, stock + incoming, input.MinOrderQty, input.OrderMultiple);

        // F3.7: temporada fora da época e fora de linha suspendem a compra (RN-CE19), sem esconder o que o cálculo indicaria.
        var windowIndex = AverageIndexOverDays(windowDays);
        // Com índice (12 meses ou mais): pico >= 2 e janela de baixa. Sem índice (item novo, menos de 12 meses): vendas concentradas em
        // poucos meses e nenhuma venda nos 2 últimos meses (a época passou e a primeira volta do ano ainda não pode ser prevista).
        var byIndex = seasonal.Max() >= SeasonalPeakIndex && windowIndex < OffSeasonIndex;
        var byBurst = IsBurstWithoutRecentSales(series, months);
        var offSeason = options.OffSeasonGuard && (byIndex || byBurst);
        var need = fullNeed;
        var blockedByDiscontinued = false;
        var blockedByOffSeason = false;
        if (fullNeed > 0 && input.IsDiscontinued)
        {
            need = 0;
            blockedByDiscontinued = true;
        }
        else if (fullNeed > 0 && offSeason)
        {
            need = 0;
            blockedByOffSeason = true;
        }

        DateOnly? deadline = null;
        if (need > 0)
        {
            var daysOfStock = coverage.HasValue
                ? (int)Math.Floor(coverage.Value)
                : DaysUntilStockout(stock + incoming, DemandOverDays);
            deadline = ctx.Today.AddDays(Math.Max(0, daysOfStock - leadTime));
        }

        // 10–11. RN-DF07/08: promoção (validade próxima ou excesso em baixa temporada).
        int? daysUntilValidity = input.NextExpiry.HasValue ? input.NextExpiry.Value.DayNumber - ctx.Today.DayNumber : null;
        decimal? demandUntilValidity = daysUntilValidity is > 0 ? Round(DemandOverDays(daysUntilValidity.Value), 2) : null;

        var trigger = PromotionTrigger.None;
        if (need <= 0)
        {
            if (daysUntilValidity is > 0 and <= ValidityWindowDays && input.NextExpiryQuantity > demandUntilValidity)
                trigger = PromotionTrigger.Validity;
            else if (stock > ExcessStockFactor * projectedDemand && averageHorizonIndex < LowSeasonIndex)
                trigger = PromotionTrigger.Excess;
        }

        var maxDiscount = MaxDiscountFraction(input.Price, input.CostPrice);
        var hasActivePromotion = input.ActivePromotionDiscountPercent.HasValue;
        var canPromote = need <= 0 && !hasActivePromotion && maxDiscount >= MinDiscount && stock > 0;

        // 12. Ação e prioridade base
        var action = RecommendationActionEnum.Hold;
        var priority = RecommendationPriorityEnum.Low;
        decimal? suggestedQuantity = null;
        decimal? suggestedDiscount = null;
        int? promotionDays = null;

        if (need > 0)
        {
            action = RecommendationActionEnum.Buy;
            suggestedQuantity = need;
            priority = coverage switch
            {
                null => RecommendationPriorityEnum.Low,
                var c when c < leadTime => RecommendationPriorityEnum.High,
                var c when c < leadTime + 15 => RecommendationPriorityEnum.Medium,
                _ => RecommendationPriorityEnum.Low
            };

        }
        else if (trigger != PromotionTrigger.None && canPromote)
        {
            action = RecommendationActionEnum.Promote;
            suggestedDiscount = SuggestedDiscountPercent(maxDiscount);
            promotionDays = trigger == PromotionTrigger.Validity
                ? Math.Clamp(daysUntilValidity!.Value - 3, MinPromotionDays, MaxPromotionDays)
                : (stock >= 3 * Math.Max(projectedDemand, 1) ? MaxPromotionDays : 21);
            priority = trigger == PromotionTrigger.Validity ? RecommendationPriorityEnum.High : RecommendationPriorityEnum.Medium;
        }

        var result = new ProductForecastResult
        {
            Input = input,
            HasSales = true,
            MonthsOfHistory = months,
            Confidence = confidence,
            SeasonalIndex = [.. seasonal.Select(x => Round(x, 3))],
            HasProductSeasonality = input.ProductSeasonalIndex is { Count: 12 },
            HasCategorySeasonality = input.CategorySeasonalIndex is { Count: 12 },
            Level = Round(level, 2),
            UsedSamePeriodLastYear = useLastYear,
            AnnualTrend = Round(trend, 3),
            Projections = projections,
            ProjectedDemand = Round(projectedDemand, 2),
            NextMonthDemand = nextMonthDemand,
            NextMonthSeasonalIndex = nextMonth.SeasonalIndex,
            AverageHorizonIndex = averageHorizonIndex,
            CoverageDays = coverage,
            PurchaseWindowDays = windowDays,
            DemandDuringLeadTimeWindow = Round(windowDemand, 2),
            SafetyStock = Round(safetyStock, 2),
            PurchaseNeed = need,
            ShelfLifeDemand = shelfLifeDemand,
            OffSeasonGuardActive = offSeason,
            OffSeasonByBurst = options.OffSeasonGuard && byBurst && !byIndex,
            WindowAverageIndex = Round(windowIndex, 3),
            SuppressedPurchaseNeed = blockedByDiscontinued || blockedByOffSeason ? fullNeed : 0,
            BlockedByDiscontinued = blockedByDiscontinued,
            BlockedByOffSeason = blockedByOffSeason,
            DaysUntilValidity = daysUntilValidity,
            DemandUntilValidity = demandUntilValidity,
            MaxDiscountPercent = Round(maxDiscount * 100m, 2),
            CanPromote = canPromote,
            PromotionTrigger = trigger,
            Action = action,
            Priority = priority,
            SuggestedQuantity = suggestedQuantity,
            SuggestedDiscountPercent = suggestedDiscount,
            SuggestedPromotionDays = promotionDays,
            Deadline = deadline
        };

        return WithReason(result, ctx);
    }

    /// <summary>
    /// Ponto único (calculador e guardrails da IA): ajusta a quantidade ao que o fornecedor e o produto permitem: não passa do que vende antes de vencer (RN-CE28), respeita o
    /// pedido mínimo e arredonda para cima ao múltiplo da embalagem (RN-CE27, RN-CE30).
    /// </summary>
    public static decimal AdjustPurchase(decimal need, decimal? shelfLifeDemand, decimal available, decimal? minOrderQty, decimal? orderMultiple)
    {
        if (need <= 0)
            return 0;

        if (shelfLifeDemand is { } demand)
        {
            var sellableBeforeExpiry = Math.Ceiling(demand - available);
            need = Math.Min(need, Math.Max(0, sellableBeforeExpiry));
            if (need <= 0)
                return 0;
        }

        if (minOrderQty is > 0)
            need = Math.Max(need, minOrderQty.Value);

        if (orderMultiple is > 0)
            need = Math.Ceiling(need / orderMultiple.Value) * orderMultiple.Value;

        return need;
    }

    // ------------------------------------------------------------------ internos

    /// <summary>
    /// Índice final por mês-calendário (posição 0 = janeiro): <c>w·produto + (1−w)·categoria</c>, com <c>w = min(1, meses/24)</c>.
    /// Sem índice do produto vale o da categoria; sem nenhum dos dois, 1,0. Sempre limitado a [0,05; 5] (RN-DF03).
    /// </summary>
    public static decimal[] CombineIndexes(IReadOnlyList<decimal>? productIndex, IReadOnlyList<decimal>? categoryIndex, int monthsOfHistory)
    {
        var weight = Math.Min(1m, monthsOfHistory / (decimal)ForecastCalculationContext.HistoryWindowMonths);
        var combined = new decimal[12];
        for (var m = 0; m < 12; m++)
        {
            var category = categoryIndex is { Count: 12 } ? categoryIndex[m] : 1m;
            var product = productIndex is { Count: 12 } ? productIndex[m] : category;
            combined[m] = Math.Clamp(weight * product + (1 - weight) * category, MinSeasonalIndex, MaxSeasonalIndex);
        }
        return combined;
    }

    private static decimal[] NormalizeSeries(IReadOnlyList<decimal> source)
    {
        var size = ForecastCalculationContext.HistoryWindowMonths;
        var series = new decimal[size];
        // Alinha pelo fim (mês mais recente); valor negativo vira 0.
        var offset = size - source.Count;
        for (var i = 0; i < source.Count; i++)
        {
            var target = i + offset;
            if (target >= 0)
                series[target] = Math.Max(0, source[i]);
        }
        return series;
    }

    /// <summary>
    /// F3.7, item sem índice sazonal: os 2 maiores meses concentram 60% ou mais das vendas do histórico (4 meses ou mais) e os 2 últimos
    /// meses completos não tiveram venda.
    /// </summary>
    private static bool IsBurstWithoutRecentSales(decimal[] series, int months)
    {
        // Só item novo, sem índice sazonal: com 12 meses ou mais o índice do produto decide (RN-DF03).
        if (months < BurstMinMonths || months >= ForecastCalculationContext.HistoryWindowMonths / 2)
            return false;

        var window = series.TakeLast(months).ToArray();
        var total = window.Sum();
        return total > 0
            && window.TakeLast(2).Sum() == 0
            && window.OrderByDescending(q => q).Take(2).Sum() >= BurstShare * total;
    }

    private static decimal SumLast(decimal[] series, int count) => series.Skip(Math.Max(0, series.Length - count)).Sum();

    private static int DaysUntilStockout(decimal stock, Func<int, decimal> demandOverDays)
    {
        // Busca binária no 1º dia em que a demanda acumulada supera o estoque (até 1 ano).
        int low = 0, high = 365;
        if (demandOverDays(high) <= stock)
            return high;
        while (low < high)
        {
            var mid = (low + high) / 2;
            if (demandOverDays(mid) > stock) high = mid; else low = mid + 1;
        }
        return low;
    }

    /// <summary>RN-DF08: desconto máximo (fração) que preserva 10% de margem sobre o custo; sem custo, 30%.</summary>
    public static decimal MaxDiscountFraction(decimal price, decimal? costPrice)
    {
        if (price <= 0)
            return 0;
        if (costPrice is not > 0)
            return DefaultMaxDiscount;
        return Math.Clamp(1 - costPrice.Value * MinMarginOverCost / price, 0, AbsoluteMaxDiscount);
    }

    /// <summary>RN-DF08: desconto sugerido (%) = min(máximo; 25%) em múltiplos de 5, limitado a [5%; máximo].</summary>
    public static decimal SuggestedDiscountPercent(decimal maxDiscountFraction)
    {
        var target = Math.Min(maxDiscountFraction, MaxSuggestedDiscount);
        var rounded = Math.Floor(target * 20m) / 20m;
        var clamped = Math.Clamp(rounded, MinDiscount, Math.Max(MinDiscount, maxDiscountFraction));
        return Round(clamped * 100m, 2);
    }

    private static decimal Round(decimal value, int decimals) => Math.Round(value, decimals, MidpointRounding.AwayFromZero);

    private static ProductForecastResult NoSalesResult(ForecastProductInput input, ForecastCalculationContext ctx)
    {
        var result = new ProductForecastResult
        {
            Input = input,
            HasSales = false,
            MonthsOfHistory = 0,
            Confidence = ForecastConfidenceEnum.Low,
            SeasonalIndex = [.. Enumerable.Repeat(1m, 12)],
            Projections = [],
            NextMonthSeasonalIndex = 1m,
            AverageHorizonIndex = 1m,
            MaxDiscountPercent = Round(MaxDiscountFraction(input.Price, input.CostPrice) * 100m, 2),
            Action = RecommendationActionEnum.Hold,
            Priority = RecommendationPriorityEnum.Low
        };
        return WithReason(result, ctx);
    }

    private static ProductForecastResult WithReason(ProductForecastResult result, ForecastCalculationContext ctx)
    {
        result.Reason = ForecastReasonTemplates.Build(result, ctx);
        return result;
    }
}
