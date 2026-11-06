using System.Linq.Expressions;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Nextflow.Application.Services;
using Nextflow.Application.UseCases.Catalog;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Categories;
using Nextflow.Domain.Dtos.Collections;
using Nextflow.Domain.Dtos.Products;
using Nextflow.Domain.Dtos.Suppliers;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Interfaces.Infrastructure.Services;
using Nextflow.Domain.Models;
using Nextflow.Domain.Utils;
using Nextflow.Domain.Validators.Catalog;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Nextflow.Tests.Unit.Inventory;

public class ProductUseCaseRulesTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IFileStorage _storage = Substitute.For<IFileStorage>();

    private static IProductRepository Products(params Product[] products)
    {
        var repository = Substitute.For<IProductRepository>();
        repository.GetByIdForUpdate(Arg.Any<Guid>(), Arg.Any<Func<IQueryable<Product>, IQueryable<Product>>?>(), Arg.Any<CancellationToken>())
            .Returns(call => products.FirstOrDefault(p => p.Id == call.Arg<Guid>()));
        repository.GetById(Arg.Any<Guid>(), Arg.Any<Func<IQueryable<Product>, IQueryable<Product>>?>(), Arg.Any<CancellationToken>())
            .Returns(call => products.FirstOrDefault(p => p.Id == call.Arg<Guid>()));
        return repository;
    }

    /// <summary>A releitura depois do commit devolve o produto já gravado, sem depender das navegações que só o banco preenche.</summary>
    private static IProductRepository ProductsRereading(Product? forUpdate = null)
    {
        var repository = Products(forUpdate is null ? [] : [forUpdate]);
        repository.GetById(Arg.Any<Guid>(), Arg.Any<Func<IQueryable<Product>, IQueryable<Product>>?>(), Arg.Any<CancellationToken>())
            .Returns(ProductTestData.NewProduct(Simple()));
        return repository;
    }

    private static ICategoryRepository Categories(params Category[] categories)
    {
        var repository = Substitute.For<ICategoryRepository>();
        repository.GetAll(Arg.Any<Expression<Func<Category, bool>>>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<Func<IQueryable<Category>, IQueryable<Category>>?>(), Arg.Any<Func<IQueryable<Category>, IOrderedQueryable<Category>>?>(), Arg.Any<CancellationToken>())
            .Returns(call => categories.AsQueryable().Where(call.Arg<Expression<Func<Category, bool>>>()).ToList());
        repository.Exists(Arg.Any<Expression<Func<Category, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(call => categories.AsQueryable().Any(call.Arg<Expression<Func<Category, bool>>>()));
        repository.GetByIdForUpdate(Arg.Any<Guid>(), Arg.Any<Func<IQueryable<Category>, IQueryable<Category>>?>(), Arg.Any<CancellationToken>())
            .Returns(call => categories.FirstOrDefault(c => c.Id == call.Arg<Guid>()));
        return repository;
    }

    private static ICollectionRepository Collections(params Collection[] collections)
    {
        var repository = Substitute.For<ICollectionRepository>();
        repository.GetById(Arg.Any<Guid>(), Arg.Any<Func<IQueryable<Collection>, IQueryable<Collection>>?>(), Arg.Any<CancellationToken>())
            .Returns(call => collections.FirstOrDefault(c => c.Id == call.Arg<Guid>()));
        return repository;
    }

    private static IProductVariantRepository Variants(params ProductVariant[] variants)
    {
        var repository = Substitute.For<IProductVariantRepository>();
        repository.GetAll(Arg.Any<Expression<Func<ProductVariant, bool>>>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<Func<IQueryable<ProductVariant>, IQueryable<ProductVariant>>?>(), Arg.Any<Func<IQueryable<ProductVariant>, IOrderedQueryable<ProductVariant>>?>(), Arg.Any<CancellationToken>())
            .Returns(call => variants.AsQueryable().Where(call.Arg<Expression<Func<ProductVariant, bool>>>()).ToList());
        return repository;
    }

    private static ICalendarEventRepository Events(params CalendarEvent[] events)
    {
        var repository = Substitute.For<ICalendarEventRepository>();
        repository.GetAll(Arg.Any<Expression<Func<CalendarEvent, bool>>>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<Func<IQueryable<CalendarEvent>, IQueryable<CalendarEvent>>?>(), Arg.Any<Func<IQueryable<CalendarEvent>, IOrderedQueryable<CalendarEvent>>?>(), Arg.Any<CancellationToken>())
            .Returns(call => events.AsQueryable().Where(call.Arg<Expression<Func<CalendarEvent, bool>>>()).ToList());
        return repository;
    }

    private static ISupplierRepository Suppliers(params Supplier[] suppliers)
    {
        var repository = Substitute.For<ISupplierRepository>();
        repository.GetAll(Arg.Any<Expression<Func<Supplier, bool>>>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<Func<IQueryable<Supplier>, IQueryable<Supplier>>?>(), Arg.Any<Func<IQueryable<Supplier>, IOrderedQueryable<Supplier>>?>(), Arg.Any<CancellationToken>())
            .Returns(call => suppliers.AsQueryable().Where(call.Arg<Expression<Func<Supplier, bool>>>()).ToList());
        return repository;
    }

    private static Supplier NewSupplier(string name = "Doces Ltda") => new(new SupplierRequestDto { Name = name, Cnpj = "11222333000181" });

    private CreateProductUseCase CreateWith(params Supplier[] suppliers)
        => new(Products(), Categories(), Collections(), Variants(), Events(), Suppliers(suppliers), Substitute.For<IStockLocationRepository>(), _storage, new ProductPricingService(), ProductTestData.Clock(), _unitOfWork, new ProductRequestDtoValidator(), new ResultFactory<ProductResponseDto>());

    private CreateProductUseCase Create(ICategoryRepository? categories = null, ICollectionRepository? collections = null, IProductVariantRepository? variants = null, IProductRepository? products = null)
        => new(products ?? Products(), categories ?? Categories(), collections ?? Collections(), variants ?? Variants(), Events(), Suppliers(), Substitute.For<IStockLocationRepository>(), _storage, new ProductPricingService(), ProductTestData.Clock(), _unitOfWork, new ProductRequestDtoValidator(), new ResultFactory<ProductResponseDto>());

    private UpdateProductUseCase Update(Product product, ICategoryRepository? categories = null, ICollectionRepository? collections = null, IProductVariantRepository? variants = null, IProductRepository? products = null)
        => new(products ?? Products(product), categories ?? Categories(), collections ?? Collections(), variants ?? Variants([.. product.Variants]), Events(), Suppliers(), Substitute.For<IStockLocationRepository>(), Substitute.For<IStockBalanceRepository>(), _storage, new ProductPricingService(), ProductTestData.Clock(), _unitOfWork, new ProductRequestDtoValidator(), new ResultFactory<ProductResponseDto>());

    private static ProductRequestDto Simple(string sku = "CAD-1", string? barcode = null) => new()
    {
        Name = "Caderno",
        ProductType = ProductTypeEnum.Standard,
        SaleUnit = SaleUnitEnum.Unit,
        Variants = [new() { Sku = sku, Barcode = barcode, Price = 5m }],
    };

    private static ProductRequestDto Grid(string prefix, params string[] sizes) => new()
    {
        Name = "Camiseta",
        ProductType = ProductTypeEnum.Apparel,
        SaleUnit = SaleUnitEnum.Unit,
        Attributes = [new() { Name = "Tamanho", Values = [.. sizes.Select(s => new ProductAttributeValueRequestDto { TempKey = s, Value = s })] }],
        Variants = [.. sizes.Select(s => new ProductVariantRequestDto { Sku = $"{prefix}-{s}", Price = 10m, AttributeValueIds = [s] })],
    };

    private static ProductRequestDto EditOf(Product product) => new ProductFormDto(product, null, new ProductFormOptionsDto());

    private static Category InactiveCategory()
    {
        var category = new Category(new CategoryRequestDto { Name = "Inativa" });
        category.Delete();
        return category;
    }

    private static Collection InactiveCollection()
    {
        var collection = new Collection(new CollectionRequestDto { Name = "Inativa" });
        collection.Delete();
        return collection;
    }

    private async Task ShouldNotCommit() => await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());

    private static void ShouldHaveField<T>(ResultDto<T> result, string field)
    {
        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(422);
        result.FieldErrors.ShouldNotBeNull();
        result.FieldErrors!.ShouldContainKey(field);
    }

    // ---------- Fornecedores (F1.4) ----------

    private static ProductRequestDto WithSuppliers(params ProductSupplierRequestDto[] suppliers)
    {
        var dto = Simple();
        dto.Suppliers = [.. suppliers];
        return dto;
    }

    [Fact]
    [Trait("Rule", "RN-CE27")]
    public async Task Should_Return422_When_SupplierDuplicated()
    {
        var supplier = NewSupplier();
        var dto = WithSuppliers(new ProductSupplierRequestDto { SupplierId = supplier.Id }, new ProductSupplierRequestDto { SupplierId = supplier.Id });

        ShouldHaveField(await CreateWith(supplier).Execute(dto, ProductTestData.UserId), "suppliers[1].supplierId");
        await ShouldNotCommit();
    }

    [Fact]
    [Trait("Rule", "RN-CE27")]
    public async Task Should_Return422_When_MoreThanOnePreferredSupplier()
    {
        var first = NewSupplier("Doces A");
        var second = NewSupplier("Doces B");
        var dto = WithSuppliers(new ProductSupplierRequestDto { SupplierId = first.Id, IsPreferred = true }, new ProductSupplierRequestDto { SupplierId = second.Id, IsPreferred = true });

        ShouldHaveField(await CreateWith(first, second).Execute(dto, ProductTestData.UserId), "suppliers");
        await ShouldNotCommit();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-6)]
    [InlineData(2.5)]
    [Trait("Rule", "RN-CE30")]
    public async Task Should_Return422_When_OrderMultipleNotPositive(double multiple)
    {
        var supplier = NewSupplier();
        var dto = WithSuppliers(new ProductSupplierRequestDto { SupplierId = supplier.Id, OrderMultiple = (decimal)multiple });

        ShouldHaveField(await CreateWith(supplier).Execute(dto, ProductTestData.UserId), "suppliers[0].orderMultiple");
        await ShouldNotCommit();
    }

    [Fact]
    [Trait("Rule", "RN-CE30")]
    public async Task Should_Return422_When_MinOrderQtyNotPositive()
    {
        var supplier = NewSupplier();
        var dto = WithSuppliers(new ProductSupplierRequestDto { SupplierId = supplier.Id, MinOrderQty = 0 });

        ShouldHaveField(await CreateWith(supplier).Execute(dto, ProductTestData.UserId), "suppliers[0].minOrderQty");
    }

    [Fact]
    [Trait("Rule", "RN-CE27")]
    public async Task Should_Return422_When_SupplierInactiveAndNotLinked()
    {
        var supplier = NewSupplier();
        supplier.Delete();
        var dto = WithSuppliers(new ProductSupplierRequestDto { SupplierId = supplier.Id });

        ShouldHaveField(await CreateWith(supplier).Execute(dto, ProductTestData.UserId), "suppliers[0].supplierId");
    }

    [Fact]
    [Trait("Rule", "RN-CE25")]
    public async Task Should_Return401_When_UserNotInformed()
    {
        var result = await Create().Execute(Simple());

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(401);
        await ShouldNotCommit();
    }

    // ---------- Criar ----------

    [Fact]
    public async Task Should_CommitOnce_When_CreatingValidProduct()
    {
        var result = await Create().Execute(Simple(), ProductTestData.UserId);

        result.IsSuccess.ShouldBeTrue();
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_FailOnField_When_FormatInvalid()
    {
        var dto = Simple();
        dto.Name = "";

        ShouldHaveField(await Create().Execute(dto, ProductTestData.UserId), "name");
        await ShouldNotCommit();
    }

    [Fact]
    [Trait("Rule", "RN-CE57")]
    public async Task Should_FailOnSku_When_UsedByAnotherProductIgnoringCase()
    {
        var other = ProductTestData.NewProduct(Simple("abc-1")).Variants.Single();

        var result = await Create(variants: Variants(other)).Execute(Simple("ABC-1"), ProductTestData.UserId);

        ShouldHaveField(result, "variants[0].sku");
        await ShouldNotCommit();
    }

    [Fact]
    [Trait("Rule", "RN-CE57")]
    public async Task Should_FailOnBarcode_When_UsedByAnotherProduct()
    {
        var other = ProductTestData.NewProduct(Simple("OUTRO", "789")).Variants.Single();

        var result = await Create(variants: Variants(other)).Execute(Simple("NOVO", " 789 "), ProductTestData.UserId);

        ShouldHaveField(result, "variants[0].barcode");
        await ShouldNotCommit();
    }

    [Fact]
    [Trait("Rule", "RN-CE57")]
    public async Task Should_FailOnBarcode_When_UsedIgnoringCase()
    {
        var other = ProductTestData.NewProduct(Simple("OUTRO", "abc789")).Variants.Single();

        ShouldHaveField(await Create(variants: Variants(other)).Execute(Simple("NOVO", "ABC789"), ProductTestData.UserId), "variants[0].barcode");
    }

    [Fact]
    [Trait("Rule", "RN-CE57")]
    public async Task Should_FailOnSku_When_ProductUsingItIsInactive()
    {
        var inactive = ProductTestData.NewProduct(Simple("ABC"));
        inactive.Delete();

        ShouldHaveField(await Create(variants: Variants(inactive.Variants.Single())).Execute(Simple("ABC"), ProductTestData.UserId), "variants[0].sku");
    }

    [Fact]
    [Trait("Rule", "RN-CE50")]
    public async Task Should_FailOnId_When_CreatingWithExistingVariantId()
    {
        var dto = Simple();
        dto.Variants[0].Id = Guid.NewGuid();

        ShouldHaveField(await Create().Execute(dto, ProductTestData.UserId), "variants[0].id");
        await ShouldNotCommit();
    }

    [Fact]
    [Trait("Rule", "RN-CE50")]
    public async Task Should_FailOnId_When_CreatingWithExistingAttributeOrValueId()
    {
        var dto = Grid("A", "P");
        dto.Attributes[0].Id = Guid.NewGuid();
        dto.Attributes[0].Values[0].Id = Guid.NewGuid();
        dto.Variants[0].AttributeValueIds = [dto.Attributes[0].Values[0].Id.ToString()!];

        var result = await Create().Execute(dto, ProductTestData.UserId);

        ShouldHaveField(result, "attributes[0].id");
        result.FieldErrors!.ShouldContainKey("attributes[0].values[0].id");
    }

    [Fact]
    public async Task Should_FailOnCategories_When_CategoryDoesNotExist()
    {
        var dto = Simple();
        dto.CategoryIds = [Guid.NewGuid()];

        ShouldHaveField(await Create().Execute(dto, ProductTestData.UserId), "categoryIds");
        await ShouldNotCommit();
    }

    [Fact]
    [Trait("Rule", "RN-CE53")]
    public async Task Should_FailOnCategories_When_NewLinkToInactiveCategory()
    {
        var category = InactiveCategory();
        var dto = Simple();
        dto.CategoryIds = [category.Id];

        ShouldHaveField(await Create(categories: Categories(category)).Execute(dto, ProductTestData.UserId), "categoryIds");
    }

    [Fact]
    public async Task Should_AcceptCategory_When_Active()
    {
        var category = new Category(new CategoryRequestDto { Name = "Doces" });
        var dto = Simple();
        dto.CategoryIds = [category.Id];

        (await Create(categories: Categories(category), products: ProductsRereading()).Execute(dto, ProductTestData.UserId)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_FailOnCollection_When_DoesNotExist()
    {
        var dto = Simple();
        dto.CollectionId = Guid.NewGuid();

        ShouldHaveField(await Create().Execute(dto, ProductTestData.UserId), "collectionId");
    }

    [Fact]
    [Trait("Rule", "RN-CE40")]
    public async Task Should_FailOnCollection_When_Inactive()
    {
        var collection = InactiveCollection();
        var dto = Simple();
        dto.CollectionId = collection.Id;

        ShouldHaveField(await Create(collections: Collections(collection)).Execute(dto, ProductTestData.UserId), "collectionId");
    }

    // ---------- Editar ----------

    [Fact]
    [Trait("Rule", "RN-CE04")]
    public async Task Should_Return400_When_EditingInactiveProduct()
    {
        var product = ProductTestData.NewProduct(Simple());
        product.Delete();

        var result = await Update(product).Execute(product.Id, EditOf(product), ProductTestData.UserId);

        result.ErrorCode.ShouldBe(400);
        await ShouldNotCommit();
    }

    [Fact]
    [Trait("Rule", "RN-CE53")]
    public async Task Should_AcceptEdit_When_LinkedCategoryWasInactivated()
    {
        var category = new Category(new CategoryRequestDto { Name = "Doces" });
        var dto = Simple();
        dto.CategoryIds = [category.Id];
        var product = ProductTestData.NewProduct(dto);
        category.Delete();

        (await Update(product, categories: Categories(category), products: ProductsRereading(product)).Execute(product.Id, EditOf(product), ProductTestData.UserId)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    [Trait("Rule", "RN-CE53")]
    public async Task Should_FailOnCategories_When_EditAddsInactiveCategory()
    {
        var product = ProductTestData.NewProduct(Simple());
        var category = InactiveCategory();
        var dto = EditOf(product);
        dto.CategoryIds = [category.Id];

        ShouldHaveField(await Update(product, categories: Categories(category)).Execute(product.Id, dto, ProductTestData.UserId), "categoryIds");
    }

    [Fact]
    public async Task Should_AcceptEdit_When_CurrentCollectionWasInactivated()
    {
        var collection = new Collection(new CollectionRequestDto { Name = "Verão" });
        var dto = Simple();
        dto.CollectionId = collection.Id;
        var product = ProductTestData.NewProduct(dto);
        collection.Delete();

        (await Update(product, collections: Collections(collection)).Execute(product.Id, EditOf(product), ProductTestData.UserId)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    [Trait("Rule", "RN-CE40")]
    public async Task Should_FailOnCollection_When_EditChangesToInactive()
    {
        var product = ProductTestData.NewProduct(Simple());
        var collection = InactiveCollection();
        var dto = EditOf(product);
        dto.CollectionId = collection.Id;

        ShouldHaveField(await Update(product, collections: Collections(collection)).Execute(product.Id, dto, ProductTestData.UserId), "collectionId");
    }

    [Fact]
    public async Task Should_FailOnId_When_VariantBelongsToAnotherProduct()
    {
        var product = ProductTestData.NewProduct(Simple());
        var dto = EditOf(product);
        dto.Variants[0].Id = Guid.NewGuid();

        ShouldHaveField(await Update(product).Execute(product.Id, dto, ProductTestData.UserId), "variants[0].id");
        await ShouldNotCommit();
    }

    [Fact]
    public async Task Should_FailOnId_When_AttributeBelongsToAnotherProduct()
    {
        var product = ProductTestData.NewProduct(Grid("A", "P"));
        var dto = EditOf(product);
        dto.Attributes[0].Id = Guid.NewGuid();

        ShouldHaveField(await Update(product).Execute(product.Id, dto, ProductTestData.UserId), "attributes[0].id");
    }

    [Fact]
    public async Task Should_FailOnId_When_ValueBelongsToAnotherAttribute()
    {
        var product = ProductTestData.NewProduct(Grid("A", "P"));
        var dto = EditOf(product);
        var foreignValue = Guid.NewGuid();
        dto.Attributes[0].Values[0].Id = foreignValue;
        dto.Variants[0].AttributeValueIds = [foreignValue.ToString()];

        ShouldHaveField(await Update(product).Execute(product.Id, dto, ProductTestData.UserId), "attributes[0].values[0].id");
    }

    [Fact]
    [Trait("Rule", "RN-CE57")]
    public async Task Should_AcceptOwnSku_When_EditingSameVariant()
        => await AssertOwnSkuAccepted(ProductTestData.NewProduct(Simple("ABC")));

    private async Task AssertOwnSkuAccepted(Product product)
        => (await Update(product).Execute(product.Id, EditOf(product), ProductTestData.UserId)).IsSuccess.ShouldBeTrue();

    [Fact]
    [Trait("Rule", "RN-CE57")]
    public async Task Should_FailOnSku_When_SwappingBetweenTwoKeptVariants()
    {
        var product = ProductTestData.NewProduct(Grid("A", "P", "M"));
        var dto = EditOf(product);
        (dto.Variants[0].Sku, dto.Variants[1].Sku) = (dto.Variants[1].Sku, dto.Variants[0].Sku);

        ShouldHaveField(await Update(product).Execute(product.Id, dto, ProductTestData.UserId), "variants[0].sku");
    }

    [Fact]
    [Trait("Rule", "RN-CE57")]
    public async Task Should_FailOnSku_When_ReusingSkuOfVariantRemovedInSameSave()
    {
        var product = ProductTestData.NewProduct(Grid("A", "P", "M"));
        var dto = EditOf(product);
        var leaving = dto.Variants[1];
        dto.Variants = [dto.Variants[0], new() { Sku = leaving.Sku, Price = 10m, AttributeValueIds = leaving.AttributeValueIds }];

        ShouldHaveField(await Update(product).Execute(product.Id, dto, ProductTestData.UserId), "variants[1].sku");
    }

    [Fact]
    [Trait("Rule", "RN-CE57")]
    public async Task Should_FailOnSku_When_UsedByAnotherProductOnEdit()
    {
        var product = ProductTestData.NewProduct(Simple("MEU"));
        var other = ProductTestData.NewProduct(Simple("DELE")).Variants.Single();
        var dto = EditOf(product);
        dto.Variants[0].Sku = "dele";

        ShouldHaveField(await Update(product, variants: Variants(other)).Execute(product.Id, dto, ProductTestData.UserId), "variants[0].sku");
    }

    // ---------- Ciclo de vida ----------

    private UpdateProductLifecycleUseCase Lifecycle(params Product[] products)
        => new(Products(products), _unitOfWork, new ProductLifecycleRequestDtoValidator(), new ResultFactory<bool>());

    [Fact]
    [Trait("Rule", "RN-CE19")]
    public async Task Should_MarkDiscontinued_When_Asked()
    {
        var product = ProductTestData.NewProduct(Simple());

        var result = await Lifecycle(product).Execute(product.Id, new() { Lifecycle = LifecycleEnum.Discontinued });

        result.IsSuccess.ShouldBeTrue();
        product.Lifecycle.ShouldBe(LifecycleEnum.Discontinued);
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Rule", "RN-CE04")]
    public async Task Should_Return400_When_ChangingLifecycleOfInactiveProduct()
    {
        var product = ProductTestData.NewProduct(Simple());
        product.Delete();

        (await Lifecycle(product).Execute(product.Id, new() { Lifecycle = LifecycleEnum.Discontinued })).ErrorCode.ShouldBe(400);
        product.Lifecycle.ShouldBe(LifecycleEnum.Active);
    }

    [Fact]
    public async Task Should_FailOnLifecycle_When_OutOfEnum()
        => ShouldHaveField(await Lifecycle().Execute(Guid.NewGuid(), new() { Lifecycle = 0 }), "lifecycle");

    [Fact]
    public async Task Should_Return404_When_LifecycleOfUnknownProduct()
        => (await Lifecycle().Execute(Guid.NewGuid(), new() { Lifecycle = LifecycleEnum.Active })).ErrorCode.ShouldBe(404);

    // ---------- Imagem ----------

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13];

    private static UploadFileDto PngFile() => new("foto.png", "image/png", Png.Length, new MemoryStream(Png));

    private UploadProductImageUseCase UploadImage(params Product[] products)
        => new(Products(products), _storage, _unitOfWork, new UploadFileDtoValidator(), new ResultFactory<ProductImageResponseDto>(), NullLogger<UploadProductImageUseCase>.Instance);

    private RemoveProductImageUseCase RemoveImage(params Product[] products)
        => new(Products(products), _storage, _unitOfWork, new ResultFactory<bool>(), NullLogger<RemoveProductImageUseCase>.Instance);

    private async Task ShouldNotUpload()
        => await _storage.DidNotReceive().UploadAsync(Arg.Any<StorageFolder>(), Arg.Any<UploadFileDto>(), Arg.Any<CancellationToken>());

    [Fact]
    public async Task Should_FailOnImage_When_NoFile()
    {
        ShouldHaveField(await UploadImage().Execute(Guid.NewGuid(), null), "image");
        await ShouldNotUpload();
    }

    [Fact]
    [Trait("Rule", "RN-CE05")]
    public async Task Should_NotUpload_When_FileInvalid()
    {
        var product = ProductTestData.NewProduct(Simple());
        var file = new UploadFileDto("foto.png", "image/png", 3, new MemoryStream([1, 2, 3]));

        ShouldHaveField(await UploadImage(product).Execute(product.Id, file), "image");
        await ShouldNotUpload();
    }

    [Fact]
    [Trait("Rule", "RN-CE04")]
    public async Task Should_NotUpload_When_ProductInactive()
    {
        var product = ProductTestData.NewProduct(Simple());
        product.Delete();

        (await UploadImage(product).Execute(product.Id, PngFile())).ErrorCode.ShouldBe(400);
        await ShouldNotUpload();
    }

    [Fact]
    public async Task Should_Return404_When_ImageOfUnknownProduct()
        => (await UploadImage().Execute(Guid.NewGuid(), PngFile())).ErrorCode.ShouldBe(404);

    [Fact]
    public async Task Should_DeletePreviousAfterCommit_When_ReplacingImage()
    {
        var product = ProductTestData.NewProduct(Simple());
        product.SetImage("products/antiga.png");
        _storage.UploadAsync(StorageFolder.ProductImages, Arg.Any<UploadFileDto>(), Arg.Any<CancellationToken>()).Returns("products/nova.png");
        _storage.GetPublicUrl("products/nova.png").Returns("http://s/nova");

        var result = await UploadImage(product).Execute(product.Id, PngFile());

        result.Data!.ImageUrl.ShouldBe("http://s/nova");
        product.ImageKey.ShouldBe("products/nova.png");
        Received.InOrder(() =>
        {
            _unitOfWork.CommitAsync(Arg.Any<CancellationToken>());
            _storage.DeleteAsync(StorageBucketEnum.Public, "products/antiga.png", Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Should_DeleteNewAndRethrow_When_CommitFails()
    {
        var product = ProductTestData.NewProduct(Simple());
        product.SetImage("products/antiga.png");
        _storage.UploadAsync(StorageFolder.ProductImages, Arg.Any<UploadFileDto>(), Arg.Any<CancellationToken>()).Returns("products/nova.png");
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("banco"));

        await Should.ThrowAsync<InvalidOperationException>(() => UploadImage(product).Execute(product.Id, PngFile()));

        await _storage.Received(1).DeleteAsync(StorageBucketEnum.Public, "products/nova.png", Arg.Any<CancellationToken>());
        await _storage.DidNotReceive().DeleteAsync(StorageBucketEnum.Public, "products/antiga.png", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_KeepSuccess_When_DeletingPreviousFails()
    {
        var product = ProductTestData.NewProduct(Simple());
        product.SetImage("products/antiga.png");
        _storage.UploadAsync(StorageFolder.ProductImages, Arg.Any<UploadFileDto>(), Arg.Any<CancellationToken>()).Returns("products/nova.png");
        _storage.DeleteAsync(StorageBucketEnum.Public, "products/antiga.png", Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("s3"));

        (await UploadImage(product).Execute(product.Id, PngFile())).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_PassTokenAndKeepSwap_When_DeletingPreviousIsCanceled()
    {
        var product = ProductTestData.NewProduct(Simple());
        product.SetImage("products/antiga.png");
        using var cts = new CancellationTokenSource();
        _storage.UploadAsync(StorageFolder.ProductImages, Arg.Any<UploadFileDto>(), cts.Token).Returns("products/nova.png");
        _storage.DeleteAsync(StorageBucketEnum.Public, "products/antiga.png", cts.Token).ThrowsAsync(new OperationCanceledException());

        var result = await UploadImage(product).Execute(product.Id, PngFile(), cts.Token);

        // A troca já foi gravada: o cancelamento só deixa o arquivo antigo sobrando, e o banco segue na imagem nova.
        result.IsSuccess.ShouldBeTrue();
        product.ImageKey.ShouldBe("products/nova.png");
        await _storage.Received(1).DeleteAsync(StorageBucketEnum.Public, "products/antiga.png", cts.Token);
    }

    [Fact]
    public async Task Should_PassTokenAndKeepRemoval_When_DeletingFileIsCanceled()
    {
        var product = ProductTestData.NewProduct(Simple());
        product.SetImage("products/a.png");
        using var cts = new CancellationTokenSource();
        _storage.DeleteAsync(StorageBucketEnum.Public, "products/a.png", cts.Token).ThrowsAsync(new OperationCanceledException());

        (await RemoveImage(product).Execute(product.Id, cts.Token)).IsSuccess.ShouldBeTrue();

        product.ImageKey.ShouldBeNull();
        await _unitOfWork.Received(1).CommitAsync(cts.Token);
        await _storage.Received(1).DeleteAsync(StorageBucketEnum.Public, "products/a.png", cts.Token);
    }

    [Fact]
    public async Task Should_DeleteNewWithoutToken_When_CommitIsCanceled()
    {
        var product = ProductTestData.NewProduct(Simple());
        using var cts = new CancellationTokenSource();
        _storage.UploadAsync(StorageFolder.ProductImages, Arg.Any<UploadFileDto>(), cts.Token).Returns("products/nova.png");
        _unitOfWork.CommitAsync(cts.Token).ThrowsAsync(new OperationCanceledException());

        await Should.ThrowAsync<OperationCanceledException>(() => UploadImage(product).Execute(product.Id, PngFile(), cts.Token));

        await _storage.Received(1).DeleteAsync(StorageBucketEnum.Public, "products/nova.png", CancellationToken.None);
    }

    [Fact]
    public async Task Should_CommitBeforeDeletingFile_When_RemovingImage()
    {
        var product = ProductTestData.NewProduct(Simple());
        product.SetImage("products/a.png");

        (await RemoveImage(product).Execute(product.Id)).IsSuccess.ShouldBeTrue();

        product.ImageKey.ShouldBeNull();
        Received.InOrder(() =>
        {
            _unitOfWork.CommitAsync(Arg.Any<CancellationToken>());
            _storage.DeleteAsync(StorageBucketEnum.Public, "products/a.png", Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Should_KeepSuccess_When_DeletingRemovedFileFails()
    {
        var product = ProductTestData.NewProduct(Simple());
        product.SetImage("products/a.png");
        _storage.DeleteAsync(StorageBucketEnum.Public, "products/a.png", Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("s3"));

        (await RemoveImage(product).Execute(product.Id)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_DoNothing_When_RemovingAbsentImage()
    {
        var product = ProductTestData.NewProduct(Simple());

        (await RemoveImage(product).Execute(product.Id)).IsSuccess.ShouldBeTrue();

        await ShouldNotCommit();
        await _storage.DidNotReceive().DeleteAsync(Arg.Any<StorageBucketEnum>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Rule", "RN-CE04")]
    public async Task Should_Return400_When_RemovingImageOfInactiveProduct()
    {
        var product = ProductTestData.NewProduct(Simple());
        product.SetImage("products/a.png");
        product.Delete();

        (await RemoveImage(product).Execute(product.Id)).ErrorCode.ShouldBe(400);
        product.ImageKey.ShouldBe("products/a.png");
    }

    // ---------- Inativar categoria (T4) ----------

    private DeleteCategoryUseCase DeleteCategory(int activeProducts, params Category[] categories)
    {
        var products = Substitute.For<IProductRepository>();
        products.Count(Arg.Any<Expression<Func<Product, bool>>>(), Arg.Any<CancellationToken>()).Returns(activeProducts);
        return new(Categories(categories), products, _unitOfWork, new ResultFactory<bool>());
    }

    [Fact]
    [Trait("Rule", "RN-CE53")]
    public async Task Should_AskConfirmation_When_CategoryHasActiveProducts()
    {
        var category = new Category(new CategoryRequestDto { Name = "Doces" });

        var result = await DeleteCategory(2, category).Execute(category.Id, false);

        result.ErrorCode.ShouldBe(422);
        result.FieldErrors!.ShouldContainKey(ResultDto<bool>.ConfirmKey);
        result.FieldErrors![ResultDto<bool>.ConfirmKey][0].ShouldContain("2 produto");
        category.IsActive.ShouldBeTrue();
        await ShouldNotCommit();
    }

    [Fact]
    [Trait("Rule", "RN-CE53")]
    public async Task Should_Inactivate_When_ConfirmedWithActiveProducts()
    {
        var category = new Category(new CategoryRequestDto { Name = "Doces" });

        (await DeleteCategory(2, category).Execute(category.Id, true)).IsSuccess.ShouldBeTrue();

        category.IsActive.ShouldBeFalse();
    }

    [Fact]
    [Trait("Rule", "RN-CE53")]
    public async Task Should_InactivateWithoutAsking_When_NoActiveProducts()
    {
        var category = new Category(new CategoryRequestDto { Name = "Doces" });

        (await DeleteCategory(0, category).Execute(category.Id, false)).IsSuccess.ShouldBeTrue();

        category.IsActive.ShouldBeFalse();
    }

    [Fact]
    [Trait("Rule", "RN-CE39")]
    public async Task Should_Return400EvenConfirmed_When_CategoryHasActiveChild()
    {
        var root = new Category(new CategoryRequestDto { Name = "Doces" });
        var child = new Category(new CategoryRequestDto { Name = "Balas", ParentId = root.Id });

        (await DeleteCategory(0, root, child).Execute(root.Id, true)).ErrorCode.ShouldBe(400);
        root.IsActive.ShouldBeTrue();
    }
}
