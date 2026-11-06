using Nextflow.Domain.Dtos.Products;
using Nextflow.Domain.Dtos.Stock;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Validators.Catalog;
using Nextflow.Domain.Validators.Stock;
using Nextflow.Tests.Unit.Inventory;

namespace Nextflow.Tests.Unit.Stock;

/// <summary>Mínimo efetivo por variação × local (RN-CE33) e formato da política ABC (RN-CE29), sem banco.</summary>
public class StockMinimumTests
{
    private static readonly Guid Shop = Guid.Parse("00000000-0000-0000-0000-00000000c001");
    private static readonly Guid Deposit = Guid.Parse("00000000-0000-0000-0000-00000000c002");

    private static ProductLocationMinimumDto Min(int variantIndex, Guid locationId, decimal minQuantity)
        => new() { VariantIndex = variantIndex, LocationId = locationId, MinQuantity = minQuantity };

    private static ProductRequestDto Grid(decimal defaultMin, params ProductLocationMinimumDto[] minimums) => new()
    {
        Name = "Camiseta",
        ProductType = ProductTypeEnum.Apparel,
        SaleUnit = SaleUnitEnum.Unit,
        DefaultMinQuantity = defaultMin,
        Attributes =
        [
            new()
            {
                Name = "Tamanho",
                Values = [new() { TempKey = "P", Value = "P" }, new() { TempKey = "M", Value = "M" }],
            },
        ],
        Variants =
        [
            new() { Sku = "CAM-P", Price = 10m, AttributeValueIds = ["P"] },
            new() { Sku = "CAM-M", Price = 10m, AttributeValueIds = ["M"] },
        ],
        LocationMinimums = [.. minimums],
    };

    [Fact]
    [Trait("Rule", "RN-CE33")]
    public void Should_UseProductDefault_When_NoLocationMinimum()
    {
        var product = ProductTestData.NewProduct(Grid(5, Min(1, Deposit, 2)));
        var small = product.Variants.Single(v => v.Sku == "CAM-P");
        var medium = product.Variants.Single(v => v.Sku == "CAM-M");

        product.EffectiveMinimum(medium, Deposit).ShouldBe(2m);
        product.EffectiveMinimum(medium, Shop).ShouldBe(5m);
        product.EffectiveMinimum(small, Deposit).ShouldBe(5m);
    }

    [Theory]
    [Trait("Rule", "RN-CE33")]
    [InlineData(1.9, true)]
    [InlineData(2, false)]
    [InlineData(10, false)]
    public void Should_FlagLowStock_When_AvailableBelowEffectiveMinimum(decimal available, bool expected)
    {
        var product = ProductTestData.NewProduct(Grid(5, Min(0, Deposit, 2)));
        var small = product.Variants.Single(v => v.Sku == "CAM-P");

        product.IsBelowMinimum(small, Deposit, available).ShouldBe(expected);
    }

    [Fact]
    [Trait("Rule", "RN-CE33")]
    public void Should_ReplaceMinimums_When_EditingProduct()
    {
        var product = ProductTestData.NewProduct(Grid(5, Min(0, Deposit, 2)));
        var small = product.Variants.Single(v => v.Sku == "CAM-P");

        var edit = Grid(3, Min(0, Shop, 1));
        edit.Variants[0].Id = small.Id;
        edit.Variants[1].Id = product.Variants.Single(v => v.Sku == "CAM-M").Id;
        product.Edit(edit);

        product.DefaultMinQuantity.ShouldBe(3m);
        small.LocationMinimums.Select(m => (m.LocationId, m.MinQuantity)).ShouldBe([(Shop, 1m)]);
        product.EffectiveMinimum(small, Deposit).ShouldBe(3m);
    }

    [Fact]
    [Trait("Rule", "RN-CE33")]
    public void Should_RejectMinimums_When_InvalidFormat()
    {
        var dto = Grid(-1,
            Min(2, Shop, 1),
            Min(0, Shop, -1),
            Min(0, Shop, 1));
        dto.TargetCoverageDays = 0;

        var result = new ProductRequestDtoValidator().Validate(dto);

        var fields = result.Errors.Select(e => e.PropertyName).ToList();
        fields.ShouldContain("DefaultMinQuantity");
        fields.ShouldContain("TargetCoverageDays");
        fields.ShouldContain("LocationMinimums[0].VariantIndex");
        fields.ShouldContain("LocationMinimums[1].MinQuantity");
        fields.ShouldContain("LocationMinimums[2].LocationId");
    }

    [Theory]
    [Trait("Rule", "RN-CE29")]
    [InlineData(0, 0, true)]
    [InlineData(30, 7, true)]
    [InlineData(-1, 7, false)]
    [InlineData(30, -1, false)]
    [InlineData(366, 7, false)]
    public void Should_ValidatePolicyDays_When_Updating(int coverage, int safety, bool valid)
    {
        var result = new StockPolicyRequestDtoValidator().Validate(new StockPolicyRequestDto { TargetCoverageDays = coverage, SafetyDays = safety });

        result.IsValid.ShouldBe(valid);
    }
}
