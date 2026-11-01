using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Products;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Validators.Catalog;

namespace Nextflow.Tests.Unit.Inventory;

public class ProductRequestValidatorTests
{
    private readonly ProductRequestDtoValidator _validator = new();

    private static ProductVariantRequestDto Variant(string sku, params string[] values) => new()
    {
        Sku = sku,
        Price = 10m,
        AttributeValueIds = [.. values],
    };

    private static ProductRequestDto Simple() => new()
    {
        Name = "Caderno",
        ProductType = ProductTypeEnum.Standard,
        SaleUnit = SaleUnitEnum.Unit,
        Variants = [Variant("CAD-1")],
    };

    private static ProductAttributeRequestDto Attribute(string name, params string[] values) => new()
    {
        Name = name,
        Values = [.. values.Select(value => new ProductAttributeValueRequestDto { TempKey = value, Value = value })],
    };

    private static ProductRequestDto Grid(params string[] sizes) => new()
    {
        Name = "Camiseta",
        ProductType = ProductTypeEnum.Apparel,
        SaleUnit = SaleUnitEnum.Unit,
        Attributes = [Attribute("Tamanho", sizes)],
        Variants = [.. sizes.Select(size => Variant($"CAM-{size}", size))],
    };

    private void ShouldFailOn(ProductRequestDto dto, string field)
    {
        var result = _validator.Validate(dto);
        result.Errors.ShouldContain(e => e.PropertyName == field, $"esperava erro em {field}");
    }

    [Fact]
    public void Should_AcceptProduct_When_NoGridAndOneVariant()
        => _validator.Validate(Simple()).IsValid.ShouldBeTrue();

    [Fact]
    public void Should_AcceptProduct_When_GridHasOneVariantPerValue()
        => _validator.Validate(Grid("P", "M", "G")).IsValid.ShouldBeTrue();

    [Fact]
    public void Should_FailOnName_When_Empty()
    {
        var dto = Simple();
        dto.Name = " ";
        ShouldFailOn(dto, "Name");
    }

    [Fact]
    public void Should_FailOnProductType_When_NotInformed()
    {
        var dto = Simple();
        dto.ProductType = 0;
        ShouldFailOn(dto, "ProductType");
    }

    [Fact]
    public void Should_FailOnSaleUnit_When_OutOfEnum()
    {
        var dto = Simple();
        dto.SaleUnit = (SaleUnitEnum)9;
        ShouldFailOn(dto, "SaleUnit");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void Should_FailOnDecimalPlaces_When_OutOfRange(short places)
    {
        var dto = Simple();
        dto.DecimalPlaces = places;
        ShouldFailOn(dto, "DecimalPlaces");
    }

    [Fact]
    public void Should_AcceptDecimalPlaces_When_AtLimit()
    {
        var dto = Simple();
        dto.DecimalPlaces = 3;
        _validator.Validate(dto).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Should_FailOnVariants_When_Empty()
    {
        var dto = Simple();
        dto.Variants = [];
        ShouldFailOn(dto, "Variants");
    }

    [Fact]
    [Trait("Rule", "RN-CE48")]
    public void Should_FailOnVariants_When_NoGridAndTwoVariants()
    {
        var dto = Simple();
        dto.Variants = [Variant("A"), Variant("B")];
        ShouldFailOn(dto, "Variants");
    }

    [Fact]
    [Trait("Rule", "RN-CE48")]
    public void Should_FailOnVariants_When_NoGridAndVariantHasCombination()
    {
        var dto = Simple();
        dto.Variants = [Variant("A", "P")];
        ShouldFailOn(dto, "Variants");
    }

    [Fact]
    [Trait("Rule", "RN-CE49")]
    public void Should_FailOnAttributeValues_When_AttributeHasNoValues()
    {
        var dto = Grid("P");
        dto.Attributes.Add(Attribute("Cor"));
        ShouldFailOn(dto, "Attributes[1].Values");
    }

    [Fact]
    [Trait("Rule", "RN-CE49")]
    public void Should_FailOnAttributes_When_NameRepeatedIgnoringCase()
    {
        var dto = Grid("P");
        dto.Attributes.Add(Attribute("TAMANHO", "X"));
        dto.Variants = [Variant("A", "P", "X")];
        ShouldFailOn(dto, "Attributes");
    }

    [Fact]
    [Trait("Rule", "RN-CE49")]
    public void Should_FailOnValues_When_ValueRepeatedIgnoringCase()
    {
        var dto = Grid("P");
        dto.Attributes[0].Values.Add(new ProductAttributeValueRequestDto { TempKey = "p2", Value = "p" });
        ShouldFailOn(dto, "Attributes[0].Values");
    }

    [Fact]
    [Trait("Rule", "RN-CE49")]
    public void Should_FailOnCombination_When_VariantMissesAnAttribute()
    {
        var dto = Grid("P");
        dto.Attributes.Add(Attribute("Cor", "Azul"));
        ShouldFailOn(dto, "Variants[0].AttributeValueIds");
    }

    [Fact]
    [Trait("Rule", "RN-CE49")]
    public void Should_FailOnCombination_When_TwoValuesOfSameAttribute()
    {
        var dto = Grid("P", "M");
        dto.Attributes.Add(Attribute("Cor", "Azul"));
        dto.Variants = [Variant("A", "P", "M")];
        ShouldFailOn(dto, "Variants[0].AttributeValueIds");
    }

    [Fact]
    [Trait("Rule", "RN-CE49")]
    public void Should_FailOnCombination_When_Repeated()
    {
        var dto = Grid("P", "M");
        dto.Variants = [Variant("A", "P"), Variant("B", "P")];
        ShouldFailOn(dto, "Variants[1].AttributeValueIds");
    }

    [Fact]
    [Trait("Rule", "RN-CE49")]
    public void Should_AcceptGrid_When_NotEveryCombinationExists()
    {
        var dto = Grid("P", "M");
        dto.Variants = [Variant("A", "P")];
        _validator.Validate(dto).IsValid.ShouldBeTrue();
    }

    [Fact]
    [Trait("Rule", "RN-CE50")]
    public void Should_FailOnTempKey_When_NewValueWithoutKey()
    {
        var dto = Grid("P");
        dto.Attributes[0].Values[0].TempKey = null;
        ShouldFailOn(dto, "Attributes[0].Values[0].TempKey");
    }

    [Fact]
    [Trait("Rule", "RN-CE50")]
    public void Should_FailOnAttributes_When_TempKeyRepeatedAcrossAttributes()
    {
        var dto = Grid("P");
        dto.Attributes.Add(new ProductAttributeRequestDto
        {
            Name = "Cor",
            Values = [new() { TempKey = "P", Value = "Azul" }],
        });
        ShouldFailOn(dto, "Attributes");
    }

    [Fact]
    [Trait("Rule", "RN-CE50")]
    public void Should_FailOnCombination_When_ReferenceIsNotInRequest()
    {
        var dto = Grid("P");
        dto.Variants[0].AttributeValueIds = ["nao-existe"];
        ShouldFailOn(dto, "Variants[0].AttributeValueIds");
    }

    [Fact]
    [Trait("Rule", "RN-CE50")]
    public void Should_FailOnCombination_When_ReferenceIsNull()
    {
        var dto = Grid("P");
        dto.Variants[0].AttributeValueIds = [null!];
        ShouldFailOn(dto, "Variants[0].AttributeValueIds");
    }

    [Fact]
    [Trait("Rule", "RN-CE50")]
    public void Should_FailOnAttributes_When_SameIdTwice()
    {
        var id = Guid.NewGuid();
        var dto = Grid("P");
        dto.Attributes[0].Id = id;
        dto.Attributes.Add(Attribute("Cor", "Azul"));
        dto.Attributes[1].Id = id;
        dto.Variants = [Variant("A", "P", "Azul")];
        ShouldFailOn(dto, "Attributes");
    }

    [Fact]
    [Trait("Rule", "RN-CE50")]
    public void Should_FailOnVariants_When_SameIdTwice()
    {
        var id = Guid.NewGuid();
        var dto = Grid("P", "M");
        dto.Variants[0].Id = id;
        dto.Variants[1].Id = id;
        ShouldFailOn(dto, "Variants");
    }

    [Fact]
    [Trait("Rule", "RN-CE50")]
    public void Should_AcceptSeveralNewRows_When_NoIdIsRepeated()
    {
        var dto = Grid("P", "M");
        dto.Attributes.Add(Attribute("Cor", "Azul"));
        dto.Variants = [Variant("A", "P", "Azul"), Variant("B", "M", "Azul")];
        _validator.Validate(dto).IsValid.ShouldBeTrue();
    }

    [Fact]
    [Trait("Rule", "RN-CE50")]
    public void Should_AcceptReference_When_ExistingValueById()
    {
        var id = Guid.NewGuid();
        var dto = Grid("P");
        dto.Attributes[0].Values = [new() { Id = id, Value = "P" }];
        dto.Variants[0].AttributeValueIds = [id.ToString().ToUpperInvariant()];
        _validator.Validate(dto).IsValid.ShouldBeTrue();
    }

    [Fact]
    [Trait("Rule", "RN-CE54")]
    public void Should_FailOnVariants_When_AllInactive()
    {
        var dto = Grid("P", "M");
        dto.Variants.ForEach(v => v.IsActive = false);
        ShouldFailOn(dto, "Variants");
    }

    [Fact]
    [Trait("Rule", "RN-CE54")]
    public void Should_AcceptGrid_When_OneVariantStillActive()
    {
        var dto = Grid("P", "M");
        dto.Variants[0].IsActive = false;
        _validator.Validate(dto).IsValid.ShouldBeTrue();
    }

    [Fact]
    [Trait("Rule", "RN-CE56")]
    public void Should_FailOnShelfLife_When_TracksLotsWithoutIt()
    {
        var dto = Simple();
        dto.TracksLots = true;
        ShouldFailOn(dto, "ShelfLifeDays");
    }

    [Fact]
    [Trait("Rule", "RN-CE56")]
    public void Should_FailOnShelfLife_When_TracksLotsWithZero()
    {
        var dto = Simple();
        dto.TracksLots = true;
        dto.ShelfLifeDays = 0;
        ShouldFailOn(dto, "ShelfLifeDays");
    }

    [Fact]
    [Trait("Rule", "RN-CE56")]
    public void Should_IgnoreShelfLife_When_NotTrackingLots()
    {
        var dto = Simple();
        dto.ShelfLifeDays = 0;
        _validator.Validate(dto).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Should_FailOnPrice_When_NotPositive(decimal price)
    {
        var dto = Simple();
        dto.Variants[0].Price = price;
        ShouldFailOn(dto, "Variants[0].Price");
    }

    [Fact]
    public void Should_FailOnPrice_When_MoreThanTwoDecimals()
    {
        var dto = Simple();
        dto.Variants[0].Price = 1.999m;
        ShouldFailOn(dto, "Variants[0].Price");
    }

    [Fact]
    public void Should_FailOnSku_When_Empty()
    {
        var dto = Simple();
        dto.Variants[0].Sku = "";
        ShouldFailOn(dto, "Variants[0].Sku");
    }

    [Fact]
    [Trait("Rule", "RN-CE57")]
    public void Should_FailOnSku_When_RepeatedInFormIgnoringCase()
    {
        var dto = Grid("P", "M");
        dto.Variants[1].Sku = dto.Variants[0].Sku.ToLowerInvariant();
        ShouldFailOn(dto, "Variants[1].Sku");
    }

    [Fact]
    [Trait("Rule", "RN-CE57")]
    public void Should_FailOnSku_When_RepeatedInFormIgnoringBlanks()
    {
        var dto = Grid("P", "M");
        dto.Variants[0].Sku = "CAM";
        dto.Variants[1].Sku = " CAM ";
        ShouldFailOn(dto, "Variants[1].Sku");
    }

    [Fact]
    public void Should_FailOnAttribute_When_ItemIsNull()
    {
        var dto = Simple();
        dto.Attributes = [null!];
        ShouldFailOn(dto, "Attributes[0]");
    }

    [Fact]
    public void Should_FailOnValue_When_ItemIsNull()
    {
        var dto = Grid("P");
        dto.Attributes[0].Values.Add(null!);
        ShouldFailOn(dto, "Attributes[0].Values[1]");
    }

    [Fact]
    public void Should_FailOnVariant_When_ItemIsNull()
    {
        var dto = Simple();
        dto.Variants = [null!];
        ShouldFailOn(dto, "Variants[0]");
    }

    [Fact]
    public void Should_FailOnNullItem_When_MixedWithValidOnes()
    {
        var dto = Grid("P", "M");
        dto.Variants.Insert(1, null!);
        ShouldFailOn(dto, "Variants[1]");
    }

    [Fact]
    [Trait("Rule", "RN-CE57")]
    public void Should_FailOnBarcode_When_RepeatedInForm()
    {
        var dto = Grid("P", "M");
        dto.Variants[0].Barcode = "789";
        dto.Variants[1].Barcode = " 789 ";
        ShouldFailOn(dto, "Variants[1].Barcode");
    }

    [Fact]
    [Trait("Rule", "RN-CE57")]
    public void Should_AcceptBarcode_When_EmptyInSeveralVariants()
    {
        var dto = Grid("P", "M");
        dto.Variants[0].Barcode = "";
        dto.Variants[1].Barcode = null;
        _validator.Validate(dto).IsValid.ShouldBeTrue();
    }
}

public class UploadFileDtoValidatorTests
{
    private readonly UploadFileDtoValidator _validator = new();

    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 1, 2, 3];
    private static readonly byte[] JpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0, 16, 1, 2, 3, 4, 5, 6, 7];
    private static readonly byte[] WebpBytes = [.. "RIFF"u8.ToArray(), 1, 2, 3, 4, .. "WEBP"u8.ToArray(), 9];

    private static UploadFileDto File(string name, byte[] bytes, long? length = null)
        => new(name, "image/qualquer", length ?? bytes.Length, new MemoryStream(bytes));

    [Theory]
    [InlineData("a.png")]
    [InlineData("a.PNG")]
    public void Should_Accept_When_PngWithSignature(string name)
        => _validator.Validate(File(name, PngBytes)).IsValid.ShouldBeTrue();

    [Theory]
    [InlineData("a.jpg")]
    [InlineData("a.jpeg")]
    public void Should_Accept_When_JpegWithSignature(string name)
        => _validator.Validate(File(name, JpegBytes)).IsValid.ShouldBeTrue();

    [Fact]
    [Trait("Rule", "RN-CE05")]
    public void Should_Accept_When_WebpWithSignature()
        => _validator.Validate(File("a.webp", WebpBytes)).IsValid.ShouldBeTrue();

    [Fact]
    [Trait("Rule", "RN-CE05")]
    public void Should_Fail_When_Empty()
        => _validator.Validate(File("a.png", [])).IsValid.ShouldBeFalse();

    [Fact]
    [Trait("Rule", "RN-CE05")]
    public void Should_Fail_When_LargerThan5Mb()
        => _validator.Validate(File("a.png", PngBytes, UploadFileDtoValidator.MaxSizeInBytes + 1)).IsValid.ShouldBeFalse();

    [Fact]
    [Trait("Rule", "RN-CE05")]
    public void Should_Accept_When_Exactly5Mb()
        => _validator.Validate(File("a.png", PngBytes, UploadFileDtoValidator.MaxSizeInBytes)).IsValid.ShouldBeTrue();

    [Fact]
    [Trait("Rule", "RN-CE05")]
    public void Should_Fail_When_ExtensionNotAccepted()
        => _validator.Validate(File("a.gif", PngBytes)).IsValid.ShouldBeFalse();

    [Theory]
    [Trait("Rule", "RN-CE05")]
    [InlineData("a.png")]
    [InlineData("a.jpg")]
    [InlineData("a.webp")]
    public void Should_Fail_When_ContentIsNotTheDeclaredImage(string name)
        => _validator.Validate(File(name, "isto nao e imagem"u8.ToArray())).IsValid.ShouldBeFalse();

    [Fact]
    [Trait("Rule", "RN-CE05")]
    public void Should_Fail_When_RiffButNotWebp()
        => _validator.Validate(File("a.webp", [.. "RIFF"u8.ToArray(), 1, 2, 3, 4, .. "WAVE"u8.ToArray(), 9])).IsValid.ShouldBeFalse();

    [Fact]
    [Trait("Rule", "RN-CE05")]
    public void Should_RewindStream_When_SignatureRead()
    {
        var file = File("a.png", PngBytes);
        _validator.Validate(file);
        file.Content.Position.ShouldBe(0);
    }

    [Fact]
    public void Should_ReportOnImageField_When_Invalid()
        => _validator.Validate(File("a.gif", PngBytes)).Errors.ShouldContain(e => e.PropertyName == UploadFileDtoValidator.Field);
}
