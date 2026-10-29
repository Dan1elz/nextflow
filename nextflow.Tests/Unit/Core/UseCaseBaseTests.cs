using System.Linq.Expressions;
using Nextflow.Application.Services;
using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using NSubstitute;

namespace Nextflow.Tests.Unit.Core;

public class UseCaseBaseTests
{
    private sealed class CreateTestUseCase(ITestEntityRepository repository, IUnitOfWork unitOfWork)
        : CreateUseCaseBase<TestEntity, ITestEntityRepository, TestRequestDto, string>(
            repository, unitOfWork, new TestRequestDtoValidator(), new ResultFactory<string>())
    {
        protected override TestEntity MapToEntity(TestRequestDto dto) => new(dto);
        protected override string MapToResponseDto(TestEntity entity) => entity.Name;
    }

    private sealed class DeleteTestUseCase(ITestEntityRepository repository, IUnitOfWork unitOfWork)
        : DeleteUseCaseBase<TestEntity, ITestEntityRepository>(repository, unitOfWork, new ResultFactory<bool>());

    private sealed class GetAllTestUseCase(ITestEntityRepository repository)
        : GetAllUseCaseBase<TestEntity, ITestEntityRepository, string>(repository, new ResultFactory<PaginationDto<string>>())
    {
        protected override string MapToResponseDto(TestEntity entity) => entity.Name;

        protected override IReadOnlyDictionary<string, Expression<Func<TestEntity, object?>>> GetSortableFields()
            => new Dictionary<string, Expression<Func<TestEntity, object?>>> { ["name"] = e => e.Name };
    }

    [Fact]
    public async Task Should_Return422WithFieldErrors_AndNotCommit_When_DtoIsInvalid()
    {
        var repository = Substitute.For<ITestEntityRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var result = await new CreateTestUseCase(repository, unitOfWork).Execute(new TestRequestDto());

        result.ErrorCode.ShouldBe(422);
        result.FieldErrors!["name"].ShouldBe(["Informe o nome."]);
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_AddAndCommitOnce_When_DtoIsValid()
    {
        var repository = Substitute.For<ITestEntityRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var result = await new CreateTestUseCase(repository, unitOfWork).Execute(new TestRequestDto { Name = "Brigadeiro" });

        result.IsSuccess.ShouldBeTrue();
        result.Data.ShouldBe("Brigadeiro");
        await repository.Received(1).AddAsync(Arg.Any<TestEntity>(), Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_SoftDelete_When_EntityIsDeletable()
    {
        var entity = new TestEntity(new TestRequestDto { Name = "Loja Centro" });
        var repository = Substitute.For<ITestEntityRepository>();
        repository.GetByIdForUpdate(entity.Id, Arg.Any<Func<IQueryable<TestEntity>, IQueryable<TestEntity>>?>(), Arg.Any<CancellationToken>())
            .Returns(entity);
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var result = await new DeleteTestUseCase(repository, unitOfWork).Execute(entity.Id);

        result.IsSuccess.ShouldBeTrue();
        entity.IsActive.ShouldBeFalse();
        repository.DidNotReceive().Remove(Arg.Any<TestEntity>());
    }

    [Fact]
    public async Task Should_Return400_When_DeletingAlreadyInactiveEntity()
    {
        var entity = new TestEntity(new TestRequestDto { Name = "Loja Centro" });
        entity.Delete();
        var repository = Substitute.For<ITestEntityRepository>();
        repository.GetByIdForUpdate(entity.Id, Arg.Any<Func<IQueryable<TestEntity>, IQueryable<TestEntity>>?>(), Arg.Any<CancellationToken>())
            .Returns(entity);

        var result = await new DeleteTestUseCase(repository, Substitute.For<IUnitOfWork>()).Execute(entity.Id);

        result.ErrorCode.ShouldBe(400);
        result.Message.ShouldBe("Registro de teste já está inativo.");
    }

    [Theory]
    [InlineData("name", "asc", true)]
    [InlineData("createdAt", "asc", false)]
    [InlineData(null, null, false)]
    public async Task Should_UseSortOnlyFromWhitelist_When_ListingWithSort(string? sort, string? order, bool expectsCustomOrder)
    {
        var repository = Substitute.For<ITestEntityRepository>();
        Func<IQueryable<TestEntity>, IOrderedQueryable<TestEntity>>? capturedOrder = null;
        repository.GetAll(Arg.Any<Expression<Func<TestEntity, bool>>>(), Arg.Any<int>(), Arg.Any<int>(),
                Arg.Any<Func<IQueryable<TestEntity>, IQueryable<TestEntity>>?>(),
                Arg.Do<Func<IQueryable<TestEntity>, IOrderedQueryable<TestEntity>>?>(o => capturedOrder = o),
                Arg.Any<CancellationToken>())
            .Returns([]);

        await new GetAllTestUseCase(repository).Execute(0, 20, null, sort, order);

        (capturedOrder is not null).ShouldBe(expectsCustomOrder);
        if (capturedOrder is null) return;

        var data = new[] { "Doces", "Bombons", "Caixas" }.Select(n => new TestEntity(new TestRequestDto { Name = n })).AsQueryable();
        capturedOrder(data).Select(e => e.Name).ShouldBe(["Bombons", "Caixas", "Doces"]);
    }

    [Theory]
    [InlineData(-5, 0, 0, 1)]
    [InlineData(10, 1000, 10, 100)]
    [InlineData(20, 20, 20, 20)]
    public void Should_ClampOffsetAndLimit_When_Normalizing(int offset, int limit, int expectedOffset, int expectedLimit)
        => PagingLimits.Normalize(offset, limit).ShouldBe((expectedOffset, expectedLimit));
}
