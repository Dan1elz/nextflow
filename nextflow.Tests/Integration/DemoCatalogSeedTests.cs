using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Infrastructure.Services;
using Nextflow.Domain.Models;
using Nextflow.Domain.Utils;
using Nextflow.Infrastructure.Database;
using Nextflow.Infrastructure.Seeders.Demo;
using Npgsql;

namespace Nextflow.Tests.Integration;

/// <summary>
/// Seed de demonstração do catálogo (F1.11) num Postgres real: cada cenário sobe a API inteira sobre um banco vazio só
/// dele, porque o seed roda no startup (migrations + seeders), como no dev e no staging.
/// </summary>
public sealed class DemoSeedFixture : IAsyncLifetime
{
    private readonly PostgresFixture _postgres = new();
    private readonly List<WebApplicationFactory<Program>> _hosts = [];
    private int _sequence;

    public async Task InitializeAsync() => await _postgres.InitializeAsync();

    /// <summary>Banco novo e vazio no mesmo container (as migrations rodam no startup da API).</summary>
    public async Task<string> NewDatabaseAsync()
    {
        var name = $"demo_{Interlocked.Increment(ref _sequence)}_{Guid.NewGuid():N}"[..20];
        await using var connection = new NpgsqlConnection(_postgres.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
        await command.ExecuteNonQueryAsync();
        return new NpgsqlConnectionStringBuilder(_postgres.ConnectionString) { Database = name }.ConnectionString;
    }

    /// <summary>Sobe a API sobre o banco: migrations e seeders rodam, como num restart.</summary>
    public void StartApi(string connectionString, bool demoEnabled, int historyMonths = 0)
    {
        var host = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
            builder.UseSetting("JwtSettings:Key", "chave-apenas-para-testes-de-integracao-0001");
            builder.UseSetting("Seed:Admin:Password", AccessApiFixture.AdminPassword);
            builder.UseSetting("Seed:Demo:Enabled", demoEnabled ? "true" : "false");
            builder.UseSetting("Seed:Demo:HistoryMonths", historyMonths.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.ConfigureTestServices(services => services.AddSingleton<IFileStorage>(new InMemoryFileStorage()));
        });
        _ = host.Services; // constrói o host: roda Program.cs até o startup
        _hosts.Add(host);
    }

    public AppDbContext CreateContext(string connectionString)
        => new((DbContextOptions<AppDbContext>)DatabaseOptions
            .Configure(new DbContextOptionsBuilder<AppDbContext>(), connectionString)
            .Options);

    public async Task DisposeAsync()
    {
        foreach (var host in _hosts)
            await host.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

/// <summary>
/// Sem paralelismo: o Program.cs troca o logger estático do Serilog ao subir, e dois hosts subindo ao mesmo tempo (aqui e na
/// <see cref="AccessApiCollection"/>) derrubam o startup com "logger is already frozen".
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class DemoSeedCollection : ICollectionFixture<DemoSeedFixture>
{
    public const string Name = "demo-seed";
}

[Collection(DemoSeedCollection.Name)]
public class DemoCatalogSeedTests(DemoSeedFixture fixture)
{
    private sealed record Counts(int Stores, int Locations, int Categories, int Products, int Variants, int Suppliers,
        int ProductSuppliers, int ProductEvents, int Minimums, int Balances, int Movements, int UserStores);

    private async Task<Counts> Count(string connectionString)
    {
        await using var context = fixture.CreateContext(connectionString);
        return new Counts(
            await context.Set<Store>().CountAsync(),
            await context.Set<StockLocation>().CountAsync(),
            await context.Set<Category>().CountAsync(),
            await context.Set<Product>().CountAsync(),
            await context.Set<ProductVariant>().CountAsync(),
            await context.Set<Supplier>().CountAsync(),
            await context.Set<ProductSupplier>().CountAsync(),
            await context.Set<ProductEvent>().CountAsync(),
            await context.Set<StockLocationMinimum>().CountAsync(),
            await context.Set<StockBalance>().CountAsync(),
            await context.Set<StockMovement>().CountAsync(),
            await context.Set<UserStore>().CountAsync());
    }

    private async Task<string> SeededDatabase()
    {
        var connection = await fixture.NewDatabaseAsync();
        fixture.StartApi(connection, demoEnabled: true);
        return connection;
    }

    [Fact]
    public async Task Should_CreateStoresCatalogAndOpeningBalances_When_FlagEnabledOnEmptyDatabase()
    {
        var connection = await SeededDatabase();
        await using var context = fixture.CreateContext(connection);

        var stores = await context.Set<Store>().Include(s => s.Locations).ToListAsync();
        stores.Select(s => s.Name).ShouldBe(DemoCatalog.StoreNames, ignoreOrder: true);
        foreach (var store in stores)
        {
            store.Locations.Select(l => l.Name).ShouldBe([StockLocation.DefaultName, DemoCatalog.DepositName], ignoreOrder: true);
            var sale = store.Locations.Single(l => l.Name == StockLocation.DefaultName);
            var deposit = store.Locations.Single(l => l.Name == DemoCatalog.DepositName);
            sale.IsSellable.ShouldBeTrue();
            deposit.IsSellable.ShouldBeFalse();
            store.DefaultSaleLocationId.ShouldBe(sale.Id);
            store.DefaultReceiveLocationId.ShouldBe(deposit.Id);
        }

        var admin = await context.Set<User>().SingleAsync(u => u.Email == AccessApiFixture.AdminEmail);
        (await context.Set<UserStore>().CountAsync(us => us.UserId == admin.Id)).ShouldBe(2);

        var products = await context.Set<Product>().Include(p => p.Variants).Include(p => p.Suppliers).ToListAsync();
        products.Count.ShouldBeInRange(35, 50);
        products.ShouldContain(p => p.HasVariants);
        products.ShouldContain(p => !p.HasVariants && p.Variants.Count == 1);
        products.ShouldAllBe(p => p.Suppliers.Count >= 1 && p.Suppliers.Count(s => s.IsPreferred) == 1);
        var stocked = await context.Set<StockBalance>().Select(b => b.VariantId).Distinct().ToListAsync();
        products.SelectMany(p => p.Variants).Where(v => stocked.Contains(v.Id)).ShouldAllBe(v => v.AverageCost > 0);
        products.ShouldContain(p => p.DefaultMinQuantity == 0, "item de temporada abre sem saldo");

        var suppliers = await context.Set<Supplier>().ToListAsync();
        suppliers.Count.ShouldBe(DemoCatalog.Suppliers.Length);
        suppliers.Select(s => s.LeadTimeDays).Distinct().Count().ShouldBeGreaterThan(1);
        (await context.Set<ProductSupplier>().AnyAsync(ps => ps.OrderMultiple > 1)).ShouldBeTrue();

        var linkedEvents = await context.Set<ProductEvent>().Select(pe => pe.Event.Name).Distinct().ToListAsync();
        linkedEvents.ShouldBe(
            [CommercialCalendar.EasterName, CommercialCalendar.MothersDayName, CommercialCalendar.ValentinesDayName, CommercialCalendar.ChristmasName],
            ignoreOrder: true);

        (await context.Set<StockLocationMinimum>().CountAsync()).ShouldBeGreaterThan(0);
        (await context.Set<StockBalance>().CountAsync(b => b.Quantity > 0)).ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Should_BeIdempotent_When_SeedRunsTwice()
    {
        var connection = await SeededDatabase();
        var first = await Count(connection);

        fixture.StartApi(connection, demoEnabled: true);

        var second = await Count(connection);
        second.ShouldBe(first);
        first.Products.ShouldBeGreaterThan(0);
        first.Movements.ShouldBeGreaterThan(0);
    }

    [Fact]
    [Trait("Rule", "RN-CE01")]
    public async Task Should_CreateBalancesOnlyThroughLedger_When_Seeding()
    {
        var connection = await SeededDatabase();
        await using var context = fixture.CreateContext(connection);

        var movements = await context.Set<StockMovement>().ToListAsync();
        movements.ShouldNotBeEmpty();
        movements.ShouldAllBe(m => m.Reason == MovementReasonEnum.Opening && m.Type == MovementTypeEnum.In
            && m.SourceType == SourceTypeEnum.Manual && m.UnitCost > 0);

        var sums = movements
            .GroupBy(m => (m.VariantId, m.LocationId))
            .ToDictionary(g => g.Key, g => g.Sum(m => m.Quantity));

        var balances = await context.Set<StockBalance>().ToListAsync();
        balances.Count.ShouldBe(sums.Count);
        foreach (var balance in balances)
            balance.Quantity.ShouldBe(sums[(balance.VariantId, balance.LocationId)]);
    }

    [Fact]
    public async Task Should_NotCreateDemoData_When_FlagDisabled()
    {
        var connection = await fixture.NewDatabaseAsync();
        fixture.StartApi(connection, demoEnabled: false);

        var counts = await Count(connection);
        counts.Stores.ShouldBe(0);
        counts.Categories.ShouldBe(0);
        counts.Products.ShouldBe(0);
        counts.Suppliers.ShouldBe(0);
        counts.Balances.ShouldBe(0);
        counts.Movements.ShouldBe(0);
    }
}
