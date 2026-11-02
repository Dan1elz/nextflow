using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Models;

namespace Nextflow.Tests.Integration;

/// <summary>Lançamento manual (T9, F1.6) pela API, no Postgres real: motivos, custo de abertura, observação e negativo.</summary>
[Collection(AccessApiCollection.Name)]
public class ManualMovementApiTests(AccessApiFixture api)
{
    private const string Route = "/api/stock-movements/manual";
    private static int _sequence;

    private static string Unique(string prefix) => $"{prefix}-{Interlocked.Increment(ref _sequence)}-{Guid.NewGuid():N}"[..24];

    private static async Task<JsonElement> Json(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<JsonElement>();

    private static string Id(JsonElement element) => element.GetProperty("id").GetString()!;

    private sealed record Setup(HttpClient Client, Guid VariantId, Guid LocationId);

    private async Task<Setup> Arrange(bool allowsNegative = false)
    {
        var client = await api.AdminAsync();
        var product = await client.PostAsJsonAsync("/api/products", new
        {
            name = Unique("Trufa"),
            productType = "Standard",
            saleUnit = "Unit",
            decimalPlaces = 0,
            allowsNegative,
            variants = new[] { new { sku = Unique("TRU"), price = 10m, isActive = true } },
        });
        product.StatusCode.ShouldBe(HttpStatusCode.Created, await product.Content.ReadAsStringAsync());
        var productId = Id(await Json(product));
        var variantId = Guid.Parse(Id((await Json(await client.GetAsync($"/api/products/{productId}/variants")))[0]));

        var store = await Json(await client.PostAsJsonAsync("/api/stores", new { name = Unique("Loja") }));
        return new(client, variantId, Guid.Parse(Id(store.GetProperty("locations")[0])));
    }

    private static Task<HttpResponseMessage> Post(Setup setup, string type, string reason, decimal quantity, decimal? unitCost = null, string? note = null)
        => setup.Client.PostAsJsonAsync(Route, new
        {
            variantId = setup.VariantId,
            locationId = setup.LocationId,
            type,
            reason,
            quantity,
            unitCost,
            note,
        });

    private static async Task ShouldFailOn(HttpResponseMessage response, string field)
    {
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity, text);
        (await Json(response)).GetProperty("fieldErrors").TryGetProperty(field, out _).ShouldBeTrue(text);
    }

    private async Task<int> MovementCount(Guid variantId)
    {
        await using var context = api.CreateContext();
        return await context.Set<StockMovement>().CountAsync(m => m.VariantId == variantId);
    }

    [Theory]
    [Trait("Rule", "RN-CE02")]
    [InlineData("In", "Purchase")]
    [InlineData("Out", "Sale")]
    [InlineData("In", "TransferIn")]
    [InlineData("Out", "TransferOut")]
    [InlineData("In", "CountAdjustment")]
    public async Task Should_Return422_When_ManualMovementUsesSystemReason(string type, string reason)
    {
        var setup = await Arrange(allowsNegative: true);

        await ShouldFailOn(await Post(setup, type, reason, 1), "reason");
        (await MovementCount(setup.VariantId)).ShouldBe(0);
    }

    [Theory]
    [Trait("Rule", "L5")]
    [InlineData("In", "Adjustment")]
    [InlineData("Out", "Breakage")]
    public async Task Should_Return422_When_UnitCostWithoutOpening(string type, string reason)
    {
        var setup = await Arrange(allowsNegative: true);

        await ShouldFailOn(await Post(setup, type, reason, 1, unitCost: 3m, note: "conferência"), "unitCost");
        (await MovementCount(setup.VariantId)).ShouldBe(0);
    }

    [Fact]
    [Trait("Rule", "L5")]
    [Trait("Rule", "RN-CE31")]
    public async Task Should_UpdateAverageCost_When_OpeningWithCost()
    {
        var setup = await Arrange();

        (await Post(setup, "In", "Opening", 10, unitCost: 4m)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Post(setup, "In", "Opening", 10, unitCost: 6m)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await using var context = api.CreateContext();
        var variant = await context.Set<ProductVariant>().SingleAsync(v => v.Id == setup.VariantId);
        variant.AverageCost.ShouldBe(5m);
        variant.LastCost.ShouldBe(6m);
        var balance = await context.Set<StockBalance>().SingleAsync(b => b.VariantId == setup.VariantId);
        balance.Quantity.ShouldBe(20m);
    }

    [Fact]
    [Trait("Rule", "L4")]
    public async Task Should_Return422_When_AdjustmentWithoutNote()
    {
        var setup = await Arrange(allowsNegative: true);

        await ShouldFailOn(await Post(setup, "In", "Adjustment", 1, note: "  "), "note");
        (await MovementCount(setup.VariantId)).ShouldBe(0);
    }

    [Fact]
    [Trait("Rule", "RN-CE32")]
    public async Task Should_Return422OnQuantity_When_ManualOutputMakesBalanceNegative()
    {
        var setup = await Arrange();
        (await Post(setup, "In", "Opening", 2)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await ShouldFailOn(await Post(setup, "Out", "Breakage", 3), "quantity");

        (await Post(setup, "Out", "Theft", 2)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await using var context = api.CreateContext();
        var movements = await context.Set<StockMovement>().Where(m => m.VariantId == setup.VariantId).OrderBy(m => m.CreatedAt).ToListAsync();
        movements.Select(m => m.Quantity).ShouldBe([2m, -2m]);
        movements.Last().BalanceAfter.ShouldBe(0m);
    }

    [Fact]
    [Trait("Rule", "RN-CE32")]
    public async Task Should_AllowNegative_When_ProductAllowsNegative()
    {
        var setup = await Arrange(allowsNegative: true);

        (await Post(setup, "Out", "InternalUse", 1)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await using var context = api.CreateContext();
        (await context.Set<StockBalance>().SingleAsync(b => b.VariantId == setup.VariantId)).Quantity.ShouldBe(-1m);
    }

    [Fact]
    [Trait("Rule", "T9")]
    public async Task Should_Return422_When_LotInformedBeforeLotControl()
    {
        var setup = await Arrange();

        var response = await setup.Client.PostAsJsonAsync(Route, new
        {
            variantId = setup.VariantId,
            locationId = setup.LocationId,
            lotId = Guid.NewGuid(),
            type = "In",
            reason = "Opening",
            quantity = 1,
        });

        await ShouldFailOn(response, "lotId");
    }
}
