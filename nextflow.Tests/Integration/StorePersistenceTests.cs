using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Dtos.Stores;
using Nextflow.Domain.Models;

namespace Nextflow.Tests.Integration;

[Collection(PostgresCollection.Name)]
public class StorePersistenceTests(PostgresFixture fixture)
{
    [Fact]
    [Trait("Rule", "RN-CE35")]
    public async Task Should_SaveStoreAndLocationPointingToEachOther_When_InTheSameCommit()
    {
        var store = new Store(new StoreRequestDto { Name = "Par circular" });
        var location = new StockLocation(new LocationRequestDto { StoreId = store.Id, Name = "Loja" });
        store.Locations.Add(location);
        store.SetDefaults(location.Id, location.Id);

        await using (var context = fixture.CreateContext())
        {
            context.Set<Store>().Add(store);
            await context.SaveChangesAsync();
        }

        await using var read = fixture.CreateContext();
        (await read.Set<Store>().SingleAsync(s => s.Id == store.Id)).DefaultSaleLocationId.ShouldBe(location.Id);
    }

    [Fact]
    [Trait("Rule", "RN-CE35")]
    public async Task Should_RejectDefaultThatDoesNotExist_When_TheTransactionEnds()
    {
        var store = new Store(new StoreRequestDto { Name = "FK adiada" });
        store.SetDefaults(Guid.NewGuid(), Guid.NewGuid());
        await using var context = fixture.CreateContext();
        context.Set<Store>().Add(store);

        await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }
}
