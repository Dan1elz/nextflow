using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;

namespace Nextflow.Tests.Integration;

/// <summary>Recebimento do pedido de compra (T-CP4) pela API, no Postgres real (F2.8).</summary>
[Collection(AccessApiCollection.Name)]
public class PurchaseReceivingApiTests(AccessApiFixture api)
{
    private const string Route = "/api/purchase-orders";
    private static int _sequence;

    private static string Unique(string prefix) => $"{prefix}-{Interlocked.Increment(ref _sequence)}-{Guid.NewGuid():N}"[..24];

    private static async Task<JsonElement> Json(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<JsonElement>();

    private static Guid Id(JsonElement element) => Guid.Parse(element.GetProperty("id").GetString()!);

    private static uint Version(JsonElement order) => order.GetProperty("version").GetUInt32();

    private static string NewCnpj()
    {
        var digits = (Random.Shared.NextInt64(10_000_000, 99_999_999) + "0001").Select(c => c - '0').ToList();
        foreach (var weights in new[] { new[] { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 }, new[] { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 } })
        {
            var rest = digits.Select((digit, index) => digit * weights[index]).Sum() % 11;
            digits.Add(rest < 2 ? 0 : 11 - rest);
        }
        return string.Concat(digits);
    }

    private sealed record Setup(HttpClient Client, Guid SupplierId, Guid VariantId, Guid StoreId, Guid ShopId, Guid ProductId);

    /// <summary>Fornecedor, produto simples vinculado a ele e uma loja nova com o local "Loja".</summary>
    private async Task<Setup> Arrange(bool linked = true, bool tracksLots = false)
    {
        var client = await api.AdminAsync();
        var supplier = await client.PostAsJsonAsync("/api/suppliers", new { name = Unique("Forn"), cnpj = NewCnpj(), leadTimeDays = 5 });
        supplier.StatusCode.ShouldBe(HttpStatusCode.Created, await supplier.Content.ReadAsStringAsync());
        var supplierId = Id(await Json(supplier));

        var product = await client.PostAsJsonAsync("/api/products", new
        {
            name = Unique("Bombom"),
            productType = "Standard",
            tracksLots,
            shelfLifeDays = tracksLots ? 90 : (int?)null,
            saleUnit = "Unit",
            decimalPlaces = 0,
            variants = new[] { new { sku = Unique("BOM"), price = 10m, isActive = true } },
            suppliers = linked ? new object[] { new { supplierId, isPreferred = true } } : [],
        });
        product.StatusCode.ShouldBe(HttpStatusCode.Created, await product.Content.ReadAsStringAsync());
        var productId = Id(await Json(product));
        var variantId = Id((await Json(await client.GetAsync($"/api/products/{productId}/variants")))[0]);

        var store = await Json(await client.PostAsJsonAsync("/api/stores", new { name = Unique("Loja") }));
        return new(client, supplierId, variantId, Id(store), Id(store.GetProperty("locations")[0]), productId);
    }

    /// <summary>Pedido com um item, já enviado ao fornecedor (Pendente).</summary>
    private static async Task<JsonElement> PendingOrder(Setup setup, decimal quantity, decimal unitCost, bool send = true)
    {
        var created = await setup.Client.PostAsJsonAsync(Route, new
        {
            supplierId = setup.SupplierId,
            receiveLocationId = setup.ShopId,
            confirmWarnings = true,
            items = new[] { new { variantId = setup.VariantId, quantity, unitCost } },
        });
        created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var order = await Json(created);
        if (!send)
            return order;

        var sent = await setup.Client.PostAsJsonAsync($"{Route}/{Id(order)}/send", new { version = Version(order) });
        sent.StatusCode.ShouldBe(HttpStatusCode.OK, await sent.Content.ReadAsStringAsync());
        return await Json(sent);
    }

    private static object ReceiveBody(JsonElement order, uint? version = null)
        => new { version = version ?? Version(order), items = order.GetProperty("items").EnumerateArray().Select(i => new { itemId = Id(i) }).ToArray() };

    private static async Task<JsonElement> Receive(Setup setup, JsonElement order, HttpClient? client = null)
    {
        var response = await (client ?? setup.Client).PostAsJsonAsync($"{Route}/{Id(order)}/receive", ReceiveBody(order));
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await Json(response);
    }

    private static async Task ShouldFailOn(HttpResponseMessage response, string field)
    {
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity, text);
        (await Json(response)).GetProperty("fieldErrors").TryGetProperty(field, out _).ShouldBeTrue(text);
    }

    [Fact]
    [Trait("Rule", "RN-CP02")]
    public async Task Should_PostEntriesAndUpdateCost_When_ReceivingPurchaseOrder()
    {
        var setup = await Arrange();
        var first = await Receive(setup, await PendingOrder(setup, 10, 4m));
        first.GetProperty("items")[0].GetProperty("averageCostAfter").GetDecimal().ShouldBe(4m);

        var received = await Receive(setup, await PendingOrder(setup, 10, 6m));

        received.GetProperty("status").GetString().ShouldBe("Received");
        received.GetProperty("receivedAt").ValueKind.ShouldBe(JsonValueKind.String);
        received.GetProperty("receivedByName").GetString().ShouldNotBeNullOrWhiteSpace();
        received.GetProperty("items")[0].GetProperty("averageCostAfter").GetDecimal().ShouldBe(5m);

        await using var read = api.CreateContext();
        var movements = await read.Set<StockMovement>().Where(m => m.VariantId == setup.VariantId).ToListAsync();
        movements.Count.ShouldBe(2);
        movements.ShouldAllBe(m => m.Reason == MovementReasonEnum.Purchase && m.SourceType == SourceTypeEnum.PurchaseOrder
            && m.LocationId == setup.ShopId && m.Type == MovementTypeEnum.In);
        movements.ShouldContain(m => m.SourceId == Id(received));
        (await read.Set<StockBalance>().SingleAsync(b => b.VariantId == setup.VariantId)).Quantity.ShouldBe(20m);
        (await read.Set<ProductVariant>().SingleAsync(v => v.Id == setup.VariantId)).AverageCost.ShouldBe(5m);
    }

    [Fact]
    [Trait("Rule", "RN-CP04")]
    public async Task Should_Return400_When_ReceivingOrderNotPending()
    {
        var setup = await Arrange();
        var budget = await PendingOrder(setup, 1, 1m, send: false);
        (await setup.Client.PostAsJsonAsync($"{Route}/{Id(budget)}/receive", ReceiveBody(budget))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var pending = await PendingOrder(setup, 1, 1m);
        (await setup.Client.PostAsJsonAsync($"{Route}/{Id(pending)}/receive", ReceiveBody(pending, Version(pending) + 1)))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var received = await Receive(setup, pending);
        (await setup.Client.PostAsJsonAsync($"{Route}/{Id(pending)}/receive", ReceiveBody(received))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        await using var read = api.CreateContext();
        (await read.Set<StockMovement>().CountAsync(m => m.VariantId == setup.VariantId)).ShouldBe(1);
    }

    [Fact]
    [Trait("Rule", "RN-CP04")]
    public async Task Should_Return422_When_ReceiveItemsIncompleteOrWithLots()
    {
        var setup = await Arrange();
        var order = await PendingOrder(setup, 3, 2m);
        var itemId = Id(order.GetProperty("items")[0]);
        var url = $"{Route}/{Id(order)}/receive";

        await ShouldFailOn(await setup.Client.PostAsJsonAsync(url, new { version = Version(order), items = Array.Empty<object>() }), "items");
        await ShouldFailOn(await setup.Client.PostAsJsonAsync(url, new { version = Version(order), items = new[] { new { itemId }, new { itemId } } }), "items");
        await ShouldFailOn(await setup.Client.PostAsJsonAsync(url, new { version = Version(order), items = new[] { new { itemId = Guid.NewGuid() } } }), "items");
        await ShouldFailOn(await setup.Client.PostAsJsonAsync(url, new { items = new[] { new { itemId } } }), "version");
        await ShouldFailOn(await setup.Client.PostAsJsonAsync(url, new
        {
            version = Version(order),
            items = new[] { new { itemId, lots = new[] { new { code = "L1", expiresOn = "2027-01-01", quantity = 3m } } } },
        }), "items[0].lots");

        await using var read = api.CreateContext();
        (await read.Set<StockMovement>().CountAsync(m => m.VariantId == setup.VariantId)).ShouldBe(0);
        (await read.Set<PurchaseOrder>().SingleAsync(p => p.Id == Id(order))).Status.ShouldBe(PurchaseStatusEnum.Pending);
    }

    private static object LotsBody(JsonElement order, params (string Code, string ExpiresOn, decimal Quantity)[] lots) => new
    {
        version = Version(order),
        items = new[]
        {
            new
            {
                itemId = Id(order.GetProperty("items")[0]),
                lots = lots.Select(l => new { code = l.Code, expiresOn = l.ExpiresOn, quantity = l.Quantity }).ToArray(),
            },
        },
    };

    [Fact]
    [Trait("Rule", "RN-CE10")]
    public async Task Should_Return422_When_LotTrackedItemReceivedWithoutLotsOrWrongSum()
    {
        var setup = await Arrange(tracksLots: true);
        var order = await PendingOrder(setup, 10, 2m);
        var url = $"{Route}/{Id(order)}/receive";

        await ShouldFailOn(await setup.Client.PostAsJsonAsync(url, ReceiveBody(order)), "items[0].lots");
        await ShouldFailOn(await setup.Client.PostAsJsonAsync(url, LotsBody(order, ("A1", "2027-01-01", 4m))), "items[0].lots");
        await ShouldFailOn(await setup.Client.PostAsJsonAsync(url, LotsBody(order, ("A1", "2027-01-01", 4m), ("a1 ", "2027-01-01", 6m))), "items[0].lots");

        await using var read = api.CreateContext();
        (await read.Set<StockMovement>().CountAsync(m => m.VariantId == setup.VariantId)).ShouldBe(0);
        (await read.Set<Lot>().CountAsync(l => l.VariantId == setup.VariantId)).ShouldBe(0);
    }

    [Fact]
    [Trait("Rule", "RN-CE15")]
    public async Task Should_RecordLotsAndConsumeEarliestExpiryFirst_When_ReceivingLotTrackedItem()
    {
        var setup = await Arrange(tracksLots: true);
        var order = await PendingOrder(setup, 10, 2m);

        var response = await setup.Client.PostAsJsonAsync($"{Route}/{Id(order)}/receive",
            LotsBody(order, ("B-LATE", "2027-03-01", 6m), ("A-SOON", "2027-01-01", 4m)));
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var received = await Json(response);

        var item = received.GetProperty("items")[0];
        item.GetProperty("averageCostAfter").GetDecimal().ShouldBe(2m);
        item.GetProperty("lots").EnumerateArray().Select(l => (l.GetProperty("lotCode").GetString(), l.GetProperty("quantity").GetDecimal()))
            .ShouldBe(new (string?, decimal)[] { ("B-LATE", 6m), ("A-SOON", 4m) }, ignoreOrder: true);

        // Saída sem lote escolhido: o que vence primeiro sai antes (FEFO).
        var loss = await setup.Client.PostAsJsonAsync("/api/stock-movements/manual", new
        {
            variantId = setup.VariantId,
            locationId = setup.ShopId,
            type = "Out",
            reason = "Breakage",
            quantity = 5,
        });
        loss.StatusCode.ShouldBe(HttpStatusCode.NoContent, await loss.Content.ReadAsStringAsync());

        var lots = await Json(await setup.Client.GetAsync($"/api/products/{setup.ProductId}/lots?storeId={setup.StoreId}"));
        lots.EnumerateArray().Select(l => (l.GetProperty("code").GetString(), l.GetProperty("quantity").GetDecimal()))
            .ShouldBe(new (string?, decimal)[] { ("B-LATE", 5m) });

        await using var read = api.CreateContext();
        (await read.Set<StockBalance>().Where(b => b.VariantId == setup.VariantId).SumAsync(b => b.Quantity)).ShouldBe(5m);
        (await read.Set<StockMovement>().CountAsync(m => m.VariantId == setup.VariantId)).ShouldBe(4);
    }

    [Fact]
    [Trait("Rule", "RN-CE10")]
    public async Task Should_Return422_When_LotCodeExistsWithAnotherExpiry()
    {
        var setup = await Arrange(tracksLots: true);
        var first = await PendingOrder(setup, 2, 2m);
        (await setup.Client.PostAsJsonAsync($"{Route}/{Id(first)}/receive", LotsBody(first, ("L-9", "2027-01-01", 2m))))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var second = await PendingOrder(setup, 2, 2m);
        await ShouldFailOn(await setup.Client.PostAsJsonAsync($"{Route}/{Id(second)}/receive", LotsBody(second, ("L-9", "2027-02-01", 2m))),
            "items[0].lots[0].code");

        // Mesmo código e mesma validade: é o mesmo lote, a entrada soma no saldo dele.
        (await setup.Client.PostAsJsonAsync($"{Route}/{Id(second)}/receive", LotsBody(second, ("l-9", "2027-01-01", 2m))))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        await using var read = api.CreateContext();
        (await read.Set<Lot>().CountAsync(l => l.VariantId == setup.VariantId)).ShouldBe(1);
        (await read.Set<StockBalance>().SingleAsync(b => b.VariantId == setup.VariantId)).Quantity.ShouldBe(4m);
    }

    [Fact]
    [Trait("Rule", "RN-CP03")]
    public async Task Should_BlockEditAndCancel_When_OrderReceived()
    {
        var setup = await Arrange();
        var received = await Receive(setup, await PendingOrder(setup, 2, 3m));
        var url = $"{Route}/{Id(received)}";

        (await setup.Client.PutAsJsonAsync(url, new
        {
            supplierId = setup.SupplierId,
            receiveLocationId = setup.ShopId,
            version = Version(received),
            items = new[] { new { variantId = setup.VariantId, quantity = 5m, unitCost = 3m } },
        })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await setup.Client.PostAsJsonAsync($"{url}/cancel", new { version = Version(received), reason = "Desisti" }))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Rule", "RN-CP08")]
    public async Task Should_SaveLastCostAndRealLeadTime_When_Receiving()
    {
        var setup = await Arrange();
        var pending = await PendingOrder(setup, 4, 7.5m);

        // Recebido três dias de negócio depois do envio (RN-CP08: conta do envio, não da criação).
        var sentOn = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(pending.GetProperty("sentAt").GetDateTime().ToUniversalTime(),
            TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")));
        using var later = api.HostWithClock(sentOn.AddDays(3));
        var received = await Receive(setup, pending, await api.AdminAsync(later));
        received.GetProperty("realLeadTimeDays").GetInt32().ShouldBe(3);

        var page = await Json(await setup.Client.GetAsync($"{Route}?storeId={setup.StoreId}&limit=50"));
        var row = page.GetProperty("items").EnumerateArray().Single(r => Id(r) == Id(received));
        row.GetProperty("realLeadTimeDays").GetInt32().ShouldBe(3);

        await using (var read = api.CreateContext())
        {
            (await read.Set<ProductVariant>().SingleAsync(v => v.Id == setup.VariantId)).LastCost.ShouldBe(7.5m);
            var productId = await read.Set<ProductVariant>().Where(v => v.Id == setup.VariantId).Select(v => v.ProductId).SingleAsync();
            (await read.Set<ProductSupplier>().SingleAsync(s => s.ProductId == productId && s.SupplierId == setup.SupplierId)).LastCost.ShouldBe(7.5m);
        }

        var suggestion = await Json(await setup.Client.GetAsync(
            $"{Route}/item-suggestion?supplierId={setup.SupplierId}&variantId={setup.VariantId}&storeId={setup.StoreId}"));
        suggestion.GetProperty("lastCostFromSupplier").GetDecimal().ShouldBe(7.5m);

        // Produto sem vínculo com o fornecedor: recebe, mas nenhum vínculo é criado.
        var unlinked = await Arrange(linked: false);
        await Receive(unlinked, await PendingOrder(unlinked, 1, 2m));
        await using var check = api.CreateContext();
        var unlinkedProduct = await check.Set<ProductVariant>().Where(v => v.Id == unlinked.VariantId).Select(v => v.ProductId).SingleAsync();
        (await check.Set<ProductSupplier>().AnyAsync(s => s.ProductId == unlinkedProduct)).ShouldBeFalse();
    }
}
