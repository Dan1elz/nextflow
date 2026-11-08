using Nextflow.Application.Forecast;
using Nextflow.Domain.Enums;
using static Nextflow.Tests.Unit.Forecast.ForecastTestData;

namespace Nextflow.Tests.Unit.Forecast;

/// <summary>
/// Cálculo puro da previsão (F3.2): cada regra com números fechados. Hoje de teste = 15/10/2026; horizonte padrão de 3 meses
/// (novembro, dezembro, janeiro). Série plana de 100/mês e prazo de 10 dias: janela de compra de 40 dias = 131,5 un. + segurança de 25.
/// </summary>
public class ForecastCalculatorTests
{
    private static ProductForecastResult Calculate(ForecastProductInput input, int horizon = 3, ForecastOptions? options = null)
        => ForecastCalculator.Calculate(input, Context(horizon), options);

    private static decimal[] IndexWithLowSeason()
    {
        // Índice dado: baixa temporada em nov, dez e jan; o resto acima da média.
        var index = Enumerable.Repeat(1.5m, 12).ToArray();
        index[10] = index[11] = index[0] = 0.5m;
        return index;
    }

    // ------------------------------------------------------------------ RN-DF03

    [Fact]
    [Trait("Rule", "RN-DF03")]
    public void Should_CombineIndexByWeight_When_HistoryIsShorterThan24Months()
    {
        var product = Enumerable.Repeat(2m, 12).ToArray();
        var category = Enumerable.Repeat(1m, 12).ToArray();

        // 12 meses → w = 0,5; 24 meses → w = 1; sem categoria o índice do produto vale sozinho.
        ForecastCalculator.CombineIndexes(product, category, 12).ShouldAllBe(v => v == 1.5m);
        ForecastCalculator.CombineIndexes(product, category, 24).ShouldAllBe(v => v == 2m);
        ForecastCalculator.CombineIndexes(null, category, 18).ShouldAllBe(v => v == 1m);
        ForecastCalculator.CombineIndexes(null, null, 6).ShouldAllBe(v => v == 1m);
    }

    [Fact]
    [Trait("Rule", "RN-DF03")]
    public void Should_ClampSeasonalIndex_When_Combining()
    {
        var high = Enumerable.Repeat(10m, 12).ToArray();
        var low = Enumerable.Repeat(0.01m, 12).ToArray();

        ForecastCalculator.CombineIndexes(high, null, 24).ShouldAllBe(v => v == 5m);
        ForecastCalculator.CombineIndexes(low, null, 24).ShouldAllBe(v => v == 0.05m);
    }

    // ------------------------------------------------------------------ RN-DF04

    private static decimal[] SeasonalNovDec(decimal previousYear, decimal lastYear)
        => Series((year, month) => month is 11 or 12 ? (year == 2024 ? previousYear : lastYear) : 0m);

    [Fact]
    [Trait("Rule", "RN-DF04")]
    public void Should_ProjectFromLastYear_When_SeasonalProductHasNoRecentSales()
    {
        var series = SeasonalNovDec(previousYear: 100, lastYear: 100);

        var result = Calculate(Input(series, productIndex: IndexOf(series)));

        result.UsedSamePeriodLastYear.ShouldBeTrue();
        result.AnnualTrend.ShouldBe(1m);
        result.Projections[0].Month.ShouldBe(new YearMonth(2026, 11));
        result.Projections[0].Quantity.ShouldBe(100m);
        result.Projections[1].Quantity.ShouldBe(100m);
        result.Projections[2].Quantity.ShouldBe(0m);
        result.Reason.ShouldContain("ano anterior");
    }

    [Theory]
    [InlineData(80, 200, 1.25)]   // tendência 2,5 → limite de 1,25
    [InlineData(400, 200, 0.8)]   // tendência 0,5 → limite de 0,8
    [InlineData(160, 200, 1.25)]  // tendência 1,25 exata
    [Trait("Rule", "RN-DF04")]
    public void Should_LimitTrend_When_ProjectingFromLastYear(int previousYear, int lastYear, double expectedTrend)
    {
        var series = SeasonalNovDec(previousYear, lastYear);

        var result = Calculate(Input(series, productIndex: IndexOf(series)));

        result.UsedSamePeriodLastYear.ShouldBeTrue();
        result.AnnualTrend.ShouldBe((decimal)expectedTrend);
        result.Projections[0].Quantity.ShouldBe(lastYear * (decimal)expectedTrend);
    }

    [Fact]
    [Trait("Rule", "RN-DF04")]
    public void Should_NotUseLastYear_When_HistoryIsShorterThan12Months()
    {
        var series = Series((_, month) => month is 11 or 12 ? 100m : 0m);

        var result = Calculate(Input(series, monthsOfHistory: 11));

        result.UsedSamePeriodLastYear.ShouldBeFalse();
    }

    // ------------------------------------------------------------------ RN-DF05

    [Theory]
    [InlineData(3, ForecastConfidenceEnum.Low)]
    [InlineData(5, ForecastConfidenceEnum.Low)]
    [InlineData(6, ForecastConfidenceEnum.Medium)]
    [InlineData(17, ForecastConfidenceEnum.Medium)]
    [InlineData(18, ForecastConfidenceEnum.High)]
    [InlineData(24, ForecastConfidenceEnum.High)]
    [Trait("Rule", "RN-DF05")]
    public void Should_RateConfidence_When_GivenMonthsOfHistory(int months, ForecastConfidenceEnum expected)
    {
        var series = Series((_, _) => 0m);
        for (var i = series.Length - months; i < series.Length; i++)
            series[i] = 10;

        Calculate(Input(series, monthsOfHistory: months)).Confidence.ShouldBe(expected);
    }

    // ------------------------------------------------------------------ RN-DF06

    [Fact]
    [Trait("Rule", "RN-DF06")]
    public void Should_SuggestBuy_When_StockDoesNotCoverPurchaseWindow()
    {
        var result = Calculate(Input());

        result.Action.ShouldBe(RecommendationActionEnum.Buy);
        result.PurchaseNeed.ShouldBe(157m); // ceil(131,5 + segurança de 25)
        result.SuggestedQuantity.ShouldBe(157m);
        result.PurchaseWindowDays.ShouldBe(40);
        result.Priority.ShouldBe(RecommendationPriorityEnum.High); // cobertura 0 < prazo de 10 dias
        result.Deadline.ShouldBe(Today);
    }

    [Fact]
    [Trait("Rule", "RN-DF06")]
    public void Should_NotSuggestBuy_When_StockCoversPurchaseWindow()
    {
        var result = Calculate(Input(stock: 300));

        result.Action.ShouldBe(RecommendationActionEnum.Hold);
        result.PurchaseNeed.ShouldBe(0m);
        result.SuggestedQuantity.ShouldBeNull();
    }

    [Fact]
    [Trait("Rule", "RN-DF06")]
    public void Should_SubtractIncomingStock_When_ComputingNeed()
    {
        Calculate(Input(incoming: 100)).PurchaseNeed.ShouldBe(57m);
        Calculate(Input(incoming: 160)).Action.ShouldBe(RecommendationActionEnum.Hold);
    }

    [Fact]
    [Trait("Rule", "RN-DF06")]
    public void Should_UseMinimumAsSafetyStock_When_AboveQuarterOfNextMonth()
    {
        var result = Calculate(Input(minStock: 200));

        result.SafetyStock.ShouldBe(200m);
        result.PurchaseNeed.ShouldBe(332m); // ceil(131,5 + 200)
    }

    // ------------------------------------------------------------------ RN-CE27/28/29

    [Fact]
    [Trait("Rule", "RN-CE29")]
    public void Should_UseTargetCoverage_When_ProductHasStockPolicy()
    {
        var result = Calculate(Input(configure: b => b.TargetCoverageDays = 60));

        result.PurchaseWindowDays.ShouldBe(70);
        result.PurchaseNeed.ShouldBe(255m); // ceil(229,03 + 25)
    }

    [Fact]
    [Trait("Rule", "RN-CE27")]
    public void Should_RoundToOrderMultipleAndMinimum_When_SupplierHasRules()
    {
        Calculate(Input(configure: b => b.OrderMultiple = 12)).PurchaseNeed.ShouldBe(168m);
        Calculate(Input(configure: b => { b.MinOrderQty = 200; b.OrderMultiple = 12; })).PurchaseNeed.ShouldBe(204m);
    }

    [Fact]
    [Trait("Rule", "RN-CE28")]
    public void Should_LimitPurchaseToShelfLife_When_ProductIsPerishable()
    {
        // Só 20 dias de vida útil: não se compra mais do que se vende em 20 dias (64,8 un.).
        Calculate(Input(configure: b => b.ShelfLifeDays = 20)).PurchaseNeed.ShouldBe(65m);
    }

    // ------------------------------------------------------------------ RN-DF07 / RN-DF08

    private static ForecastProductInput ExcessInLowSeason(decimal? cost = 5m, decimal? promotion = null)
    {
        var series = Flat(100);
        return Input(series, stock: 400, productIndex: IndexWithLowSeason(), cost: cost, configure: b => b.ActivePromotionDiscount = promotion);
    }

    [Fact]
    [Trait("Rule", "RN-DF07")]
    public void Should_SuggestPromotion_When_ExcessInLowSeason()
    {
        var result = Calculate(ExcessInLowSeason());

        result.PromotionTrigger.ShouldBe(PromotionTrigger.Excess);
        result.Action.ShouldBe(RecommendationActionEnum.Promote);
        result.SuggestedDiscountPercent.ShouldBe(25m);
        result.SuggestedPromotionDays.ShouldBe(30);
        result.Priority.ShouldBe(RecommendationPriorityEnum.Medium);
    }

    [Fact]
    [Trait("Rule", "RN-DF07")]
    public void Should_SuggestPromotion_When_LotExpiresWithin60DaysWithLeftover()
    {
        var series = Flat(10);

        var result = Calculate(Input(series, stock: 100, configure: b =>
        {
            b.NextExpiry = Today.AddDays(45);
            b.NextExpiryQuantity = 100;
        }));

        result.PromotionTrigger.ShouldBe(PromotionTrigger.Validity);
        result.Action.ShouldBe(RecommendationActionEnum.Promote);
        result.Priority.ShouldBe(RecommendationPriorityEnum.High);
        result.SuggestedPromotionDays.ShouldBe(30);
        result.Reason.ShouldContain("Validade em");
    }

    [Fact]
    [Trait("Rule", "RN-DF07")]
    public void Should_NotPromote_When_LotIsSoldBeforeExpiry()
    {
        // O lote de 10 un. vence em 45 dias, mas se vendem 148 un. até lá.
        var result = Calculate(Input(stock: 300, configure: b =>
        {
            b.NextExpiry = Today.AddDays(45);
            b.NextExpiryQuantity = 10;
        }));

        result.PromotionTrigger.ShouldNotBe(PromotionTrigger.Validity);
    }

    [Fact]
    [Trait("Rule", "RN-DF07")]
    public void Should_NotPromote_When_PromotionIsActive()
    {
        var result = Calculate(ExcessInLowSeason(promotion: 10m));

        result.PromotionTrigger.ShouldBe(PromotionTrigger.Excess);
        result.CanPromote.ShouldBeFalse();
        result.Action.ShouldBe(RecommendationActionEnum.Hold);
        result.Reason.ShouldContain("promoção vigente");
    }

    [Fact]
    [Trait("Rule", "RN-DF08")]
    public void Should_PreserveTenPercentMargin_When_SuggestingDiscount()
    {
        // Custo 8 e preço 10: com 10% de margem sobre o custo o máximo é 12%, e o sugerido 10%.
        var result = Calculate(ExcessInLowSeason(cost: 8m));

        result.MaxDiscountPercent.ShouldBe(12m);
        result.SuggestedDiscountPercent.ShouldBe(10m);
        ForecastCalculator.MaxDiscountFraction(10m, 5m).ShouldBe(0.45m);
        ForecastCalculator.MaxDiscountFraction(10m, null).ShouldBe(0.30m);
    }

    [Fact]
    [Trait("Rule", "RN-DF08")]
    public void Should_NotPromote_When_MarginDoesNotAllowFivePercent()
    {
        var result = Calculate(ExcessInLowSeason(cost: 9.8m));

        result.MaxDiscountPercent.ShouldBe(0m);
        result.Action.ShouldBe(RecommendationActionEnum.Hold);
        result.Reason.ShouldContain("margem");
    }

    // ------------------------------------------------------------------ RN-DF09 / RN-DF17

    [Fact]
    [Trait("Rule", "RN-DF09")]
    public void Should_HoldWithLowConfidence_When_ProductHasNoSales()
    {
        var result = Calculate(Input(Series((_, _) => 0m), monthsOfHistory: 0, stock: 0, minStock: 50));

        result.HasSales.ShouldBeFalse();
        result.Action.ShouldBe(RecommendationActionEnum.Hold);
        result.Confidence.ShouldBe(ForecastConfidenceEnum.Low);
        result.Priority.ShouldBe(RecommendationPriorityEnum.Low);
        result.Projections.ShouldBeEmpty();
        result.Reason.ShouldContain("Sem vendas");
    }

    [Fact]
    [Trait("Rule", "RN-DF17")]
    public void Should_ProjectUpToHorizon_When_HorizonIsSixMonths()
    {
        var result = Calculate(Input(), horizon: 6);

        result.Projections.Count.ShouldBe(6);
        result.Projections[0].Month.ShouldBe(new YearMonth(2026, 11));
        result.Projections[5].Month.ShouldBe(new YearMonth(2027, 4));
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(7, 4)]
    [InlineData(25, 4)]
    [InlineData(30, 4)]
    [InlineData(31, 5)]
    [InlineData(60, 5)]
    [InlineData(61, 6)]
    [Trait("Rule", "RN-DF17")]
    public void Should_AddThreeMonthsToLongestLeadTime_When_ComputingHorizon(int leadTimeDays, int expected)
        => ForecastHorizon.Automatic(leadTimeDays).ShouldBe(expected);

    [Theory]
    [InlineData(90)]
    [InlineData(180)]
    [Trait("Rule", "RN-DF17")]
    public void Should_CapHorizonAtSixMonths_When_LeadTimeIsLong(int leadTimeDays)
        => ForecastHorizon.Automatic(leadTimeDays).ShouldBe(6);

    [Fact]
    [Trait("Rule", "RN-DF17")]
    public void Should_UseLeadTimeOfEachProduct_When_HorizonIsLonger()
    {
        // L-DF2: horizonte de 6 meses, mas a necessidade de compra continua na janela do prazo do produto (10 + 30 dias).
        Calculate(Input(), horizon: 6).PurchaseNeed.ShouldBe(157m);
    }

    // ------------------------------------------------------------------ RN-CE19

    [Fact]
    [Trait("Rule", "RN-CE19")]
    public void Should_NeverBuy_When_ProductIsDiscontinued()
    {
        var result = Calculate(Input(configure: b => b.Discontinued = true));

        result.Action.ShouldBe(RecommendationActionEnum.Hold);
        result.PurchaseNeed.ShouldBe(0m);
        result.BlockedByDiscontinued.ShouldBeTrue();
        result.SuppressedPurchaseNeed.ShouldBe(157m);
        result.Reason.ShouldContain("fora de linha");
    }

    // ------------------------------------------------------------------ F3.7: temporada zerada fora da época

    /// <summary>
    /// Ovos de Páscoa: vendem forte em fevereiro e março, e o resto do ano pinga 2 un./mês. Estoque zerado (fora da época ninguém
    /// mantém), mínimo de 10 e prazo de 10 dias. É o item que o motor antigo marcava como "Comprar alta" em outubro.
    /// </summary>
    private static ForecastProductInput EasterEgg(DateOnly today)
    {
        var series = Series((_, month) => month is 2 or 3 ? 150m : 2m, today);
        return Input(series, minStock: 10, productIndex: IndexOf(series, today), price: 50m, cost: 25m, name: "Ovo de Páscoa");
    }

    [Fact]
    [Trait("Rule", "F3.7")]
    public void Should_ReproduceBuyHigh_When_OffSeasonGuardIsOff()
    {
        var result = ForecastCalculator.Calculate(EasterEgg(Today), Context(), new ForecastOptions(OffSeasonGuard: false));

        // O defeito: item de temporada zerado, fora da época, vira Comprar com prioridade Alta.
        result.Action.ShouldBe(RecommendationActionEnum.Buy);
        result.Priority.ShouldBe(RecommendationPriorityEnum.High);
        result.PurchaseNeed.ShouldBeGreaterThan(0m);
    }

    [Fact]
    [Trait("Rule", "F3.7")]
    public void Should_HoldInsteadOfBuyHigh_When_SeasonalItemIsZeroOutOfSeason()
    {
        var result = Calculate(EasterEgg(Today));

        result.OffSeasonGuardActive.ShouldBeTrue();
        result.BlockedByOffSeason.ShouldBeTrue();
        result.SuppressedPurchaseNeed.ShouldBeGreaterThan(0m);
        result.Action.ShouldBe(RecommendationActionEnum.Hold);
        result.Priority.ShouldBe(RecommendationPriorityEnum.Low);
        result.PurchaseNeed.ShouldBe(0m);
        result.Reason.ShouldContain("fora da época");
    }

    [Fact]
    [Trait("Rule", "F3.7")]
    public void Should_BuyAgain_When_SeasonApproaches()
    {
        // 25/01/2027: a janela de compra (10 + 30 dias) já alcança fevereiro e março, a época do item.
        var today = new DateOnly(2027, 1, 25);

        var result = ForecastCalculator.Calculate(EasterEgg(today), Context(today: today));

        result.OffSeasonGuardActive.ShouldBeFalse();
        result.Action.ShouldBe(RecommendationActionEnum.Buy);
        result.PurchaseNeed.ShouldBeGreaterThan(0m);
    }

    [Fact]
    [Trait("Rule", "F3.7")]
    public void Should_HoldInsteadOfBuy_When_NewSeasonalItemSoldOnlyInBurst()
    {
        // Kit de Dia das Mães no 1o ano: 6 meses de histórico, vendas só em 2 meses (há 5 e 4 meses) e nada nos 2 últimos; mínimo 5, estoque 4.
        var series = Series((_, _) => 0m);
        series[^6] = 100;
        series[^5] = 80;
        series[^4] = 5;
        var input = Input(series, monthsOfHistory: 6, stock: 4, minStock: 5);

        var legacy = ForecastCalculator.Calculate(input, Context(), new ForecastOptions(OffSeasonGuard: false));
        var fixedResult = Calculate(input);

        legacy.Action.ShouldBe(RecommendationActionEnum.Buy);
        fixedResult.Action.ShouldBe(RecommendationActionEnum.Hold);
        fixedResult.OffSeasonByBurst.ShouldBeTrue();
        fixedResult.Reason.ShouldContain("sem venda nos 2 últimos meses");
    }

    [Fact]
    [Trait("Rule", "F3.7")]
    public void Should_NotBlockPurchase_When_ProductIsNotSeasonal()
    {
        // Item de venda constante com o estoque zerado continua sendo Comprar: a trava é só para temporada.
        var series = Flat(100);

        var result = Calculate(Input(series, productIndex: IndexOf(series)));

        result.OffSeasonGuardActive.ShouldBeFalse();
        result.Action.ShouldBe(RecommendationActionEnum.Buy);
    }

    [Fact]
    [Trait("Rule", "F3.7")]
    public void Should_KeepBuying_When_NewItemSoldSteadilyThenRanOut()
    {
        // Contraexemplo da trava por concentração: lançamento com venda constante (4 meses de 100) e 2 meses zerados por ruptura.
        // Os 2 maiores meses são só 50% das vendas: não é rajada de temporada, e a compra continua.
        var series = Series((_, _) => 0m);
        series[^6] = series[^5] = series[^4] = series[^3] = 100;

        var result = Calculate(Input(series, monthsOfHistory: 6));

        result.OffSeasonByBurst.ShouldBeFalse();
        result.Action.ShouldBe(RecommendationActionEnum.Buy);
    }

    // ------------------------------------------------------------------ F3.7: previsão zerada vira "Sem ação"

    [Fact]
    [Trait("Rule", "F3.7")]
    public void Should_HoldWithoutAction_When_NoDemandIsForecastInPurchaseWindow()
    {
        // Vendeu 50/mês há mais de um ano e parou: a projeção da janela de compra é zero. O mínimo de 10 sozinho não justifica
        // comprar (o motor antigo marcava "Comprar"); a recomendação fica sem ação, explicando o porquê.
        var series = Series((_, _) => 0m);
        for (var i = 0; i < 12; i++)
            series[i] = 50;

        var result = Calculate(Input(series, minStock: 10));

        result.DemandDuringLeadTimeWindow.ShouldBe(0m);
        result.Action.ShouldBe(RecommendationActionEnum.Hold);
        result.Priority.ShouldBe(RecommendationPriorityEnum.Low);
        result.PurchaseNeed.ShouldBe(0m);
        result.BlockedByNoDemand.ShouldBeTrue();
        result.SuppressedPurchaseNeed.ShouldBe(10m);
        result.Reason.ShouldContain("Sem ação");
    }

    [Fact]
    [Trait("Rule", "F3.7")]
    public void Should_NotBlockPurchase_When_WindowHasSomeDemand()
    {
        // 0,4 kg por mês num produto vendido a quilo (3 casas) ainda é demanda: a regra "sem demanda" não pode esconder a compra.
        var result = Calculate(Input(Flat(0.4m), configure: b => b.DecimalPlaces = 3));

        result.BlockedByNoDemand.ShouldBeFalse();
        result.Action.ShouldBe(RecommendationActionEnum.Buy);
    }
}
