using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Nextflow.Application.Services;
using Nextflow.Domain.Dtos.Stock;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;
using Nextflow.Infrastructure.Database;
using Nextflow.Infrastructure.Repositories;
using Nextflow.Middlewares;
using NSubstitute;

namespace Nextflow.Tests.Integration;

/// <summary>
/// Livro-razão no Postgres real (RN-CE01, RN-CE14) e as regras que dependem do saldo (RN-CE35, RN-CE36, RN-CE51). Não há
/// tela que lance estoque antes da F1.6: os lançamentos passam pelo <see cref="StockLedger"/> com os repositórios reais,
/// cada um no próprio contexto e commit, como um use case faria.
/// </summary>
[Collection(AccessApiCollection.Name)]
public class StockLedgerApiTests(AccessApiFixture api)
{
    private static int _sequence;

    private static string Unique(string prefix) => $"{prefix}-{Interlocked.Increment(ref _sequence)}-{Guid.NewGuid():N}"[..24];

    private static async Task<JsonElement> Json(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<JsonElement>();

    private static string Id(JsonElement element) => element.GetProperty("id").GetString()!;

    private static StockLedger Ledger(AppDbContext context)
        => new(new StockBalanceRepository(context), new StockMovementRepository(context), new ProductVariantRepository(context),
            new ProductRepository(context), new StockLocationRepository(context), new LotRepository(context));

    private sealed record Setup(HttpClient Client, string ProductId, Guid VariantId, string StoreId, Guid MainLocationId, Guid DepositId);

    /// <summary>Produto simples (uma variação) e uma loja nova com o local "Loja" e um "Depósito".</summary>
    private async Task<Setup> Arrange(bool allowsNegative = false)
    {
        var client = await api.AdminAsync();
        var product = await client.PostAsJsonAsync("/api/products", new
        {
            name = Unique("Bombom"),
            productType = "Standard",
            saleUnit = "Unit",
            decimalPlaces = 0,
            allowsNegative,
            variants = new[] { new { sku = Unique("BOM"), price = 10m, isActive = true } },
        });
        product.StatusCode.ShouldBe(HttpStatusCode.Created, await product.Content.ReadAsStringAsync());
        var productId = Id(await Json(product));
        var variantId = Guid.Parse(Id((await Json(await client.GetAsync($"/api/products/{productId}/variants")))[0]));

        var store = await Json(await client.PostAsJsonAsync("/api/stores", new { name = Unique("Loja") }));
        var main = Guid.Parse(Id(store.GetProperty("locations")[0]));
        var deposit = await client.PostAsJsonAsync("/api/stock-locations", new { storeId = Id(store), name = "Depósito", isSellable = false });
        deposit.StatusCode.ShouldBe(HttpStatusCode.Created);

        return new(client, productId, variantId, Id(store), main, Guid.Parse(Id(await Json(deposit))));
    }

    private async Task<Guid> AdminId()
    {
        await using var context = api.CreateContext();
        return await context.Set<User>().Where(u => u.Email == AccessApiFixture.AdminEmail).Select(u => u.Id).SingleAsync();
    }

    private static StockLineDto Line(Guid variantId, Guid locationId, MovementTypeEnum type, decimal quantity, decimal? unitCost = null) => new()
    {
        VariantId = variantId,
        LocationId = locationId,
        Type = type,
        Reason = type == MovementTypeEnum.In ? MovementReasonEnum.Opening : MovementReasonEnum.Adjustment,
        Quantity = quantity,
        UnitCost = unitCost,
    };

    /// <summary>Lança e commita num contexto próprio. Devolve as falhas do livro-razão.</summary>
    private async Task<List<string>> Post(params StockLineDto[] lines)
    {
        await using var context = api.CreateContext();
        var failures = await Ledger(context).Post(new StockDocumentDto { SourceType = SourceTypeEnum.Manual, UserId = await AdminId() }, lines, CancellationToken.None);
        if (failures.Count == 0)
            await context.SaveChangesAsync();
        return [.. failures.Select(f => f.PropertyName)];
    }

    private static async Task<int> RunThroughMiddleware(Func<Task> action)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        await new GlobalResultMiddleware(_ => action(), Substitute.For<IWebHostEnvironment>()).Invoke(context);
        return context.Response.StatusCode;
    }

    [Fact]
    [Trait("Rule", "RN-CE01")]
    public async Task Should_KeepBalanceEqualToSumOfMovements_When_ManyMovements()
    {
        var setup = await Arrange(allowsNegative: true);
        var random = new Random(15);
        for (var i = 0; i < 30; i++)
        {
            var location = i % 3 == 0 ? setup.DepositId : setup.MainLocationId;
            var type = random.Next(3) == 0 ? MovementTypeEnum.Out : MovementTypeEnum.In;
            var quantity = random.Next(1, 20);
            decimal? cost = type == MovementTypeEnum.In && i % 2 == 0 ? (decimal)random.Next(5, 15) : null;
            // Linhas em par às vezes: a segunda enxerga a primeira no mesmo documento.
            if (i % 5 == 0)
                (await Post(Line(setup.VariantId, location, type, quantity, cost), Line(setup.VariantId, location, MovementTypeEnum.Out, 1))).ShouldBeEmpty();
            else
                (await Post(Line(setup.VariantId, location, type, quantity, cost))).ShouldBeEmpty();
        }

        await using var read = api.CreateContext();
        var balances = await read.Set<StockBalance>().Where(b => b.VariantId == setup.VariantId).ToListAsync();
        var movements = await read.Set<StockMovement>().Where(m => m.VariantId == setup.VariantId).ToListAsync();

        balances.Count.ShouldBe(2);
        foreach (var balance in balances)
        {
            var own = movements.Where(m => m.LocationId == balance.LocationId).OrderBy(m => m.CreatedAt).ToList();
            balance.Quantity.ShouldBe(own.Sum(m => m.Quantity));
            own.Last().BalanceAfter.ShouldBe(balance.Quantity);
            balance.Version.ShouldBe(own.Count);
        }
    }

    [Fact]
    [Trait("Rule", "RN-CE14")]
    public async Task Should_Return409_When_ConcurrentBalanceUpdate()
    {
        var setup = await Arrange();
        (await Post(Line(setup.VariantId, setup.MainLocationId, MovementTypeEnum.In, 10, 4m))).ShouldBeEmpty();
        var userId = await AdminId();
        var document = new StockDocumentDto { SourceType = SourceTypeEnum.Manual, UserId = userId };

        // As duas leem o saldo na mesma versão; a primeira grava e a segunda chega com a versão velha.
        await using var first = api.CreateContext();
        await using var second = api.CreateContext();
        (await Ledger(first).Post(document, [Line(setup.VariantId, setup.MainLocationId, MovementTypeEnum.Out, 3)], CancellationToken.None)).ShouldBeEmpty();
        (await Ledger(second).Post(document, [Line(setup.VariantId, setup.MainLocationId, MovementTypeEnum.Out, 4)], CancellationToken.None)).ShouldBeEmpty();

        (await RunThroughMiddleware(() => first.SaveChangesAsync())).ShouldBe(StatusCodes.Status200OK);
        (await RunThroughMiddleware(() => second.SaveChangesAsync())).ShouldBe(StatusCodes.Status409Conflict);

        await using var read = api.CreateContext();
        (await read.Set<StockBalance>().SingleAsync(b => b.VariantId == setup.VariantId)).Quantity.ShouldBe(7m);
        (await read.Set<StockMovement>().Where(m => m.VariantId == setup.VariantId).SumAsync(m => m.Quantity)).ShouldBe(7m);
    }

    [Fact]
    [Trait("Rule", "RN-CE31")]
    public async Task Should_Return409_When_ConcurrentInboundCostOnVariant()
    {
        var setup = await Arrange();
        (await Post(Line(setup.VariantId, setup.MainLocationId, MovementTypeEnum.In, 10, 4m))).ShouldBeEmpty();
        var document = new StockDocumentDto { SourceType = SourceTypeEnum.Manual, UserId = await AdminId() };

        // Locais diferentes: o saldo não colide, só o custo médio da variação. A segunda entrada chega com a versão velha.
        await using var first = api.CreateContext();
        await using var second = api.CreateContext();
        (await Ledger(first).Post(document, [Line(setup.VariantId, setup.MainLocationId, MovementTypeEnum.In, 10, 6m)], CancellationToken.None)).ShouldBeEmpty();
        (await Ledger(second).Post(document, [Line(setup.VariantId, setup.DepositId, MovementTypeEnum.In, 10, 10m)], CancellationToken.None)).ShouldBeEmpty();

        (await RunThroughMiddleware(() => first.SaveChangesAsync())).ShouldBe(StatusCodes.Status200OK);
        (await RunThroughMiddleware(() => second.SaveChangesAsync())).ShouldBe(StatusCodes.Status409Conflict);

        await using var read = api.CreateContext();
        (await read.Set<ProductVariant>().SingleAsync(v => v.Id == setup.VariantId)).AverageCost.ShouldBe(5m);
        (await read.Set<StockMovement>().Where(m => m.VariantId == setup.VariantId).SumAsync(m => m.Quantity)).ShouldBe(20m);
    }

    [Fact]
    [Trait("Rule", "RN-CE14")]
    public async Task Should_Return409_When_FirstBalanceOfTheKeyIsCreatedConcurrently()
    {
        var setup = await Arrange();
        var document = new StockDocumentDto { SourceType = SourceTypeEnum.Manual, UserId = await AdminId() };

        await using var first = api.CreateContext();
        await using var second = api.CreateContext();
        (await Ledger(first).Post(document, [Line(setup.VariantId, setup.DepositId, MovementTypeEnum.In, 2)], CancellationToken.None)).ShouldBeEmpty();
        (await Ledger(second).Post(document, [Line(setup.VariantId, setup.DepositId, MovementTypeEnum.In, 5)], CancellationToken.None)).ShouldBeEmpty();

        (await RunThroughMiddleware(() => first.SaveChangesAsync())).ShouldBe(StatusCodes.Status200OK);
        (await RunThroughMiddleware(() => second.SaveChangesAsync())).ShouldBe(StatusCodes.Status409Conflict);

        await using var read = api.CreateContext();
        (await read.Set<StockBalance>().SingleAsync(b => b.VariantId == setup.VariantId)).Quantity.ShouldBe(2m);
    }

    [Fact]
    [Trait("Rule", "RN-CE14")]
    public async Task Should_RefuseUpdateAndDelete_When_MovementIsSaved()
    {
        var setup = await Arrange();
        (await Post(Line(setup.VariantId, setup.MainLocationId, MovementTypeEnum.In, 1))).ShouldBeEmpty();

        await using var context = api.CreateContext();
        var movement = await context.Set<StockMovement>().FirstAsync(m => m.VariantId == setup.VariantId);
        var repository = new StockMovementRepository(context);

        Should.Throw<InvalidOperationException>(() => repository.Update(movement));
        Should.Throw<InvalidOperationException>(() => repository.Remove(movement));
    }

    [Fact]
    [Trait("Rule", "RN-CE32")]
    public async Task Should_Return422Failure_When_OutboundMakesNegativeInTheDatabase()
    {
        var setup = await Arrange();
        (await Post(Line(setup.VariantId, setup.MainLocationId, MovementTypeEnum.In, 2))).ShouldBeEmpty();

        (await Post(Line(setup.VariantId, setup.MainLocationId, MovementTypeEnum.Out, 3))).ShouldBe(["Lines[0].Quantity"]);

        await using var read = api.CreateContext();
        (await read.Set<StockMovement>().CountAsync(m => m.VariantId == setup.VariantId)).ShouldBe(1);
    }

    [Fact]
    [Trait("Rule", "RN-CE35")]
    public async Task Should_Return422_When_InactivatingLocationWithBalance()
    {
        var setup = await Arrange();
        (await Post(Line(setup.VariantId, setup.DepositId, MovementTypeEnum.In, 5))).ShouldBeEmpty();

        var store = await Json(await setup.Client.GetAsync($"/api/stores/{setup.StoreId}"));
        store.GetProperty("locations").EnumerateArray().Single(l => Id(l) == setup.DepositId.ToString()).GetProperty("hasStock").GetBoolean().ShouldBeTrue();
        (await setup.Client.DeleteAsync($"/api/stock-locations/{setup.DepositId}")).StatusCode.ShouldBe((HttpStatusCode)422);

        (await Post(Line(setup.VariantId, setup.DepositId, MovementTypeEnum.Out, 5))).ShouldBeEmpty();
        (await setup.Client.DeleteAsync($"/api/stock-locations/{setup.DepositId}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    [Trait("Rule", "RN-CE36")]
    public async Task Should_Return422_When_InactivatingStoreWithBalance()
    {
        var setup = await Arrange();
        (await Post(Line(setup.VariantId, setup.DepositId, MovementTypeEnum.In, 1))).ShouldBeEmpty();

        (await setup.Client.DeleteAsync($"/api/stores/{setup.StoreId}")).StatusCode.ShouldBe((HttpStatusCode)422);

        (await Post(Line(setup.VariantId, setup.DepositId, MovementTypeEnum.Out, 1))).ShouldBeEmpty();
        (await setup.Client.DeleteAsync($"/api/stores/{setup.StoreId}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    [Trait("Rule", "RN-CE51")]
    public async Task Should_Return422_When_RemovingVariantWithBalance()
    {
        var client = await api.AdminAsync();
        var prefix = Unique("CAM");
        var created = await client.PostAsJsonAsync("/api/products", new
        {
            name = Unique("Camiseta"),
            productType = "Apparel",
            saleUnit = "Unit",
            decimalPlaces = 0,
            attributes = new[] { new { name = "Tamanho", values = new[] { new { tempKey = "p", value = "P" }, new { tempKey = "m", value = "M" } } } },
            variants = new[]
            {
                new { attributeValueIds = new[] { "p" }, sku = $"{prefix}-P", price = 30m, isActive = true },
                new { attributeValueIds = new[] { "m" }, sku = $"{prefix}-M", price = 30m, isActive = true },
            },
        });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var productId = Id(await Json(created));
        var form = await Json(await client.GetAsync($"/api/products/{productId}/form"));
        var small = Guid.Parse(Id(form.GetProperty("variants")[0]));
        var store = await Json(await client.PostAsJsonAsync("/api/stores", new { name = Unique("Loja") }));
        (await Post(Line(small, Guid.Parse(Id(store.GetProperty("locations")[0])), MovementTypeEnum.In, 2))).ShouldBeEmpty();

        // Tira a variação P do formulário, como a tela faria.
        var body = JsonSerializer.Deserialize<Dictionary<string, object?>>(form.GetRawText())!;
        body.Remove("options");
        body.Remove("imageUrl");
        body["variants"] = form.GetProperty("variants").EnumerateArray().Skip(1).ToList();
        var response = await client.PutAsJsonAsync($"/api/products/{productId}", body);

        response.StatusCode.ShouldBe((HttpStatusCode)422, await response.Content.ReadAsStringAsync());
        (await Json(response)).GetProperty("fieldErrors").TryGetProperty("variants", out _).ShouldBeTrue();
    }

    [Fact]
    [Trait("Rule", "RN-CE08")]
    public async Task Should_ShowAvailableAndAverageCost_When_ProductHasMovements()
    {
        var setup = await Arrange();
        (await Post(Line(setup.VariantId, setup.MainLocationId, MovementTypeEnum.In, 10, 4m))).ShouldBeEmpty();
        (await Post(Line(setup.VariantId, setup.DepositId, MovementTypeEnum.In, 10, 6m))).ShouldBeEmpty();
        (await Post(Line(setup.VariantId, setup.MainLocationId, MovementTypeEnum.Out, 3))).ShouldBeEmpty();

        var detail = await Json(await setup.Client.GetAsync($"/api/products/{setup.ProductId}"));
        detail.GetProperty("availableQuantity").GetDecimal().ShouldBe(17m);
        detail.GetProperty("minAverageCost").GetDecimal().ShouldBe(5m);
        detail.GetProperty("maxAverageCost").GetDecimal().ShouldBe(5m);

        var variant = (await Json(await setup.Client.GetAsync($"/api/products/{setup.ProductId}/variants")))[0];
        variant.GetProperty("availableQuantity").GetDecimal().ShouldBe(17m);
        variant.GetProperty("averageCost").GetDecimal().ShouldBe(5m);
        variant.GetProperty("lastCost").GetDecimal().ShouldBe(6m);
        variant.GetProperty("marginPercent").GetDecimal().ShouldBe(50m);

        var filters = Uri.EscapeDataString(JsonSerializer.Serialize(new { search = detail.GetProperty("name").GetString(), storeId = setup.StoreId }));
        var item = (await Json(await setup.Client.GetAsync($"/api/products?filters={filters}"))).GetProperty("items")[0];
        item.GetProperty("availableQuantity").GetDecimal().ShouldBe(17m);
        item.GetProperty("hasLowStock").GetBoolean().ShouldBeFalse();

        var otherStore = await Json(await setup.Client.PostAsJsonAsync("/api/stores", new { name = Unique("Outra") }));
        var otherFilters = Uri.EscapeDataString(JsonSerializer.Serialize(new { search = detail.GetProperty("name").GetString(), storeId = Id(otherStore) }));
        (await Json(await setup.Client.GetAsync($"/api/products?filters={otherFilters}"))).GetProperty("items")[0]
            .GetProperty("availableQuantity").GetDecimal().ShouldBe(0m);

        var matrix = await Json(await setup.Client.GetAsync($"/api/products/{setup.ProductId}/stock?storeId={setup.StoreId}"));
        matrix.GetProperty("locations").EnumerateArray().Select(l => l.GetProperty("name").GetString()).ShouldBe(["Depósito", "Loja"]);
        var cells = matrix.GetProperty("rows")[0].GetProperty("cells").EnumerateArray().ToList();
        cells.Select(c => c.GetProperty("physical").GetDecimal()).ShouldBe([10m, 7m]);
        cells.Select(c => c.GetProperty("available").GetDecimal()).ShouldBe([10m, 7m]);
        cells.ShouldAllBe(c => c.GetProperty("reserved").GetDecimal() == 0m && !c.GetProperty("isBelowMin").GetBoolean());
    }
}
