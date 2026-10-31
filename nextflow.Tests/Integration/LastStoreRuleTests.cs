using Microsoft.EntityFrameworkCore;
using Nextflow.Application.Services;
using Nextflow.Application.UseCases.Inventory;
using Nextflow.Domain.Dtos.Stores;
using Nextflow.Domain.Models;
using Nextflow.Infrastructure.Repositories;

namespace Nextflow.Tests.Integration;

/// <summary>
/// RN-CE36 contra o Postgres. Banco próprio, porque a regra olha todas as lojas e os outros testes deixam lojas ativas.
/// </summary>
public class LastStoreRuleTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await using var context = fixture.CreateContext();
        await context.Database.ExecuteSqlRawAsync("TRUNCATE stores, stock_locations CASCADE");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<Store> Seed(string name)
    {
        var store = new Store(new StoreRequestDto { Name = name });
        var location = new StockLocation(new LocationRequestDto { StoreId = store.Id, Name = StockLocation.DefaultName });
        store.Locations.Add(location);
        store.SetDefaults(location.Id, location.Id);

        await using var context = fixture.CreateContext();
        context.Set<Store>().Add(store);
        await context.SaveChangesAsync();
        return store;
    }

    private async Task<Domain.Dtos.ResultDto<bool>> Inactivate(Guid id)
    {
        await using var context = fixture.CreateContext();
        return await new DeleteStoreUseCase(new StoreRepository(context), new UnitOfWork(context), new ResultFactory<bool>()).Execute(id);
    }

    private async Task<bool> IsActiveInDatabase(Guid id)
    {
        await using var context = fixture.CreateContext();
        return await context.Set<Store>().Where(s => s.Id == id).Select(s => s.IsActive).SingleAsync();
    }

    [Fact]
    [Trait("Rule", "RN-CE36")]
    public async Task Should_InactivateUntilOneRemainsThenRefuse_When_StoresAreInactivatedInSequence()
    {
        var first = await Seed("Primeira");
        var second = await Seed("Segunda");

        (await Inactivate(first.Id)).IsSuccess.ShouldBeTrue();
        var refused = await Inactivate(second.Id);

        refused.ErrorCode.ShouldBe(400);
        refused.Message.ShouldBe("Não é possível inativar a última loja ativa.");
        (await IsActiveInDatabase(first.Id)).ShouldBeFalse();
        (await IsActiveInDatabase(second.Id)).ShouldBeTrue();
    }

    [Fact]
    [Trait("Rule", "RN-CE36")]
    public async Task Should_NotCountInactiveStore_When_CheckingTheLastOne()
    {
        var only = await Seed("Unica");

        var refused = await Inactivate(only.Id);

        refused.ErrorCode.ShouldBe(400);
        (await IsActiveInDatabase(only.Id)).ShouldBeTrue();
    }
}
