using Nextflow.Application.Forecast;
using Nextflow.Domain.Enums;

namespace Nextflow.Tests.Unit.Forecast;

/// <summary>Precisão da previsão: previsto × vendido por mês e por produto (RN-DF19, L-DF3, F3.6a).</summary>
public class ForecastAccuracyCalculatorTests
{
    private static readonly YearMonth Jan = new(2026, 1);   // 31 dias
    private static readonly YearMonth Feb = new(2026, 2);
    private static readonly YearMonth Mar = new(2026, 3);

    private static AccuracyObservation Obs(YearMonth month, decimal projected, decimal actual, int daysOutOfStock = 0)
        => new(month, projected, actual, daysOutOfStock);

    [Theory]
    [InlineData(120, 100)]
    [InlineData(80, 100)]
    [Trait("Rule", "RN-DF19")]
    public void Should_ComputeAbsolutePercentError_When_MonthHasSales(decimal projected, decimal actual)
    {
        var month = ForecastAccuracyCalculator.MeasureMonth(Obs(Jan, projected, actual));

        month.ErrorPercent.ShouldBe(20m);
        month.Exclusion.ShouldBeNull();
    }

    [Fact]
    [Trait("Rule", "RN-DF19")]
    public void Should_RoundErrorToTwoDecimals_When_ErrorIsPeriodic()
    {
        // |10 − 3| ÷ 3 = 233,333...%
        ForecastAccuracyCalculator.MeasureMonth(Obs(Jan, 10m, 3m)).ErrorPercent.ShouldBe(233.33m);
    }

    [Theory]
    [InlineData(16, true)]
    [InlineData(15, false)]
    [Trait("Rule", "L-DF3")]
    public void Should_ExcludeMonth_When_StockoutCoversMoreThanHalfTheMonth(int daysOutOfStock, bool excluded)
    {
        var month = ForecastAccuracyCalculator.MeasureMonth(Obs(Jan, 100m, 40m, daysOutOfStock));

        if (excluded)
        {
            month.Exclusion.ShouldBe(AccuracyExclusion.Stockout);
            month.ErrorPercent.ShouldBeNull();
        }
        else
        {
            month.Exclusion.ShouldBeNull();
            month.ErrorPercent.ShouldBe(150m);
        }
    }

    [Fact]
    [Trait("Rule", "RN-DF19")]
    public void Should_ReturnZeroError_When_NothingProjectedAndNothingSold()
    {
        var month = ForecastAccuracyCalculator.MeasureMonth(Obs(Jan, 0m, 0m));

        month.ErrorPercent.ShouldBe(0m);
        month.Exclusion.ShouldBeNull();
    }

    [Fact]
    [Trait("Rule", "RN-DF19")]
    public void Should_ExcludeMonth_When_NothingSoldButSomethingProjected()
    {
        var month = ForecastAccuracyCalculator.MeasureMonth(Obs(Jan, 5m, 0m));

        month.Exclusion.ShouldBe(AccuracyExclusion.NoSales);
        month.ErrorPercent.ShouldBeNull();
    }

    [Fact]
    [Trait("Rule", "RN-DF19")]
    public void Should_AverageOnlyMeasuredMonths_When_ComputingProductMape()
    {
        // 10% e 30% medidos; março tem ruptura em 20 de 31 dias e fica fora.
        var product = ForecastAccuracyCalculator.MeasureProduct([Obs(Jan, 110m, 100m), Obs(Feb, 130m, 100m), Obs(Mar, 100m, 10m, 20)]);

        product.MapePercent.ShouldBe(20m);
        product.MonthsMeasured.ShouldBe(2);
        product.Months.Count.ShouldBe(3);
    }

    [Theory]
    [InlineData(106, ForecastBiasEnum.Over)]
    [InlineData(94, ForecastBiasEnum.Under)]
    [InlineData(105, ForecastBiasEnum.Balanced)]
    [InlineData(95, ForecastBiasEnum.Balanced)]
    [Trait("Rule", "RN-DF19")]
    public void Should_ClassifyBias_When_SignedErrorCrossesFivePercent(decimal projected, ForecastBiasEnum expected)
    {
        ForecastAccuracyCalculator.MeasureProduct([Obs(Jan, projected, 100m)]).Bias.ShouldBe(expected);
    }

    [Fact]
    [Trait("Rule", "RN-DF19")]
    public void Should_ReturnNullMape_When_NoMonthIsMeasured()
    {
        var product = ForecastAccuracyCalculator.MeasureProduct([Obs(Jan, 5m, 0m), Obs(Feb, 50m, 10m, 20)]);

        product.MonthsMeasured.ShouldBe(0);
        product.MapePercent.ShouldBeNull();
        product.Bias.ShouldBeNull();
        product.WithinTolerance.ShouldBeFalse();
    }

    [Theory]
    [InlineData(115, true)]       // 15%
    [InlineData(115.01, false)]   // 15,01%
    [Trait("Rule", "RN-DF19")]
    public void Should_MarkWithinTolerance_When_MapeIsAtMostFifteenPercent(decimal projected, bool within)
    {
        ForecastAccuracyCalculator.MeasureProduct([Obs(Jan, projected, 100m)]).WithinTolerance.ShouldBe(within);
    }

    [Fact]
    [Trait("Rule", "RN-DF19")]
    public void Should_IgnoreUnmeasuredProducts_When_ComputingShareWithinTolerance()
    {
        var products = new[]
        {
            ForecastAccuracyCalculator.MeasureProduct([Obs(Jan, 110m, 100m)]),   // 10%: dentro
            ForecastAccuracyCalculator.MeasureProduct([Obs(Jan, 100m, 100m)]),   // 0%: dentro
            ForecastAccuracyCalculator.MeasureProduct([Obs(Jan, 200m, 100m)]),   // 100%: fora
            ForecastAccuracyCalculator.MeasureProduct([Obs(Jan, 5m, 0m)]),       // sem medição
        };

        ForecastAccuracyCalculator.WithinTolerancePercent(products).ShouldBe(66.67m);
    }

    [Fact]
    [Trait("Rule", "RN-DF19")]
    public void Should_OrderMonthsChronologically_When_InputIsUnordered()
    {
        var product = ForecastAccuracyCalculator.MeasureProduct([Obs(Mar, 1m, 1m), Obs(Jan, 1m, 1m), Obs(Feb, 1m, 1m)]);

        product.Months.Select(m => m.Month).ShouldBe([Jan, Feb, Mar]);
    }
}
