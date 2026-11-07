using Nextflow.Application.Forecast;
using Nextflow.Domain.Enums;
using static Nextflow.Tests.Unit.Forecast.ForecastTestData;

namespace Nextflow.Tests.Unit.Forecast;

/// <summary>Guardrails da IA (RN-DF10 a RN-DF12): a resposta só vale dentro dos limites do cálculo.</summary>
public class ForecastGuardrailsTests
{
    private static ProductForecastResult Calc(ForecastProductInput input) => ForecastCalculator.Calculate(input, Context());

    /// <summary>Precisa comprar 157 un. (série plana de 100, estoque zerado, prazo de 10 dias).</summary>
    private static ProductForecastResult NeedsBuy() => Calc(Input());

    private static ProductForecastResult NoNeed() => Calc(Input(stock: 1000));

    private static ProductForecastResult Promotion()
    {
        var index = Enumerable.Repeat(1.5m, 12).ToArray();
        index[10] = index[11] = index[0] = 0.5m;
        return Calc(Input(Flat(100), stock: 400, productIndex: index));
    }

    private static AiRecommendationItem Ai(string action, string priority = "Media", decimal? quantity = null, decimal? discount = null, decimal? days = null)
        => new() { ProdutoId = Guid.NewGuid().ToString(), Acao = action, Prioridade = priority, Quantidade = quantity, DescontoPct = discount, DiasPromocao = days, Motivo = "Motivo da IA." };

    [Fact]
    [Trait("Rule", "RN-DF10")]
    public void Should_FallBackToCalculation_When_ProductIsMissingFromResponse()
    {
        var calc = NeedsBuy();

        var decision = ForecastGuardrails.Apply(calc, null);

        decision.Action.ShouldBe(RecommendationActionEnum.Buy);
        decision.SuggestedQuantity.ShouldBe(calc.PurchaseNeed);
        decision.Reason.ShouldBe(calc.Reason);
        decision.FromAi.ShouldBeFalse();
        decision.AiAdjusted.ShouldBeFalse();
    }

    [Fact]
    [Trait("Rule", "RN-DF10")]
    public void Should_FallBackToCalculation_When_ProductHasNoSales()
    {
        var calc = Calc(Input(Series((_, _) => 0m), monthsOfHistory: 0));

        var decision = ForecastGuardrails.Apply(calc, Ai("Comprar", quantity: 50));

        decision.Action.ShouldBe(RecommendationActionEnum.Hold);
        decision.FromAi.ShouldBeFalse();
    }

    [Fact]
    [Trait("Rule", "RN-DF10")]
    public void Should_IgnoreAction_When_AiReturnsUnknownAction()
    {
        var calc = NeedsBuy();

        ForecastGuardrails.Apply(calc, Ai("Transferir")).Action.ShouldBe(RecommendationActionEnum.Buy);
        ForecastGuardrails.Apply(calc, Ai("???")).FromAi.ShouldBeFalse();
    }

    [Fact]
    [Trait("Rule", "RN-DF11")]
    public void Should_TurnBuyIntoHold_When_NoCalculatedNeed()
    {
        var decision = ForecastGuardrails.Apply(NoNeed(), Ai("Comprar", "Alta", quantity: 50));

        decision.Action.ShouldBe(RecommendationActionEnum.Hold);
        decision.Priority.ShouldBe(RecommendationPriorityEnum.Low);
        decision.SuggestedQuantity.ShouldBeNull();
        decision.Reason.ShouldContain("Ajuste automático");
        decision.Reason.ShouldContain("Motivo da IA.");
    }

    [Theory]
    [InlineData(1000, 236)]  // 1,5 × 157 = 235,5 → 236
    [InlineData(10, 79)]     // 0,5 × 157 = 78,5 → 79
    [InlineData(200, 200)]   // dentro dos limites
    [Trait("Rule", "RN-DF12")]
    public void Should_ClampQuantityToHalfAndOneAndHalf(int requested, int expected)
    {
        var decision = ForecastGuardrails.Apply(NeedsBuy(), Ai("Comprar", quantity: requested));

        decision.Action.ShouldBe(RecommendationActionEnum.Buy);
        decision.SuggestedQuantity.ShouldBe(expected);
        decision.FromAi.ShouldBeTrue();
        (decision.Reason.Contains("ajustada") == (requested != expected)).ShouldBeTrue();
    }

    [Theory]
    [InlineData(80, 100, 45, 45)]    // desconto acima do máximo (45%) e dias acima de 45
    [InlineData(1, 2, 5, 7)]         // abaixo dos mínimos
    [InlineData(0.15, 20, 15, 20)]   // fração vinda do modelo vale como percentual
    [Trait("Rule", "RN-DF12")]
    public void Should_ClampDiscountAndPromotionDays(double discount, int days, double expectedDiscount, int expectedDays)
    {
        var decision = ForecastGuardrails.Apply(Promotion(), Ai("Promover", discount: (decimal)discount, days: days));

        decision.Action.ShouldBe(RecommendationActionEnum.Promote);
        decision.SuggestedDiscountPercent.ShouldBe((decimal)expectedDiscount);
        decision.SuggestedPromotionDays.ShouldBe(expectedDays);
    }

    [Fact]
    [Trait("Rule", "RN-DF12")]
    public void Should_KeepCalculation_When_AiPromotesWhatCannotBePromoted()
    {
        var calc = NeedsBuy(); // precisa comprar: não pode promover

        var decision = ForecastGuardrails.Apply(calc, Ai("Promover", discount: 20, days: 15));

        decision.Action.ShouldBe(RecommendationActionEnum.Buy);
        decision.Reason.ShouldContain("Ajuste automático");
    }

    [Fact]
    [Trait("Rule", "RN-DF22")]
    public void Should_KeepCalculatedBuy_When_AiSaysHoldWithHighPriority()
    {
        var calc = NeedsBuy();
        calc.Priority.ShouldBe(RecommendationPriorityEnum.High);

        var decision = ForecastGuardrails.Apply(calc, Ai("Manter", "Baixa"));

        decision.Action.ShouldBe(RecommendationActionEnum.Buy);
        decision.SuggestedQuantity.ShouldBe(calc.PurchaseNeed);
    }

    [Fact]
    [Trait("Rule", "RN-DF22")]
    public void Should_MarkAiAdjusted_When_AiChangesPriorityOrQuantity()
    {
        var calc = NeedsBuy();

        ForecastGuardrails.Apply(calc, Ai("Comprar", "Alta", quantity: calc.PurchaseNeed)).AiAdjusted.ShouldBeFalse();
        ForecastGuardrails.Apply(calc, Ai("Comprar", "Baixa", quantity: calc.PurchaseNeed)).AiAdjusted.ShouldBeTrue();
        ForecastGuardrails.Apply(calc, Ai("Comprar", "Alta", quantity: 200)).AiAdjusted.ShouldBeTrue();
    }

    [Fact]
    [Trait("Rule", "RN-DF04")]
    public void Should_MentionLastYear_When_AiReasonOmitsIt()
    {
        var series = Series((year, month) => month is 11 or 12 ? 100m : 0m);
        var calc = Calc(Input(series, productIndex: IndexOf(series), stock: 1000));
        calc.UsedSamePeriodLastYear.ShouldBeTrue();

        var decision = ForecastGuardrails.Apply(calc, Ai("Manter"));

        decision.Reason.ShouldContain("ano anterior");
    }
}
