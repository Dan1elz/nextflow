using Nextflow.Domain.Enums;

namespace Nextflow.Application.Forecast;

/// <summary>Um mês completo de um produto: o que a análise projetou, o que foi vendido e quantos dias ficou sem estoque.</summary>
public readonly record struct AccuracyObservation(YearMonth Month, decimal Projected, decimal Actual, int DaysOutOfStock);

/// <summary>Por que um mês ficou fora da medição.</summary>
public enum AccuracyExclusion
{
    /// <summary>Mais da metade dos dias sem estoque (L-DF3): a venda baixa foi falta de produto, não erro da previsão.</summary>
    Stockout = 1,

    /// <summary>Nada vendido com algo previsto: o erro percentual não existe.</summary>
    NoSales = 2,
}

/// <summary>Resultado de um mês: erro % quando medido, ou o motivo da exclusão.</summary>
public sealed record AccuracyMonth(YearMonth Month, decimal Projected, decimal Actual, decimal? ErrorPercent, AccuracyExclusion? Exclusion);

/// <summary>Precisão de um produto: meses em ordem, erro médio (MAPE), viés e se está dentro da tolerância.</summary>
public sealed record ProductAccuracy(IReadOnlyList<AccuracyMonth> Months, int MonthsMeasured, decimal? MapePercent, ForecastBiasEnum? Bias, bool WithinTolerance);

/// <summary>
/// Compara a projeção com o vendido (RN-DF19, F3.6a). Puro, sem banco: quem chama escolhe qual projeção vale para cada mês (F3.6b).
/// Erro do mês = |previsto − vendido| ÷ vendido × 100. Fica fora da medição o mês com mais da metade dos dias sem estoque (L-DF3) e o
/// mês sem venda com algo previsto; previsto e vendido zerados contam como 0%.
/// </summary>
public static class ForecastAccuracyCalculator
{
    /// <summary>Erro médio até este valor conta como "dentro de ±15%" (T-DF10).</summary>
    public const decimal TolerancePercent = 15m;

    /// <summary>Erro médio com sinal acima de +5% superestima; abaixo de −5% subestima.</summary>
    public const decimal BiasThresholdPercent = 5m;

    public static AccuracyMonth MeasureMonth(AccuracyObservation observation)
    {
        var (month, projected, actual, daysOutOfStock) = observation;

        if (daysOutOfStock * 2 > month.DaysInMonth)
            return new AccuracyMonth(month, projected, actual, null, AccuracyExclusion.Stockout);

        if (actual == 0)
            return projected == 0
                ? new AccuracyMonth(month, projected, actual, 0m, null)
                : new AccuracyMonth(month, projected, actual, null, AccuracyExclusion.NoSales);

        return new AccuracyMonth(month, projected, actual, Round(Math.Abs(projected - actual) / actual * 100m), null);
    }

    public static ProductAccuracy MeasureProduct(IEnumerable<AccuracyObservation> observations)
    {
        var months = observations.OrderBy(o => o.Month).Select(MeasureMonth).ToList();
        var measured = months.Where(m => m.ErrorPercent.HasValue).ToList();
        if (measured.Count == 0)
            return new ProductAccuracy(months, 0, null, null, false);

        var mape = Round(measured.Average(m => m.ErrorPercent!.Value));
        var signed = measured.Average(m => m.Actual == 0 ? 0m : (m.Projected - m.Actual) / m.Actual * 100m);
        var bias = signed > BiasThresholdPercent ? ForecastBiasEnum.Over
            : signed < -BiasThresholdPercent ? ForecastBiasEnum.Under
            : ForecastBiasEnum.Balanced;

        return new ProductAccuracy(months, measured.Count, mape, bias, mape <= TolerancePercent);
    }

    /// <summary>% de produtos dentro da tolerância, contando só os que têm algum mês medido; nulo quando nenhum tem.</summary>
    public static decimal? WithinTolerancePercent(IEnumerable<ProductAccuracy> products)
    {
        var measured = products.Where(p => p.MonthsMeasured > 0).ToList();
        return measured.Count == 0 ? null : Round(measured.Count(p => p.WithinTolerance) * 100m / measured.Count);
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
