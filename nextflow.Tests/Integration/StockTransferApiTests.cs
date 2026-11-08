using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;

namespace Nextflow.Tests.Integration;

/// <summary>Transferência de estoque entre locais (RN-CE62, F3.4b parte 1) pela API, no Postgres real.</summary>
[Collection(AccessApiCollection.Name)]
public class StockTransferApiTests(AccessApiFixture api)
{
    private const string Route = "/api/stock-transfers";
    private static int _sequence;

    private static string Unique(string prefix) => $"{prefix}-{Interlocked.Increment(ref _sequence)}-{Guid.NewGuid():N}"[..24];

    private static async Task<JsonElement> Json(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<JsonElement>();

    private static Guid Id(JsonElement element) => Guid.Parse(element.GetProperty("id").GetString()!);

    private sealed record Setup(HttpClient Client, Guid VariantId, Guid From, Guid To);

    /// <summary>Produto (com ou sem lote) e duas lojas novas; a origem é o local "Loja" da primeira, o destino o da segunda.</summary>
    private async Task<Setup> Arrange(bool tracksLots)
    {
        var client = await api.AdminAsync();
        var product = await client.PostAsJsonAsync("/api/products", new
        {
            name = Unique("Trufa"),
            productType = tracksLots ? "Perishable" : "Standard",
            saleUnit = "Unit",
            decimalPlaces = 0,
            tracksLots,
            shelfLifeDays = tracksLots ? 90 : (int?)null,
            variants = new[] { new { sku = Unique("TRU"), price = 10m, isActive = true } },
        });
        product.StatusCode.ShouldBe(HttpStatusCode.Created, await product.Content.ReadAsStringAsync());
        var productId = Id(await Json(product));
        var variantId = Id((await Json(await client.GetAsync($"/api/products/{productId}/variants")))[0]);

        var from = await Json(await client.PostAsJsonAsync("/api/stores", new { name = Unique("Centro") }));
        var to = await Json(await client.PostAsJsonAsync("/api/stores", new { name = Unique("Shopping") }));
        return new(client, variantId, Id(from.GetProperty("locations")[0]), Id(to.GetProperty("locations")[0]));
    }

    private static async Task Opening(Setup setup, decimal quantity, decimal unitCost, object? newLot = null)
    {
        var response = await setup.Client.PostAsJsonAsync("/api/stock-movements/manual", new
        {
            variantId = setup.VariantId,
            locationId = setup.From,
            newLot,
            type = "In",
            reason = "Opening",
            quantity,
            unitCost,
        });
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
    }

    private static Task<HttpResponseMessage> Transfer(Setup setup, decimal quantity)
        => setup.Client.PostAsJsonAsync(Route, new
        {
            fromLocationId = setup.From,
            toLocationId = setup.To,
            items = new[] { new { variantId = setup.VariantId, quantity } },
        });

    [Fact]
    [Trait("Rule", "RN-CE62")]
    public async Task Should_MoveStockWithSameLots_When_TransferringLotItem()
    {
        var setup = await Arrange(tracksLots: true);
        await Opening(setup, 4, 2m, new { code = "VENCE-ANTES", expiresOn = "2030-01-01" });
        await Opening(setup, 10, 2m, new { code = "VENCE-DEPOIS", expiresOn = "2031-01-01" });

        var response = await Transfer(setup, 10);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var body = await Json(response);
        body.GetProperty("number").GetInt32().ShouldBeGreaterThan(0);

        await using var read = api.CreateContext();
        var lots = await read.Set<Lot>().Where(l => l.VariantId == setup.VariantId).ToDictionaryAsync(l => l.Code, l => l.Id);
        var balances = await read.Set<StockBalance>().Where(b => b.VariantId == setup.VariantId).ToListAsync();
        decimal At(Guid location, string lot) => balances.SingleOrDefault(b => b.LocationId == location && b.LotId == lots[lot])?.Quantity ?? 0;

        // FEFO na origem: 4 do que vence antes e 6 do outro; o destino recebe nos mesmos lotes.
        At(setup.From, "VENCE-ANTES").ShouldBe(0m);
        At(setup.From, "VENCE-DEPOIS").ShouldBe(4m);
        At(setup.To, "VENCE-ANTES").ShouldBe(4m);
        At(setup.To, "VENCE-DEPOIS").ShouldBe(6m);

        // Saldo = soma dos movimentos (RN-CE01) e os movimentos apontam para a transferência.
        var movements = await read.Set<StockMovement>().Where(m => m.VariantId == setup.VariantId).ToListAsync();
        foreach (var balance in balances)
            balance.Quantity.ShouldBe(movements.Where(m => m.LocationId == balance.LocationId && m.LotId == balance.LotId).Sum(m => m.Quantity));
        var transferMovements = movements.Where(m => m.SourceType == SourceTypeEnum.Transfer).ToList();
        transferMovements.ShouldAllBe(m => m.SourceId == Id(body));
        transferMovements.Where(m => m.Reason == MovementReasonEnum.TransferOut).Sum(m => m.Quantity).ShouldBe(-10m);
        transferMovements.Where(m => m.Reason == MovementReasonEnum.TransferIn).Sum(m => m.Quantity).ShouldBe(10m);
    }

    [Fact]
    [Trait("Rule", "RN-CE32")]
    public async Task Should_Return422_When_TransferExceedsAvailable()
    {
        var setup = await Arrange(tracksLots: false);
        await Opening(setup, 3, 2m);

        var response = await Transfer(setup, 5);

        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity, text);
        (await Json(response)).GetProperty("fieldErrors").TryGetProperty("items[0].quantity", out _).ShouldBeTrue(text);
        await using var read = api.CreateContext();
        (await read.Set<StockTransfer>().AnyAsync(t => t.FromLocationId == setup.From)).ShouldBeFalse();
        (await read.Set<StockMovement>().AnyAsync(m => m.VariantId == setup.VariantId && m.SourceType == SourceTypeEnum.Transfer)).ShouldBeFalse();
        (await read.Set<StockBalance>().SingleAsync(b => b.VariantId == setup.VariantId)).Quantity.ShouldBe(3m);
    }

    [Fact]
    [Trait("Rule", "RN-CE62")]
    public async Task Should_KeepAverageCost_When_Transferring()
    {
        var setup = await Arrange(tracksLots: false);
        await Opening(setup, 10, 4m);
        await using (var before = api.CreateContext())
            (await before.Set<ProductVariant>().SingleAsync(v => v.Id == setup.VariantId)).AverageCost.ShouldBe(4m);

        (await Transfer(setup, 6)).StatusCode.ShouldBe(HttpStatusCode.Created);

        await using var read = api.CreateContext();
        (await read.Set<ProductVariant>().SingleAsync(v => v.Id == setup.VariantId)).AverageCost.ShouldBe(4m);
        var balances = await read.Set<StockBalance>().Where(b => b.VariantId == setup.VariantId).ToListAsync();
        balances.Single(b => b.LocationId == setup.From).Quantity.ShouldBe(4m);
        balances.Single(b => b.LocationId == setup.To).Quantity.ShouldBe(6m);
    }

    [Fact]
    public async Task Should_Return422_When_DestinationEqualsOrigin()
    {
        var setup = await Arrange(tracksLots: false);
        var response = await setup.Client.PostAsJsonAsync(Route, new
        {
            fromLocationId = setup.From,
            toLocationId = setup.From,
            items = new[] { new { variantId = setup.VariantId, quantity = 1m } },
        });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await Json(response)).GetProperty("fieldErrors").TryGetProperty("toLocationId", out _).ShouldBeTrue();
    }
}
