using Nextflow.Application.Services;
using Nextflow.Domain.Dtos.Products;
using Nextflow.Domain.Dtos.Promotions;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;
using Nextflow.Tests.Unit.Inventory;

namespace Nextflow.Tests.Unit.Sales;

/// <summary>Preço efetivo com a promoção vigente (RN-VD02, RN-VD12), sobre o grafo carregado, na data de negócio.</summary>
public class PromotionPricingTests
{
    private static readonly DateOnly Today = ProductTestData.Today;
    private static readonly ProductPricingService Pricing = new(ProductTestData.Clock());

    private static Product TwoVariants() => ProductTestData.NewProduct(new ProductRequestDto
    {
        Name = "Trufa",
        ProductType = ProductTypeEnum.Standard,
        SaleUnit = SaleUnitEnum.Unit,
        Variants = [new() { Sku = "TRU-A", Price = 10m }, new() { Sku = "TRU-B", Price = 12.99m }],
    });

    private static Promotion Promote(Product product, decimal percent, DateOnly start, DateOnly end, Guid? variantId = null)
    {
        var promotion = new Promotion(new PromotionRequestDto
        {
            ProductId = product.Id,
            VariantId = variantId,
            DiscountPercent = percent,
            StartDate = start,
            EndDate = end,
        }, ProductTestData.UserId, belowCostConfirmed: false);
        product.Promotions.Add(promotion);
        return promotion;
    }

    [Fact]
    [Trait("Rule", "RN-VD02")]
    public void Should_ApplyActivePromotion_When_CalculatingEffectivePrice()
    {
        var product = TwoVariants();
        var (a, b) = (product.Variants.First(), product.Variants.Last());
        var promotion = Promote(product, 15m, Today, Today.AddDays(5));

        // 12,99 × 0,85 = 11,0415 → 11,04 (duas casas, meio para cima).
        Pricing.GetEffectivePrice(a).ShouldBe(8.5m);
        Pricing.GetEffectivePrice(b).ShouldBe(11.04m);
        Pricing.GetActivePromotion(a).ShouldBe(promotion);

        var other = TwoVariants();
        Promote(other, 50m, Today.AddDays(-3), Today, other.Variants.Last().Id);
        Pricing.GetEffectivePrice(other.Variants.First()).ShouldBe(10m);
        Pricing.GetEffectivePrice(other.Variants.Last()).ShouldBe(6.5m);

        new ProductListItemDto(other, null, Pricing).ActivePromotionPercent.ShouldBe(50m);
        new ProductResponseDto(other, null, Pricing).ActivePromotionPercent.ShouldBe(50m);
        new ProductVariantResponseDto(other.Variants.Last(), Pricing).ActivePromotion!.DiscountPercent.ShouldBe(50m);
    }

    [Fact]
    [Trait("Rule", "RN-VD12")]
    public void Should_IgnorePromotion_When_NotCurrentOrCanceled()
    {
        var scheduled = TwoVariants();
        Promote(scheduled, 20m, Today.AddDays(1), Today.AddDays(10));

        var ended = TwoVariants();
        Promote(ended, 20m, Today.AddDays(-10), Today.AddDays(-1));

        var canceled = TwoVariants();
        Promote(canceled, 20m, Today, Today.AddDays(10)).Cancel();

        foreach (var product in new[] { scheduled, ended, canceled })
        {
            var variant = product.Variants.First();
            Pricing.GetEffectivePrice(variant).ShouldBe(10m);
            Pricing.GetActivePromotion(variant).ShouldBeNull();
            new ProductListItemDto(product, null, Pricing).ActivePromotionPercent.ShouldBeNull();
        }

        scheduled.Promotions.Single().StatusOn(Today).ShouldBe(PromotionStatusEnum.Scheduled);
        ended.Promotions.Single().StatusOn(Today).ShouldBe(PromotionStatusEnum.Ended);
        canceled.Promotions.Single().StatusOn(Today).ShouldBe(PromotionStatusEnum.Canceled);
        scheduled.Promotions.Single().StatusOn(Today.AddDays(10)).ShouldBe(PromotionStatusEnum.Active);
    }
}
