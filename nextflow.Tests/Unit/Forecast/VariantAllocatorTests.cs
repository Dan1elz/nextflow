using Nextflow.Application.Forecast;
using Nextflow.Domain.Enums;

namespace Nextflow.Tests.Unit.Forecast;

/// <summary>Rateio da quantidade nas variações (RN-DF21, L-DF5) e classe ABC (RN-CE29).</summary>
public class VariantAllocatorTests
{
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();
    private static readonly Guid C = Guid.NewGuid();

    [Fact]
    [Trait("Rule", "RN-DF21")]
    public void Should_SplitByHistoricalShare()
    {
        // Vendas históricas 50, 30 e 20: 50% / 30% / 20% de 100 un.
        var parts = VariantAllocator.Split(100m, [(A, 50m), (B, 30m), (C, 20m)], 0);

        parts.Select(p => p.Quantity).ShouldBe([50m, 30m, 20m]);
        parts.Select(p => p.SharePercent).ShouldBe([50m, 30m, 20m]);
    }

    [Fact]
    [Trait("Rule", "L-DF5")]
    public void Should_SplitEqually_When_NoVariantHasHistory()
    {
        var parts = VariantAllocator.Split(90m, [(A, 0m), (B, 0m), (C, 0m)], 0);

        parts.Select(p => p.Quantity).ShouldBe([30m, 30m, 30m]);
    }

    [Fact]
    [Trait("Rule", "L-DF5")]
    public void Should_GiveAverageShare_When_VariantHasNoHistory()
    {
        // A e B vendem 60 e 20 (média 40); C é tamanho novo e entra com a média: pesos 60/20/40 de 120.
        var parts = VariantAllocator.Split(120m, [(A, 60m), (B, 20m), (C, 0m)], 0);

        parts.Select(p => p.Quantity).ShouldBe([60m, 20m, 40m]);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(157)]
    [InlineData(1)]
    [Trait("Rule", "RN-DF21")]
    public void Should_SumExactlyToTotal_When_RoundingUnits(int total)
    {
        var parts = VariantAllocator.Split(total, [(A, 35m), (B, 40m), (C, 25m)], 0);

        parts.Sum(p => p.Quantity).ShouldBe(total);
        parts.ShouldAllBe(p => p.Quantity == Math.Floor(p.Quantity)); // unidade inteira
    }

    [Fact]
    [Trait("Rule", "RN-DF21")]
    public void Should_RespectDecimalPlaces_When_ProductIsSoldByWeight()
    {
        var parts = VariantAllocator.Split(10m, [(A, 1m), (B, 1m), (C, 1m)], 2);

        parts.Sum(p => p.Quantity).ShouldBe(10m);
        parts.ShouldAllBe(p => p.Quantity == Math.Round(p.Quantity, 2));
    }

    [Fact]
    [Trait("Rule", "RN-DF21")]
    public void Should_ReturnNothing_When_ThereIsNoQuantityOrVariant()
    {
        VariantAllocator.Split(0m, [(A, 1m)], 0).ShouldBeEmpty();
        VariantAllocator.Split(10m, [], 0).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Rule", "RN-CE29")]
    public void Should_ClassifyByRevenueShare()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var p3 = Guid.NewGuid();
        var p4 = Guid.NewGuid();
        var p5 = Guid.NewGuid();

        // Receita 1000: 700 (A), 180 (acumulado 70% antes → A), 90 (88% antes → B), 30 (97% antes → C), 0 (C).
        var classes = AbcClassifier.Classify([(p1, 700m), (p2, 180m), (p3, 90m), (p4, 30m), (p5, 0m)]);

        classes[p1].ShouldBe(AbcClassEnum.A);
        classes[p2].ShouldBe(AbcClassEnum.A);
        classes[p3].ShouldBe(AbcClassEnum.B);
        classes[p4].ShouldBe(AbcClassEnum.C);
        classes[p5].ShouldBe(AbcClassEnum.C);
    }

    [Fact]
    [Trait("Rule", "RN-CE29")]
    public void Should_ClassifyAllAsC_When_NoProductHasRevenue()
    {
        var ids = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToList();

        AbcClassifier.Classify(ids.Select(id => (id, 0m))).Values.ShouldAllBe(c => c == AbcClassEnum.C);
    }
}
