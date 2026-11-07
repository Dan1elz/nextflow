using Nextflow.Application.Services;
using Nextflow.Domain.Dtos.Categories;
using Nextflow.Domain.Dtos.Products;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Models;
using NSubstitute;

namespace Nextflow.Tests.Unit.Inventory;

public class ProductModelTests
{
    private static ProductRequestDto Grid(params string[] sizes) => new()
    {
        Name = " Camiseta ",
        Brand = "  ",
        ProductType = ProductTypeEnum.Apparel,
        SaleUnit = SaleUnitEnum.Unit,
        Attributes =
        [
            new()
            {
                Name = " Tamanho ",
                Values = [.. sizes.Select(size => new ProductAttributeValueRequestDto { TempKey = size, Value = size })],
            },
        ],
        Variants = [.. sizes.Select(size => new ProductVariantRequestDto
        {
            Sku = $" CAM-{size} ",
            Barcode = " ",
            Price = 10m,
            AttributeValueIds = [size],
        })],
    };

    private static ProductRequestDto Simple() => new()
    {
        Name = "Caderno",
        ProductType = ProductTypeEnum.Standard,
        SaleUnit = SaleUnitEnum.Unit,
        Variants = [new() { Sku = "CAD", Price = 5m }],
    };

    /// <summary>O que o formulário devolve ao editar, com as chaves trocadas pelos ids gravados.</summary>
    private static ProductRequestDto EditOf(Product product) => new ProductFormDto(product, null, new ProductFormOptionsDto());

    // ---------- Histórico de preço (F1.4) ----------

    [Fact]
    [Trait("Rule", "RN-CE25")]
    public void Should_RecordPriceHistory_When_VariantPriceChanges()
    {
        var created = new DateOnly(2026, 10, 1);
        var changed = new DateOnly(2026, 11, 1);
        var product = ProductTestData.NewProduct(Simple(), created);
        var variant = product.Variants.Single();
        var dto = EditOf(product);
        dto.Variants[0].Price = 7.5m;

        product.Edit(dto, changed);

        variant.PriceHistory.Count.ShouldBe(2);
        var closed = variant.PriceHistory.Single(h => h.Price == 5m);
        closed.ValidFrom.ShouldBe(created);
        closed.ValidTo.ShouldBe(changed);
        var open = variant.PriceHistory.Single(h => h.Price == 7.5m);
        open.ValidFrom.ShouldBe(changed);
        open.ValidTo.ShouldBeNull();
        open.ChangedById.ShouldBe(ProductTestData.UserId);
    }

    [Fact]
    [Trait("Rule", "RN-CE25")]
    public void Should_NotRecordPriceHistory_When_PriceUnchanged()
    {
        var product = ProductTestData.NewProduct(Simple());
        var variant = product.Variants.Single();
        var dto = EditOf(product);
        dto.Name = "Caderno universitário";

        product.Edit(dto, new DateOnly(2026, 11, 2));

        variant.PriceHistory.Count.ShouldBe(1);
        variant.PriceHistory.Single().ValidTo.ShouldBeNull();
    }

    [Fact]
    [Trait("Rule", "RN-CE48")]
    public void Should_MarkWithoutVariants_When_NoAttributes()
    {
        var product = ProductTestData.NewProduct(Simple());

        product.HasVariants.ShouldBeFalse();
        product.Variants.Single(v => v.RemovedOn == null).AttributeValues.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Rule", "RN-CE48")]
    public void Should_MarkWithVariants_When_HasAttributes()
    {
        var product = ProductTestData.NewProduct(Grid("P", "M", "G"));

        product.HasVariants.ShouldBeTrue();
        product.Variants.Count.ShouldBe(3);
    }

    [Fact]
    [Trait("Rule", "RN-CE50")]
    public void Should_ResolveTempKeyToValueId_When_Creating()
    {
        var product = ProductTestData.NewProduct(Grid("P", "M"));

        var value = product.Attributes.Single().Values.Single(v => v.Value == "M");
        var variant = product.Variants.Single(v => v.Sku == "CAM-M");

        variant.AttributeValues.Single().AttributeValueId.ShouldBe(value.Id);
    }

    [Fact]
    public void Should_TrimAndNullBlank_When_Creating()
    {
        var product = ProductTestData.NewProduct(Grid("P"));

        product.Name.ShouldBe("Camiseta");
        product.Brand.ShouldBeNull();
        product.Attributes.Single().Name.ShouldBe("Tamanho");
        product.Variants.Single(v => v.RemovedOn == null).Sku.ShouldBe("CAM-P");
        product.Variants.Single(v => v.RemovedOn == null).Barcode.ShouldBeNull();
    }

    [Fact]
    public void Should_StartActiveAndInLine_When_Creating()
    {
        var product = ProductTestData.NewProduct(Simple());

        product.IsActive.ShouldBeTrue();
        product.Lifecycle.ShouldBe(LifecycleEnum.Active);
    }

    [Fact]
    public void Should_KeepFormOrder_When_Creating()
    {
        var product = ProductTestData.NewProduct(Grid("G", "P"));

        product.Attributes.Single().Values.Single(v => v.Value == "G").SortOrder.ShouldBe((short)0);
        product.Attributes.Single().Values.Single(v => v.Value == "P").SortOrder.ShouldBe((short)1);
        product.Variants.Single(v => v.Sku == "CAM-P").SortOrder.ShouldBe((short)1);
    }

    [Fact]
    [Trait("Rule", "RN-CE56")]
    public void Should_RecordShelfLifeAsNull_When_NotTrackingLots()
    {
        var dto = Simple();
        dto.ShelfLifeDays = 30;

        ProductTestData.NewProduct(dto).ShelfLifeDays.ShouldBeNull();
    }

    [Fact]
    [Trait("Rule", "RN-CE56")]
    public void Should_RecordShelfLife_When_TrackingLots()
    {
        var dto = Simple();
        dto.TracksLots = true;
        dto.ShelfLifeDays = 30;

        ProductTestData.NewProduct(dto).ShelfLifeDays.ShouldBe(30);
    }

    [Fact]
    [Trait("Rule", "RN-CE51")]
    public void Should_RemoveVariant_When_ItLeavesTheForm()
    {
        var product = ProductTestData.NewProduct(Grid("P", "M"));
        var kept = product.Variants.Single(v => v.Sku == "CAM-P").Id;
        var dto = EditOf(product);
        dto.Variants = [.. dto.Variants.Where(v => v.Id == kept)];

        product.Edit(dto);

        product.Variants.Single(v => v.RemovedOn == null).Id.ShouldBe(kept);
    }

    [Fact]
    public void Should_UpdateVariantInPlace_When_IdComesInForm()
    {
        var product = ProductTestData.NewProduct(Simple());
        var id = product.Variants.Single(v => v.RemovedOn == null).Id;
        var dto = EditOf(product);
        dto.Variants[0].Price = 99m;
        dto.Variants[0].IsActive = true;

        product.Edit(dto);

        product.Variants.Single(v => v.RemovedOn == null).Id.ShouldBe(id);
        product.Variants.Single(v => v.RemovedOn == null).Price.ShouldBe(99m);
    }

    [Fact]
    [Trait("Rule", "RN-CE51")]
    public void Should_RemoveValue_When_ItLeavesTheForm()
    {
        var product = ProductTestData.NewProduct(Grid("P", "M"));
        var kept = product.Attributes.Single().Values.Single(v => v.Value == "P").Id;
        var dto = EditOf(product);
        dto.Attributes[0].Values = [.. dto.Attributes[0].Values.Where(v => v.Id == kept)];
        dto.Variants = [.. dto.Variants.Where(v => v.AttributeValueIds.Contains(kept.ToString()))];

        product.Edit(dto);

        product.Attributes.Single().Values.Single().Id.ShouldBe(kept);
        product.Variants.Single(v => v.RemovedOn == null).AttributeValues.Single().AttributeValueId.ShouldBe(kept);
    }

    [Fact]
    [Trait("Rule", "RN-CE51")]
    public void Should_RemoveAttributeAndValues_When_GridIsTurnedOff()
    {
        var product = ProductTestData.NewProduct(Grid("P", "M"));
        var keptId = product.Variants.First().Id;
        var dto = EditOf(product);
        dto.Attributes = [];
        dto.Variants = [new() { Id = keptId, Sku = "CAM", Price = 10m }];

        product.Edit(dto);

        product.HasVariants.ShouldBeFalse();
        product.Attributes.ShouldBeEmpty();
        product.Variants.Single(v => v.RemovedOn == null).Id.ShouldBe(keptId);
        product.Variants.Single(v => v.RemovedOn == null).AttributeValues.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Rule", "RN-CE50")]
    public void Should_AddValueByTempKey_When_EditingExistingAttribute()
    {
        var product = ProductTestData.NewProduct(Grid("P"));
        var existingValueId = product.Attributes.Single().Values.Single().Id;
        var dto = EditOf(product);
        dto.Attributes[0].Values.Add(new() { TempKey = "novo", Value = "M" });
        dto.Variants.Add(new() { Sku = "CAM-M", Price = 10m, AttributeValueIds = ["novo"] });

        product.Edit(dto);

        product.Attributes.Single().Values.Count.ShouldBe(2);
        product.Variants.Count.ShouldBe(2);
        product.Attributes.Single().Values.ShouldContain(v => v.Id == existingValueId);
        var added = product.Attributes.Single().Values.Single(v => v.Value == "M");
        product.Variants.Single(v => v.Sku == "CAM-M").AttributeValues.Single().AttributeValueId.ShouldBe(added.Id);
    }

    [Fact]
    public void Should_ChangeCombination_When_VariantPointsToAnotherValue()
    {
        var product = ProductTestData.NewProduct(Grid("P", "M"));
        var dto = EditOf(product);
        var variant = dto.Variants[0];
        var other = dto.Attributes[0].Values.Single(v => v.Id.ToString() != variant.AttributeValueIds[0]);
        dto.Variants = [variant];
        variant.AttributeValueIds = [other.Id.ToString()!];

        product.Edit(dto);

        product.Variants.Single(v => v.RemovedOn == null).AttributeValues.Single().AttributeValueId.ShouldBe(other.Id!.Value);
    }

    [Fact]
    public void Should_SyncCategories_When_Updating()
    {
        var stays = Guid.NewGuid();
        var leaves = Guid.NewGuid();
        var arrives = Guid.NewGuid();
        var dto = Simple();
        dto.CategoryIds = [stays, leaves, leaves];
        var product = ProductTestData.NewProduct(dto);
        product.Categories.Count.ShouldBe(2);
        var stayingLink = product.Categories.Single(c => c.CategoryId == stays);

        var edit = EditOf(product);
        edit.CategoryIds = [stays, arrives];
        product.Edit(edit);

        product.Categories.Select(c => c.CategoryId).ShouldBe([stays, arrives], ignoreOrder: true);
        product.Categories.ShouldContain(stayingLink);
    }

    [Fact]
    [Trait("Rule", "RN-CE55")]
    public void Should_KeepGridAndVariants_When_TypeChanges()
    {
        var product = ProductTestData.NewProduct(Grid("P", "M"));
        var ids = product.Variants.Select(v => v.Id).ToList();
        var dto = EditOf(product);
        dto.ProductType = ProductTypeEnum.Standard;

        product.Edit(dto);

        product.ProductType.ShouldBe(ProductTypeEnum.Standard);
        product.HasVariants.ShouldBeTrue();
        product.Variants.Select(v => v.Id).ShouldBe(ids, ignoreOrder: true);
    }

    [Fact]
    [Trait("Rule", "RN-CE49")]
    public void Should_FollowFormOrder_When_EditingMovesAttributes()
    {
        var create = Grid("P");
        create.Attributes.Add(new() { Name = "Cor", Values = [new() { TempKey = "Azul", Value = "Azul" }] });
        create.Variants[0].AttributeValueIds = ["P", "Azul"];
        var product = ProductTestData.NewProduct(create);
        var dto = EditOf(product);
        dto.Attributes.Reverse();

        product.Edit(dto);

        product.Attributes.Single(a => a.Name == "Cor").SortOrder.ShouldBe((short)0);
        product.Attributes.Single(a => a.Name == "Tamanho").SortOrder.ShouldBe((short)1);
    }

    [Fact]
    [Trait("Rule", "RN-CE49")]
    public void Should_FollowFormOrder_When_EditingMovesValues()
    {
        var product = ProductTestData.NewProduct(Grid("P", "M"));
        var dto = EditOf(product);
        dto.Attributes[0].Values.Reverse();

        product.Edit(dto);

        var values = product.Attributes.Single().Values;
        values.Single(v => v.Value == "M").SortOrder.ShouldBe((short)0);
        values.Single(v => v.Value == "P").SortOrder.ShouldBe((short)1);
    }

    [Fact]
    public void Should_FollowFormOrder_When_EditingMovesVariants()
    {
        var product = ProductTestData.NewProduct(Grid("P", "M"));
        var dto = EditOf(product);
        dto.Variants.Reverse();

        product.Edit(dto);

        product.Variants.Single(v => v.Sku == "CAM-M").SortOrder.ShouldBe((short)0);
        product.Variants.Single(v => v.Sku == "CAM-P").SortOrder.ShouldBe((short)1);
    }

    [Fact]
    public void Should_RenameInPlace_When_EditingValueAndAttribute()
    {
        var product = ProductTestData.NewProduct(Grid("P"));
        var valueId = product.Attributes.Single().Values.Single().Id;
        var dto = EditOf(product);
        dto.Attributes[0].Name = "Numeração";
        dto.Attributes[0].Values[0].Value = " Pequeno ";

        product.Edit(dto);

        product.Attributes.Single().Name.ShouldBe("Numeração");
        var value = product.Attributes.Single().Values.Single();
        value.Id.ShouldBe(valueId);
        value.Value.ShouldBe("Pequeno");
    }

    [Fact]
    [Trait("Rule", "RN-CE54")]
    public void Should_KeepVariantInactive_When_EditingWithIsActiveFalse()
    {
        var product = ProductTestData.NewProduct(Grid("P", "M"));
        var dto = EditOf(product);
        dto.Variants[0].IsActive = false;

        product.Edit(dto);

        product.Variants.Single(v => v.Id == dto.Variants[0].Id).IsActive.ShouldBeFalse();
        product.Variants.Single(v => v.Id == dto.Variants[1].Id).IsActive.ShouldBeTrue();
    }

    [Fact]
    [Trait("Rule", "RN-CE50")]
    public void Should_ResolveCombination_When_ValueIdComesInUppercase()
    {
        var product = ProductTestData.NewProduct(Grid("P", "M"));
        var dto = EditOf(product);
        var target = dto.Attributes[0].Values.Single(v => v.Value == "M").Id!.Value;
        dto.Variants = [dto.Variants[0]];
        dto.Variants[0].AttributeValueIds = [target.ToString().ToUpperInvariant()];

        product.Edit(dto);

        product.Variants.Single(v => v.RemovedOn == null).AttributeValues.Single().AttributeValueId.ShouldBe(target);
    }

    [Fact]
    [Trait("Rule", "RN-CE19")]
    public void Should_ChangeLifecycle_When_Asked()
    {
        var product = ProductTestData.NewProduct(Simple());

        product.ChangeLifecycle(LifecycleEnum.Discontinued);

        product.Lifecycle.ShouldBe(LifecycleEnum.Discontinued);
        product.UpdatedAt.ShouldNotBeNull();
    }

    [Fact]
    public void Should_SetAndRemoveImageKey_When_Asked()
    {
        var product = ProductTestData.NewProduct(Simple());

        product.SetImage("products/a.png");
        product.ImageKey.ShouldBe("products/a.png");

        product.RemoveImage();
        product.ImageKey.ShouldBeNull();
    }
}

public class ProductDtoMappingTests
{
    private static Product GridProduct(params (string Size, decimal Price, bool Active)[] rows) => ProductTestData.NewProduct(new ProductRequestDto
    {
        Name = "Camiseta",
        ProductType = ProductTypeEnum.Apparel,
        SaleUnit = SaleUnitEnum.Kilogram,
        DecimalPlaces = 3,
        Attributes =
        [
            new()
            {
                Name = "Tamanho",
                Values = [.. rows.Select(r => new ProductAttributeValueRequestDto { TempKey = r.Size, Value = r.Size })],
            },
        ],
        Variants = [.. rows.Select(r => new ProductVariantRequestDto
        {
            Sku = r.Size,
            Price = r.Price,
            IsActive = r.Active,
            AttributeValueIds = [r.Size],
        })],
    });

    private static readonly ProductPricingService Pricing = new(ProductTestData.Clock());

    [Fact]
    [Trait("Rule", "RN-VD02")]
    public void Should_UseVariantPriceAsEffective_When_NoPromotionExistsYet()
    {
        var variant = GridProduct(("P", 12.5m, true)).Variants.Single();

        Pricing.GetEffectivePrice(variant).ShouldBe(12.5m);
    }

    [Fact]
    public void Should_RangeOverActiveVariants_When_MappingListItem()
    {
        var product = GridProduct(("P", 10m, true), ("M", 30m, true), ("G", 500m, false));

        var dto = new ProductListItemDto(product, "http://img", Pricing);

        dto.MinEffectivePrice.ShouldBe(10m);
        dto.MaxEffectivePrice.ShouldBe(30m);
        dto.VariantCount.ShouldBe(2);
        dto.ImageUrl.ShouldBe("http://img");
        dto.ProductType.ShouldBe("Apparel");
        dto.SaleUnit.ShouldBe("Kilogram");
        dto.DecimalPlaces.ShouldBe((short)3);
        dto.HasVariants.ShouldBeTrue();
    }

    [Fact]
    public void Should_RangeOverActiveVariants_When_MappingDetail()
    {
        var product = GridProduct(("P", 10m, true), ("M", 30m, true), ("G", 500m, false), ("GG", 1m, false));

        var dto = new ProductResponseDto(product, null, Pricing);

        dto.MinEffectivePrice.ShouldBe(10m);
        dto.MaxEffectivePrice.ShouldBe(30m);
        dto.VariantCount.ShouldBe(2);
    }

    [Fact]
    [Trait("Rule", "RN-VD02")]
    public void Should_AskPricingService_When_MappingEffectivePrice()
    {
        var product = ProductTestData.NewProduct(new ProductRequestDto { Name = "Caderno", ProductType = ProductTypeEnum.Standard, SaleUnit = SaleUnitEnum.Unit, Variants = [new() { Sku = "A", Price = 10m }] });
        var pricing = Substitute.For<IProductPricingService>();
        pricing.GetEffectivePrice(Arg.Any<ProductVariant>()).Returns(7m);

        new ProductListItemDto(product, null, pricing).MinEffectivePrice.ShouldBe(7m);
        new ProductResponseDto(product, null, pricing).MaxEffectivePrice.ShouldBe(7m);
        new ProductVariantResponseDto(product.Variants.Single(), pricing).EffectivePrice.ShouldBe(7m);
    }

    [Fact]
    public void Should_MapWithoutCategories_When_NavigationNotLoaded()
    {
        var product = ProductTestData.NewProduct(new ProductRequestDto
        {
            Name = "Caderno",
            ProductType = ProductTypeEnum.Standard,
            SaleUnit = SaleUnitEnum.Unit,
            CategoryIds = [Guid.NewGuid()],
            Variants = [new() { Sku = "A", Price = 10m }],
        });

        new ProductListItemDto(product, null, Pricing).CategoryPath.ShouldBeNull();
        new ProductResponseDto(product, null, Pricing).Categories.ShouldBeEmpty();
    }

    [Fact]
    public void Should_MapEmptyLabel_When_ValueNavigationNotLoaded()
    {
        var variant = GridProduct(("P", 10m, true)).Variants.Single();

        new ProductVariantResponseDto(variant, Pricing).Label.ShouldBeEmpty();
    }

    [Fact]
    public void Should_MapZeroStockAndNoCost_When_VariantHasNoMovement()
    {
        var product = ProductTestData.NewProduct(new ProductRequestDto { Name = "Caderno", ProductType = ProductTypeEnum.Standard, SaleUnit = SaleUnitEnum.Unit, Variants = [new() { Sku = "A", Price = 10m }] });

        var item = new ProductListItemDto(product, null, Pricing);
        item.AvailableQuantity.ShouldBe(0);
        item.HasLowStock.ShouldBeFalse();
        item.ActivePromotionPercent.ShouldBeNull();

        var detail = new ProductResponseDto(product, null, Pricing);
        detail.AvailableQuantity.ShouldBe(0);
        detail.MinAverageCost.ShouldBeNull();
        detail.MaxAverageCost.ShouldBeNull();

        var variant = new ProductVariantResponseDto(product.Variants.Single(), Pricing);
        variant.AverageCost.ShouldBeNull();
        variant.MarginPercent.ShouldBeNull();
    }
}
