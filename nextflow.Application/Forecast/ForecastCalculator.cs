using Nextflow.Domain.Enums;

namespace Nextflow.Application.Forecast;

/// <summary>
/// Cálculo determinístico da previsão de demanda (spec <c>docs/AI_FORECAST_SPEC.md</c>, seção 5 + Regra 5b).
/// Classe pura: sem EF, sem I/O, sem relógio — tudo vem por parâmetro, o que a torna fácil de testar e explicar.
/// </summary>
/// <remarks>
/// Passos (por produto):
/// <list type="number">
/// <item>Meses de histórico = meses desde a primeira venda (máx. 24).</item>
/// <item>Índice sazonal do produto (≥ 12 meses): média do mês-calendário ÷ média mensal geral (meses sem venda = 0).</item>
/// <item>Índice da categoria de referência: mesmo cálculo sobre a soma dos produtos da categoria.</item>
/// <item>Índice final: w·produto + (1−w)·categoria, w = min(1, meses/24); sem categoria → 1,0; limitado a [0,05; 5].</item>
/// <item>Nível dessazonalizado: média dos últimos 6 meses de qtd ÷ max(índice, 0,2).</item>
/// <item>Regra 5b: com ≥ 12 meses de histórico e vendas dos últimos 6 meses &lt; 15% das vendas dos últimos 12,
/// a projeção de cada mês usa a venda do mesmo mês do ano anterior × tendência anual (limitada a [0,8; 1,25]).</item>
/// <item>Projeção mensal = nível × índice do mês (mês corrente: fração restante de dias).</item>
/// <item>Confiança, cobertura, necessidade de compra, data-limite, promoção, desconto e prioridade (itens 7–13).</item>
/// </list>
/// </remarks>
public static class ForecastCalculator
{
    public const decimal MinSeasonalIndex = 0.05m;
    public const decimal MaxSeasonalIndex = 5m;
    public const decimal LevelIndexFloor = 0.2m;
    public const int LevelMonths = 6;

    /// <summary>Regra 5b: fração máxima das vendas dos últimos 12 meses que caiu nos últimos 6 para ativar o fallback.</summary>
    public const decimal SamePeriodThreshold = 0.15m;
    public const decimal MinAnnualTrend = 0.8m;
    public const decimal MaxAnnualTrend = 1.25m;

    public const int PurchaseWindowExtraDays = 30;
    public const decimal SafetyStockFactor = 0.25m;

    public const decimal ExcessStockFactor = 1.5m;
    public const decimal LowSeasonIndex = 0.9m;
    public const int ValidityWindowDays = 60;

    public const decimal MinDiscount = 0.05m;
    public const decimal MaxSuggestedDiscount = 0.25m;
    public const decimal DefaultMaxDiscount = 0.30m;
    public const decimal MinMarginOverCost = 1.10m;
    public const int MinPromotionDays = 14;
    public const int MaxPromotionDays = 30;

    // ------------------------------------------------------------------ API

    public static ProductForecastResult Calculate(ForecastProductInput input, ForecastCalculationContext ctx)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(ctx);

        var series = NormalizeSeries(input.MonthlySales);
        var months = Math.Clamp(input.MonthsOfHistory, 0, ForecastCalculationContext.HistoryWindowMonths);
        var hasSales = months > 0 && series.Any(q => q > 0);

        if (!hasSales)
            return NoSalesResult(input, ctx);

        // 2–4. Índice sazonal
        var productIndex = ComputeSeasonalIndex(series, months, ctx.LastCompleteMonth);
        var categoryIndex = input.CategoryMonthlySales != null
            ? ComputeSeasonalIndex(
                NormalizeSeries(input.CategoryMonthlySales),
                Math.Clamp(input.CategoryMonthsOfHistory, 0, ForecastCalculationContext.HistoryWindowMonths),
                ctx.LastCompleteMonth)
            : null;
        var seasonal = CombineIndexes(productIndex, categoryIndex, months);

        // 5. Nível dessazonalizado (últimos 6 meses ou os disponíveis)
        var levelMonths = Math.Min(LevelMonths, months);
        decimal levelSum = 0;
        for (var i = series.Length - levelMonths; i < series.Length; i++)
        {
            var month = ctx.MonthAt(i);
            levelSum += series[i] / Math.Max(seasonal[month.Month - 1], LevelIndexFloor);
        }
        var level = levelMonths > 0 ? levelSum / levelMonths : 0;

        // 5b. Produto sazonal sem vendas recentes → mesmo período do ano anterior × tendência
        var last12 = SumLast(series, 12);
        var last6 = SumLast(series, 6);
        var useLastYear = months >= 12 && last12 > 0 && last6 < SamePeriodThreshold * last12;
        var trend = 1m;
        if (useLastYear && months > 12)
        {
            // tendência = últimos 12 meses ÷ 12 meses anteriores (quando há vendas nesse período anterior)
            var previous12 = series.Take(series.Length - 12).TakeLast(12).Sum();
            if (previous12 > 0)
                trend = Math.Clamp(last12 / previous12, MinAnnualTrend, MaxAnnualTrend);
        }

        decimal Project(YearMonth month)
        {
            if (!useLastYear)
                return level * seasonal[month.Month - 1];

            // mesmo mês-calendário do ano anterior que esteja dentro da série
            var source = month.AddMonths(-12);
            while (source > ctx.LastCompleteMonth)
                source = source.AddMonths(-12);
            var index = series.Length - 1 - YearMonth.MonthsBetween(source, ctx.LastCompleteMonth);
            return index >= 0 ? series[index] * trend : 0;
        }

        // 6. Projeção do horizonte (a partir do próximo mês)
        var projections = ctx.Horizon
            .Select(m => new MonthlyProjection(m, Round(Project(m), 2), Round(seasonal[m.Month - 1], 3)))
            .ToList();
        var projectedDemand = projections.Sum(p => p.Quantity);
        var nextMonth = projections[0];
        var nextMonthDemand = nextMonth.Quantity;
        var averageHorizonIndex = Round(projections.Average(p => p.SeasonalIndex), 3);

        // Demanda entre hoje (inclusive) e hoje + N dias, usando a fração de cada mês
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

        // 7. Confiança
        var confidence = months switch
        {
            < 6 => ForecastConfidence.Low,
            < 18 => ForecastConfidence.Medium,
            _ => ForecastConfidence.High
        };

        // 8. Cobertura
        var stock = Math.Max(0, input.CurrentStock);
        var incoming = Math.Max(0, input.IncomingStock);
        decimal? coverage = nextMonthDemand > 0 ? Round(Math.Min(stock / (nextMonthDemand / 30m), 9999m), 1) : null;

        // 9. Necessidade de compra
        var leadTime = Math.Max(1, input.LeadTimeDays);
        var windowDemand = DemandOverDays(leadTime + PurchaseWindowExtraDays);
        var safetyStock = Math.Max(input.MinStock, SafetyStockFactor * nextMonthDemand);
        var rawNeed = windowDemand + safetyStock - stock - incoming;
        var need = rawNeed > 0 ? Math.Ceiling(rawNeed) : 0;

        DateOnly? deadline = null;
        if (need > 0)
        {
            var daysOfStock = coverage.HasValue
                ? (int)Math.Floor(coverage.Value)
                : DaysUntilStockout(stock + incoming, DemandOverDays);
            deadline = ctx.Today.AddDays(Math.Max(0, daysOfStock - leadTime));
        }

        // 10–11. Promoção (validade próxima ou excesso em baixa temporada)
        int? daysUntilValidity = input.Validity.HasValue ? input.Validity.Value.DayNumber - ctx.Today.DayNumber : null;
        decimal? demandUntilValidity = daysUntilValidity is > 0 ? Round(DemandOverDays(daysUntilValidity.Value), 2) : null;

        var trigger = PromotionTrigger.None;
        if (need <= 0)
        {
            if (daysUntilValidity is > 0 and <= ValidityWindowDays && stock > demandUntilValidity)
                trigger = PromotionTrigger.Validity;
            else if (stock > ExcessStockFactor * projectedDemand && averageHorizonIndex < LowSeasonIndex)
                trigger = PromotionTrigger.Excess;
        }

        var maxDiscount = MaxDiscountFraction(input.Price, input.CostPrice);
        var hasActivePromotion = input.ActivePromotionDiscountPercent.HasValue;
        var canPromote = need <= 0 && !hasActivePromotion && maxDiscount >= MinDiscount && stock > 0;

        // 12. Ação e prioridade base
        var action = RecommendationAction.Hold;
        var priority = RecommendationPriority.Low;
        decimal? suggestedQuantity = null;
        decimal? suggestedDiscount = null;
        int? promotionDays = null;

        if (need > 0)
        {
            action = RecommendationAction.Buy;
            suggestedQuantity = need;
            priority = coverage switch
            {
                null => RecommendationPriority.Low,
                var c when c < leadTime => RecommendationPriority.High,
                var c when c < leadTime + 15 => RecommendationPriority.Medium,
                _ => RecommendationPriority.Low
            };
        }
        else if (trigger != PromotionTrigger.None && canPromote)
        {
            action = RecommendationAction.Promote;
            suggestedDiscount = SuggestedDiscountPercent(maxDiscount);
            promotionDays = trigger == PromotionTrigger.Validity
                ? Math.Clamp(daysUntilValidity!.Value - 3, MinPromotionDays, MaxPromotionDays)
                : (stock >= 3 * Math.Max(projectedDemand, 1) ? MaxPromotionDays : 21);
            priority = trigger == PromotionTrigger.Validity ? RecommendationPriority.High : RecommendationPriority.Medium;
        }

        var result = new ProductForecastResult
        {
            Input = input,
            HasSales = true,
            MonthsOfHistory = months,
            Confidence = confidence,
            SeasonalIndex = [.. seasonal.Select(x => Round(x, 3))],
            HasProductSeasonality = productIndex != null,
            HasCategorySeasonality = categoryIndex != null,
            Level = Round(level, 2),
            UsedSamePeriodLastYear = useLastYear,
            AnnualTrend = Round(trend, 3),
            Projections = projections,
            ProjectedDemand = Round(projectedDemand, 2),
            NextMonthDemand = nextMonthDemand,
            NextMonthSeasonalIndex = nextMonth.SeasonalIndex,
            AverageHorizonIndex = averageHorizonIndex,
            CoverageDays = coverage,
            DemandDuringLeadTimeWindow = Round(windowDemand, 2),
            SafetyStock = Round(safetyStock, 2),
            PurchaseNeed = need,
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
    /// Índice sazonal por mês-calendário (posição 0 = janeiro) a partir de uma série que termina em
    /// <paramref name="lastMonth"/> (último mês completo). Usa só os meses desde a primeira venda; exige ≥ 12 meses
    /// (senão null). Meses sem venda contam como 0. A "média mensal geral" é a média das 12 médias de
    /// mês-calendário — assim a média dos índices é 1,0 mesmo quando alguns meses têm 2 observações e outros 1.
    /// </summary>
    public static decimal[]? ComputeSeasonalIndex(IReadOnlyList<decimal> series, int monthsOfHistory, YearMonth lastMonth)
    {
        if (monthsOfHistory < 12 || series.Count == 0)
            return null;

        var sums = new decimal[12];
        var counts = new int[12];
        var lastIndex = series.Count - 1;
        var start = Math.Max(0, series.Count - monthsOfHistory);

        for (var i = start; i <= lastIndex; i++)
        {
            var calendarMonth = lastMonth.AddMonths(i - lastIndex).Month - 1;
            sums[calendarMonth] += series[i];
            counts[calendarMonth]++;
        }

        var averages = new decimal[12];
        for (var m = 0; m < 12; m++)
            averages[m] = counts[m] > 0 ? sums[m] / counts[m] : 0;

        var mean = averages.Average();
        if (mean <= 0)
            return null;

        return [.. averages.Select(a => a / mean)];
    }

    // ------------------------------------------------------------------ internos

    private static decimal[] CombineIndexes(decimal[]? productIndex, decimal[]? categoryIndex, int monthsOfHistory)
    {
        var weight = Math.Min(1m, monthsOfHistory / (decimal)ForecastCalculationContext.HistoryWindowMonths);
        var combined = new decimal[12];
        for (var m = 0; m < 12; m++)
        {
            var category = categoryIndex?[m] ?? 1m;
            var product = productIndex?[m] ?? category;
            combined[m] = Math.Clamp(weight * product + (1 - weight) * category, MinSeasonalIndex, MaxSeasonalIndex);
        }
        return combined;
    }

    private static decimal[] NormalizeSeries(IReadOnlyList<decimal> source)
    {
        var size = ForecastCalculationContext.HistoryWindowMonths;
        var series = new decimal[size];
        // alinha pelo fim (mês mais recente); valores negativos viram 0
        var offset = size - source.Count;
        for (var i = 0; i < source.Count; i++)
        {
            var target = i + offset;
            if (target >= 0)
                series[target] = Math.Max(0, source[i]);
        }
        return series;
    }

    private static decimal SumLast(decimal[] series, int count) => series.Skip(Math.Max(0, series.Length - count)).Sum();

    private static int DaysUntilStockout(decimal stock, Func<int, decimal> demandOverDays)
    {
        // busca binária no 1º dia em que a demanda acumulada supera o estoque (até 1 ano)
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

    /// <summary>Desconto máximo (fração) que preserva 10% de margem sobre o custo; sem custo → 30%.</summary>
    public static decimal MaxDiscountFraction(decimal price, decimal? costPrice)
    {
        if (price <= 0)
            return 0;
        if (costPrice is not > 0)
            return DefaultMaxDiscount;
        return Math.Max(0, 1 - costPrice.Value * MinMarginOverCost / price);
    }

    /// <summary>Desconto sugerido (%): min(máximo, 25%) arredondado para baixo em múltiplos de 5, limitado a [5%, máximo].</summary>
    public static decimal SuggestedDiscountPercent(decimal maxDiscountFraction)
    {
        var target = Math.Min(maxDiscountFraction, MaxSuggestedDiscount);
        var rounded = Math.Floor(target * 20m) / 20m; // múltiplos de 5%
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
            Confidence = ForecastConfidence.Low,
            SeasonalIndex = [.. Enumerable.Repeat(1m, 12)],
            Projections = [],
            NextMonthSeasonalIndex = 1m,
            AverageHorizonIndex = 1m,
            MaxDiscountPercent = Round(MaxDiscountFraction(input.Price, input.CostPrice) * 100m, 2),
            Action = RecommendationAction.Hold,
            Priority = RecommendationPriority.Low
        };
        return WithReason(result, ctx);
    }

    private static ProductForecastResult WithReason(ProductForecastResult result, ForecastCalculationContext ctx)
    {
        result.Reason = ForecastReasonTemplates.Build(result, ctx);
        return result;
    }
}
