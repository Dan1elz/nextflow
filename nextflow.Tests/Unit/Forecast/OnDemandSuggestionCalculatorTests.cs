using Nextflow.Application.Forecast;

namespace Nextflow.Tests.Unit.Forecast;

/// <summary>Sugestão sob demanda do painel (RN-DF30): base calculada e limites da IA (RN-DF12).</summary>
public class OnDemandSuggestionCalculatorTests
{
    [Fact]
    [Trait("Rule", "RN-DF30")]
    public void Should_CoverLeadTimeAndThirtyDays_When_BelowMinimum()
    {
        // Mínimo 36, saldo 2, vende 1/dia, prazo 10: alvo 36 + 40 = 76; menos saldo = 74; múltiplo 12 → 84.
        var quantity = OnDemandSuggestionCalculator.PurchaseQuantity(new(2m, 36m, 0m, 1m, 10, 12m, 12m));

        quantity.ShouldBe(84m);
    }

    [Fact]
    [Trait("Rule", "RN-DF30")]
    public void Should_AtLeastRefillMinimum_When_IncomingCoversDemand()
    {
        // A caminho cobre a venda, mas o local está 10 abaixo do mínimo: compra pelo menos a falta.
        var quantity = OnDemandSuggestionCalculator.PurchaseQuantity(new(2m, 12m, 500m, 1m, 10, null, null));

        quantity.ShouldBe(10m);
    }

    [Fact]
    [Trait("Rule", "RN-DF12")]
    public void Should_ClampAiQuantity_When_OutsideHalfToOneAndHalf()
    {
        OnDemandSuggestionCalculator.ClampAiQuantity(1000m, 84m, 12m, 12m).ShouldBe(132m); // 126 → múltiplo 12
        OnDemandSuggestionCalculator.ClampAiQuantity(1m, 84m, 12m, 12m).ShouldBe(48m);     // 42 → múltiplo 12
    }

    [Fact]
    [Trait("Rule", "RN-DF30")]
    public void Should_SuggestMinimumDiscount_When_NormalSalesClearTheLot()
    {
        // 7 un. vencendo em 10 dias, vende 1/dia: escoa sem esforço.
        var suggestion = OnDemandSuggestionCalculator.Promotion(new(7m, 10, 1m, 10m, 5m));

        suggestion.DiscountPercent.ShouldBe(5m);
        suggestion.Days.ShouldBe(10);
    }

    [Fact]
    [Trait("Rule", "RN-DF30")]
    public void Should_RaiseDiscountWithLeftover_When_SalesDoNotClearTheLot()
    {
        // 30 un., 12 dias, vende 0,5/dia: saem 6, sobram 80% → 5% + 16 p.p. = 21% → passo de 5% = 25%.
        var suggestion = OnDemandSuggestionCalculator.Promotion(new(30m, 12, 0.5m, 10m, 5m));

        suggestion.ExpectedSales.ShouldBe(6m);
        suggestion.DiscountPercent.ShouldBe(25m);
    }

    [Fact]
    [Trait("Rule", "RN-DF12")]
    public void Should_KeepMargin_When_CostIsHigh()
    {
        // Preço 10, custo 8: máximo = 1 − 8 × 1,1 / 10 = 12%.
        var suggestion = OnDemandSuggestionCalculator.Promotion(new(30m, 12, 0m, 10m, 8m));

        suggestion.MaxDiscountPercent.ShouldBe(12m);
        suggestion.DiscountPercent.ShouldBe(12m);
    }

    [Theory]
    [Trait("Rule", "RN-DF12")]
    [InlineData(60, 30, 30)] // teto 45, mas não passa do vencimento
    [InlineData(60, 90, 45)]
    [InlineData(1, 30, 7)]
    [InlineData(10, 6, 6)]   // vence antes de 7 dias: vai até o vencimento
    [InlineData(1, 0, 1)]
    public void Should_LimitPromotionDays_When_Requested(int requested, int daysToExpire, int expected)
        => OnDemandSuggestionCalculator.PromotionDays(requested, daysToExpire).ShouldBe(expected);
}
