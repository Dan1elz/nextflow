using Microsoft.EntityFrameworkCore;

namespace Nextflow.Tests.Integration;

[Collection(PostgresCollection.Name)]
public class MigrationsTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Should_LeaveNoPendingMigration_When_AppliedFromScratch()
    {
        await using var context = fixture.CreateContext();

        (await context.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();
        (await context.Database.GetAppliedMigrationsAsync()).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Should_HaveNoModelChangesWithoutMigration()
    {
        await using var context = fixture.CreateContext();

        // Mudou modelo ou configuração sem gerar migration: este teste falha antes do deploy.
        context.Database.HasPendingModelChanges().ShouldBeFalse();
    }
}
