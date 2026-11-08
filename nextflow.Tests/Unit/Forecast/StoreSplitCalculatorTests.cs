using Nextflow.Application.Forecast;

namespace Nextflow.Tests.Unit.Forecast;

/// <summary>Divisão da compra entre lojas (RN-DF27, F3.9).</summary>
public class StoreSplitCalculatorTests
{
    private static readonly Guid Centro = Guid.NewGuid();
    private static readonly Guid Shopping = Guid.NewGuid();

    [Fact]
    [Trait("Rule", "RN-DF27")]
    public void Should_SplitByShortfall_When_StoresLackStock()
    {
        // Falta 100 no Centro e 200 no Shopping: 300 un. viram 100 + 200, mesmo com a participação invertida.
        var parts = StoreSplitCalculator.Split(300m, [new(Centro, 70m, 100m), new(Shopping, 30m, 200m)], null);

        parts.ShouldBe([(Centro, 100m), (Shopping, 200m)]);
    }

    [Fact]
    [Trait("Rule", "RN-DF27")]
    public void Should_SplitByShare_When_NoStoreLacks()
    {
        var parts = StoreSplitCalculator.Split(100m, [new(Centro, 60m, 0m), new(Shopping, 40m, 0m)], null);

        parts.ShouldBe([(Centro, 60m), (Shopping, 40m)]);
    }

    [Fact]
    [Trait("Rule", "RN-DF27")]
    public void Should_RoundToMultiple_When_SupplierHasMultiple()
    {
        // 120 em múltiplos de 12 com pesos 1:2: 40 e 80 descem para 36 e 72, e o resto (12) vai para o maior peso → 36 + 84.
        var parts = StoreSplitCalculator.Split(120m, [new(Centro, 0m, 10m), new(Shopping, 0m, 20m)], 12m);

        parts.ShouldBe([(Centro, 36m), (Shopping, 84m)]);
        parts.Sum(p => p.Quantity).ShouldBe(120m);
        parts.ShouldAllBe(p => p.Quantity % 12m == 0);
    }

    [Fact]
    [Trait("Rule", "RN-DF27")]
    public void Should_GiveZero_When_StoreHasNoSalesNorShortfall()
    {
        var parts = StoreSplitCalculator.Split(50m, [new(Centro, 100m, 30m), new(Shopping, 0m, 0m)], null);

        parts.ShouldBe([(Centro, 50m), (Shopping, 0m)]);
    }

    [Fact]
    public void Should_ComputeShortfallAsShareOfDemandMinusStock_When_Calculating()
    {
        StoreSplitCalculator.Shortfall(300m, 40m, 50m, 20m).ShouldBe(50m);
        StoreSplitCalculator.Shortfall(300m, 40m, 200m, 0m).ShouldBe(0m);
    }
}
