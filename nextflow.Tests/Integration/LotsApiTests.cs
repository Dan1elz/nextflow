using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Models;

namespace Nextflow.Tests.Integration;

/// <summary>Lotes (F1.7) pela API, no Postgres real: ajuste com lote, aba Lotes, opções do ajuste e trava da RN-CE55.</summary>
[Collection(AccessApiCollection.Name)]
public class LotsApiTests(AccessApiFixture api)
{
    private const string ManualRoute = "/api/stock-movements/manual";
    private static int _sequence;

    private static string Unique(string prefix) => $"{prefix}-{Interlocked.Increment(ref _sequence)}-{Guid.NewGuid():N}"[..24];

    private static async Task<JsonElement> Json(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<JsonElement>();

    private static Guid Id(JsonElement element) => Guid.Parse(element.GetProperty("id").GetString()!);

    private sealed record Setup(HttpClient Client, Guid ProductId, Guid VariantId, Guid StoreId, Guid LocationId);

    /// <summary>Produto simples que controla lote (90 dias) e uma loja nova com o local "Loja".</summary>
    private async Task<Setup> Arrange()
    {
        var client = await api.AdminAsync();
        var product = await client.PostAsJsonAsync("/api/products", new
        {
            name = Unique("Trufa"),
            productType = "Perishable",
            saleUnit = "Unit",
            decimalPlaces = 0,
            tracksLots = true,
            shelfLifeDays = 90,
            variants = new[] { new { sku = Unique("TRU"), price = 5m, isActive = true } },
        });
        product.StatusCode.ShouldBe(HttpStatusCode.Created, await product.Content.ReadAsStringAsync());
        var productId = Id(await Json(product));
        var variantId = Id((await Json(await client.GetAsync($"/api/products/{productId}/variants")))[0]);

        var store = await Json(await client.PostAsJsonAsync("/api/stores", new { name = Unique("Loja") }));
        return new(client, productId, variantId, Id(store), Id(store.GetProperty("locations")[0]));
    }

    private static async Task<HttpResponseMessage> Opening(Setup setup, decimal quantity, object? newLot = null, Guid? lotId = null)
        => await setup.Client.PostAsJsonAsync(ManualRoute, new
        {
            variantId = setup.VariantId,
            locationId = setup.LocationId,
            lotId,
            newLot,
            type = "In",
            reason = "Opening",
            quantity,
            unitCost = 2m,
        });

    private static async Task ShouldFailOn(HttpResponseMessage response, string field)
    {
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity, text);
        (await Json(response)).GetProperty("fieldErrors").TryGetProperty(field, out _).ShouldBeTrue(text);
    }

    private static async Task<List<JsonElement>> LotOptions(Setup setup)
        => [.. (await Json(await setup.Client.GetAsync(
            $"/api/stock/lots/options?variantId={setup.VariantId}&locationId={setup.LocationId}"))).EnumerateArray()];

    [Fact]
    [Trait("Rule", "RN-CE10")]
    public async Task Should_CreateLotAndAddToExistingLot_When_ManualInboundWithLot()
    {
        var setup = await Arrange();

        await ShouldFailOn(await Opening(setup, 3), "lotId");

        var created = await Opening(setup, 3, newLot: new { code = " n-1 ", expiresOn = "2027-05-01" });
        created.StatusCode.ShouldBe(HttpStatusCode.NoContent, await created.Content.ReadAsStringAsync());

        var lot = (await LotOptions(setup)).Single();
        lot.GetProperty("code").GetString().ShouldBe("N-1");
        lot.GetProperty("quantity").GetDecimal().ShouldBe(3m);

        var added = await Opening(setup, 2, lotId: Id(lot));
        added.StatusCode.ShouldBe(HttpStatusCode.NoContent, await added.Content.ReadAsStringAsync());
        // Mesmo código com outra validade é recusado no campo do lote novo.
        await ShouldFailOn(await Opening(setup, 1, newLot: new { code = "N-1", expiresOn = "2027-06-01" }), "newLot.code");

        (await LotOptions(setup)).Single().GetProperty("quantity").GetDecimal().ShouldBe(5m);
        await using var read = api.CreateContext();
        (await read.Set<Lot>().CountAsync(l => l.VariantId == setup.VariantId)).ShouldBe(1);
        (await read.Set<StockBalance>().SingleAsync(b => b.VariantId == setup.VariantId)).Quantity.ShouldBe(5m);
    }

    [Fact]
    [Trait("Rule", "RN-CE15")]
    public async Task Should_ListLotsInExpiryOrderWithDaysToExpire_When_ProductLotsRequested()
    {
        var setup = await Arrange();
        (await Opening(setup, 4, newLot: new { code = "FUTURO", expiresOn = "2099-01-01" })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Opening(setup, 2, newLot: new { code = "VENCIDO", expiresOn = "2020-01-01" })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Opening(setup, 1, newLot: new { code = "ZERADO", expiresOn = "2050-01-01" })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var zeroed = (await LotOptions(setup)).Single(l => l.GetProperty("code").GetString() == "ZERADO");
        var loss = await setup.Client.PostAsJsonAsync(ManualRoute, new
        {
            variantId = setup.VariantId,
            locationId = setup.LocationId,
            lotId = Id(zeroed),
            type = "Out",
            reason = "Breakage",
            quantity = 1,
        });
        loss.StatusCode.ShouldBe(HttpStatusCode.NoContent, await loss.Content.ReadAsStringAsync());

        var lots = (await Json(await setup.Client.GetAsync($"/api/products/{setup.ProductId}/lots?storeId={setup.StoreId}")))
            .EnumerateArray().ToList();

        // Zerado fica fora; o vencido vem primeiro (a ordem da baixa).
        lots.Select(l => l.GetProperty("code").GetString()).ShouldBe(["VENCIDO", "FUTURO"]);
        lots[0].GetProperty("isExpired").GetBoolean().ShouldBeTrue();
        lots[0].GetProperty("daysToExpire").GetInt32().ShouldBeLessThan(0);
        lots[1].GetProperty("isExpired").GetBoolean().ShouldBeFalse();
        lots[1].GetProperty("isExpiringSoon").GetBoolean().ShouldBeFalse();
        lots[1].GetProperty("quantity").GetDecimal().ShouldBe(4m);

        // As opções do ajuste trazem todos os lotes da variação, inclusive o zerado.
        (await LotOptions(setup)).Select(l => l.GetProperty("code").GetString()).ShouldBe(["VENCIDO", "ZERADO", "FUTURO"]);
    }

    [Fact]
    [Trait("Rule", "RN-CE55")]
    public async Task Should_Return422_When_TogglingLotTrackingWithBalance()
    {
        var setup = await Arrange();
        (await Opening(setup, 2, newLot: new { code = "L-1", expiresOn = "2027-01-01" })).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var form = await Json(await setup.Client.GetAsync($"/api/products/{setup.ProductId}/form"));
        var body = JsonSerializer.Deserialize<Dictionary<string, object?>>(form.GetRawText())!;
        body.Remove("options");
        body.Remove("imageUrl");
        body["tracksLots"] = false;

        await ShouldFailOn(await setup.Client.PutAsJsonAsync($"/api/products/{setup.ProductId}", body), "tracksLots");

        await using var read = api.CreateContext();
        (await read.Set<Product>().SingleAsync(p => p.Id == setup.ProductId)).TracksLots.ShouldBeTrue();
    }
}
