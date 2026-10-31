using System.Linq.Expressions;
using Nextflow.Application.Services;
using Nextflow.Application.UseCases.Catalog;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Categories;
using Nextflow.Domain.Dtos.Collections;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Domain.Validators.Catalog;
using NSubstitute;

namespace Nextflow.Tests.Unit.Inventory;

public class ClassificationRulesTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static ICategoryRepository Categories(params Category[] categories)
    {
        var repository = Substitute.For<ICategoryRepository>();
        repository.Exists(Arg.Any<Expression<Func<Category, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(call => categories.AsQueryable().Any(call.Arg<Expression<Func<Category, bool>>>()));
        repository.GetById(Arg.Any<Guid>(), Arg.Any<Func<IQueryable<Category>, IQueryable<Category>>?>(), Arg.Any<CancellationToken>())
            .Returns(call => categories.FirstOrDefault(c => c.Id == call.Arg<Guid>()));
        repository.GetByIdForUpdate(Arg.Any<Guid>(), Arg.Any<Func<IQueryable<Category>, IQueryable<Category>>?>(), Arg.Any<CancellationToken>())
            .Returns(call => categories.FirstOrDefault(c => c.Id == call.Arg<Guid>()));
        return repository;
    }

    private static ICollectionRepository Collections(params Collection[] collections)
    {
        var repository = Substitute.For<ICollectionRepository>();
        repository.Exists(Arg.Any<Expression<Func<Collection, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(call => collections.AsQueryable().Any(call.Arg<Expression<Func<Collection, bool>>>()));
        repository.GetByIdForUpdate(Arg.Any<Guid>(), Arg.Any<Func<IQueryable<Collection>, IQueryable<Collection>>?>(), Arg.Any<CancellationToken>())
            .Returns(call => collections.FirstOrDefault(c => c.Id == call.Arg<Guid>()));
        return repository;
    }

    private CreateCategoryUseCase CreateCategory(params Category[] categories)
        => new(Categories(categories), _unitOfWork, new CategoryRequestDtoValidator(), new ResultFactory<CategoryResponseDto>());

    private UpdateCategoryUseCase UpdateCategory(params Category[] categories)
        => new(Categories(categories), _unitOfWork, new CategoryRequestDtoValidator(), new ResultFactory<CategoryResponseDto>());

    private DeleteCategoryUseCase DeleteCategory(params Category[] categories)
        => new(Categories(categories), _unitOfWork, new ResultFactory<bool>());

    private ReactivateCategoryUseCase ReactivateCategory(params Category[] categories)
        => new(Categories(categories), _unitOfWork, new ResultFactory<bool>());

    private CreateCollectionUseCase CreateCollection(params Collection[] collections)
        => new(Collections(collections), _unitOfWork, new CollectionRequestDtoValidator(), new ResultFactory<CollectionResponseDto>());

    private UpdateCollectionUseCase UpdateCollection(params Collection[] collections)
        => new(Collections(collections), _unitOfWork, new CollectionRequestDtoValidator(), new ResultFactory<CollectionResponseDto>());

    private static Category Root(string name = "Doces") => new(new CategoryRequestDto { Name = name });

    private static Category Child(Category parent, string name = "Chocolates")
        => new(new CategoryRequestDto { Name = name, ParentId = parent.Id });

    private static void ShouldHaveFieldError<T>(ResultDto<T> result, string field)
    {
        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(422);
        result.FieldErrors!.ShouldContainKey(field);
    }

    [Fact]
    [Trait("Rule", "RN-CE23")]
    public async Task Should_CreateChild_When_ParentIsLevelOne()
    {
        var root = Root();

        var result = await CreateCategory(root).Execute(new CategoryRequestDto { Name = "Chocolates", ParentId = root.Id });

        result.IsSuccess.ShouldBeTrue();
        result.Data!.ParentId.ShouldBe(root.Id);
    }

    [Fact]
    [Trait("Rule", "RN-CE23")]
    public async Task Should_Return422OnParentId_When_CreatingGrandchild()
    {
        var root = Root();
        var child = Child(root);

        var result = await CreateCategory(root, child).Execute(new CategoryRequestDto { Name = "Trufas", ParentId = child.Id });

        ShouldHaveFieldError(result, "parentId");
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Rule", "RN-CE23")]
    public async Task Should_Return422OnParentId_When_MovingCategoryUnderAChild()
    {
        var root = Root();
        var child = Child(root);
        var other = Root("Presentes");

        var result = await UpdateCategory(root, child, other).Execute(other.Id, new CategoryRequestDto { Name = other.Name, ParentId = child.Id });

        ShouldHaveFieldError(result, "parentId");
        other.ParentId.ShouldBeNull();
    }

    [Fact]
    [Trait("Rule", "RN-CE23")]
    public async Task Should_Return422OnParentId_When_CategoryWithChildrenBecomesChild()
    {
        var root = Root();
        var child = Child(root);
        var other = Root("Presentes");

        var result = await UpdateCategory(root, child, other).Execute(root.Id, new CategoryRequestDto { Name = root.Name, ParentId = other.Id });

        ShouldHaveFieldError(result, "parentId");
    }

    [Fact]
    [Trait("Rule", "RN-CE23")]
    public async Task Should_Return422OnParentId_When_CategoryIsItsOwnParent()
    {
        var root = Root();

        var result = await UpdateCategory(root).Execute(root.Id, new CategoryRequestDto { Name = root.Name, ParentId = root.Id });

        ShouldHaveFieldError(result, "parentId");
    }

    [Fact]
    [Trait("Rule", "RN-CE23")]
    public async Task Should_Return422OnParentId_When_ParentDoesNotExist()
    {
        var result = await CreateCategory().Execute(new CategoryRequestDto { Name = "Trufas", ParentId = Guid.NewGuid() });

        ShouldHaveFieldError(result, "parentId");
    }

    [Fact]
    [Trait("Rule", "RN-CE23")]
    public async Task Should_KeepChildren_When_LevelOneOnlyRenames()
    {
        var root = Root();
        var child = Child(root);

        var result = await UpdateCategory(root, child).Execute(root.Id, new CategoryRequestDto { Name = "Doces finos" });

        result.IsSuccess.ShouldBeTrue();
        root.Name.ShouldBe("Doces finos");
    }

    [Fact]
    [Trait("Rule", "RN-CE38")]
    public async Task Should_Return422OnName_When_SiblingHasSameNameInAnyCase()
    {
        var root = Root();
        var child = Child(root);

        var result = await CreateCategory(root, child).Execute(new CategoryRequestDto { Name = " CHOCOLATES ", ParentId = root.Id });

        ShouldHaveFieldError(result, "name");
    }

    [Fact]
    [Trait("Rule", "RN-CE38")]
    public async Task Should_Return422OnName_When_LevelOneRepeatsName()
    {
        var result = await CreateCategory(Root()).Execute(new CategoryRequestDto { Name = "doces" });

        ShouldHaveFieldError(result, "name");
    }

    [Fact]
    [Trait("Rule", "RN-CE38")]
    public async Task Should_AllowSameName_When_ParentsDiffer()
    {
        var root = Root();
        var other = Root("Presentes");
        var child = Child(root, "Kits");

        var result = await CreateCategory(root, other, child).Execute(new CategoryRequestDto { Name = "Kits", ParentId = other.Id });

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    [Trait("Rule", "RN-CE39")]
    public async Task Should_Return422OnParentId_When_ParentIsInactive()
    {
        var root = Root();
        root.Delete();

        var result = await CreateCategory(root).Execute(new CategoryRequestDto { Name = "Trufas", ParentId = root.Id });

        ShouldHaveFieldError(result, "parentId");
    }

    [Fact]
    [Trait("Rule", "RN-CE39")]
    public async Task Should_Return400_When_InactivatingCategoryWithActiveChild()
    {
        var root = Root();
        var child = Child(root);

        var result = await DeleteCategory(root, child).Execute(root.Id);

        result.ErrorCode.ShouldBe(400);
        root.IsActive.ShouldBeTrue();
    }

    [Fact]
    [Trait("Rule", "RN-CE39")]
    public async Task Should_InactivateParent_When_ChildrenAreInactive()
    {
        var root = Root();
        var child = Child(root);
        child.Delete();

        var result = await DeleteCategory(root, child).Execute(root.Id);

        result.IsSuccess.ShouldBeTrue();
        root.IsActive.ShouldBeFalse();
    }

    [Fact]
    [Trait("Rule", "RN-CE39")]
    public async Task Should_Return400_When_ReactivatingChildOfInactiveParent()
    {
        var root = Root();
        var child = Child(root);
        child.Delete();
        root.Delete();

        var result = await ReactivateCategory(root, child).Execute(child.Id);

        result.ErrorCode.ShouldBe(400);
        child.IsActive.ShouldBeFalse();
    }

    [Fact]
    [Trait("Rule", "RN-CE39")]
    public async Task Should_ReactivateChild_When_ParentIsActive()
    {
        var root = Root();
        var child = Child(root);
        child.Delete();

        var result = await ReactivateCategory(root, child).Execute(child.Id);

        result.IsSuccess.ShouldBeTrue();
        child.IsActive.ShouldBeTrue();
    }

    [Fact]
    [Trait("Rule", "RN-CE40")]
    public async Task Should_Return422OnEndsOn_When_EndIsBeforeStart()
    {
        var dto = new CollectionRequestDto { Name = "Verão", StartsOn = new DateOnly(2026, 12, 1), EndsOn = new DateOnly(2026, 11, 30) };

        var result = await CreateCollection().Execute(dto);

        ShouldHaveFieldError(result, "endsOn");
    }

    [Fact]
    [Trait("Rule", "RN-CE40")]
    public async Task Should_CreateCollection_When_EndEqualsStart()
    {
        var day = new DateOnly(2026, 12, 1);

        var result = await CreateCollection().Execute(new CollectionRequestDto { Name = "Dia único", StartsOn = day, EndsOn = day });

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    [Trait("Rule", "RN-CE40")]
    public async Task Should_CreateCollection_When_DatesAreOmitted()
    {
        var result = await CreateCollection().Execute(new CollectionRequestDto { Name = "Sem datas" });

        result.IsSuccess.ShouldBeTrue();
        result.Data!.StartsOn.ShouldBeNull();
    }

    [Fact]
    [Trait("Rule", "RN-CE40")]
    public async Task Should_Return422OnName_When_CollectionNameRepeatsInAnyCase()
    {
        var existing = new Collection(new CollectionRequestDto { Name = "Verão 2026" });

        var result = await CreateCollection(existing).Execute(new CollectionRequestDto { Name = "VERÃO 2026" });

        ShouldHaveFieldError(result, "name");
    }

    [Fact]
    [Trait("Rule", "RN-CE40")]
    public async Task Should_KeepName_When_UpdatingCollectionWithItsOwnName()
    {
        var existing = new Collection(new CollectionRequestDto { Name = "Verão 2026" });

        var result = await UpdateCollection(existing).Execute(existing.Id, new CollectionRequestDto { Name = "Verão 2026", EndsOn = new DateOnly(2026, 3, 1) });

        result.IsSuccess.ShouldBeTrue();
        existing.EndsOn.ShouldBe(new DateOnly(2026, 3, 1));
    }
}
