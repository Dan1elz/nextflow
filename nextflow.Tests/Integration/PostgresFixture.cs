using Microsoft.EntityFrameworkCore;
using Nextflow.Infrastructure.Database;
using Testcontainers.PostgreSql;

namespace Nextflow.Tests.Integration;

/// <summary>
/// Postgres real e descartável (Testcontainers), com as migrations aplicadas do zero. Os testes unitários usam mocks e
/// não enxergam problemas de persistência (índices únicos, FKs, CHECK, change tracker). Exige Docker.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public AppDbContext CreateContext()
        => new((DbContextOptions<AppDbContext>)DatabaseOptions
            .Configure(new DbContextOptionsBuilder<AppDbContext>(), ConnectionString)
            .Options);

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(Name)]
public class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
