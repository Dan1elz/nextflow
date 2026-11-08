using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Nextflow.Tests.Integration;

/// <summary>Venda rápida de balcão (T-VD2) pela API, no Postgres real, com baixa pelo livro-razão (F2.4).</summary>
[Collection(AccessApiCollection.Name)]
public class QuickSaleApiTests(AccessApiFixture api)
{
    private const string Route = "/api/sales/quick";
    private static readonly DateOnly Today = new(2027, 4, 12);
    private static int _sequence;

    private static string Unique(string prefix) => $"{prefix}-{Interlocked.Increment(ref _sequence)}-{Guid.NewGuid():N}"[..24];

    private static async Task<JsonElement> Json(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<JsonElement>();

    private static string Filters(object filters) => Uri.EscapeDataString(JsonSerializer.Serialize(filters));

    private static IEnumerable<string> FieldErrors(JsonElement body)
        => body.GetProperty("fieldErrors").EnumerateObject().Select(field => field.Name);

    private sealed record Product(Guid Id, Guid VariantId);

    private static async Task<Product> CreateProduct(HttpClient client, decimal price = 10m, bool allowsNegative = false)
    {
        var response = await client.PostAsJsonAsync("/api/products", new
        {
            name = Unique("Pirulito"),
            productType = "Standard",
            saleUnit = "Unit",
            decimalPlaces = 0,
            allowsNegative,
            variants = new[] { new { sku = Unique("PIR"), price, isActive = true } },
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var productId = (await Json(response)).GetProperty("id").GetGuid();
        var variants = await Json(await client.GetAsync($"/api/products/{productId}/variants"));
        return new(productId, variants[0].GetProperty("id").GetGuid());
    }

    private static async Task<(Guid StoreId, Guid ShopId)> CreateStore(HttpClient client)
    {
        var store = await Json(await client.PostAsJsonAsync("/api/stores", new { name = Unique("Loja") }));
        return (store.GetProperty("id").GetGuid(), store.GetProperty("locations")[0].GetProperty("id").GetGuid());
    }

    private static async Task Opening(HttpClient client, Guid variantId, Guid locationId, decimal quantity, decimal unitCost = 4m)
    {
        var response = await client.PostAsJsonAsync("/api/stock-movements/manual", new
        {
            variantId,
            locationId,
            type = "In",
            reason = "Opening",
            quantity,
            unitCost,
        });
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
    }

    private static async Task<decimal> Available(HttpClient client, Guid storeId, Guid variantId)
    {
        var options = await Json(await client.GetAsync($"/api/sales/variant-options?search=&storeId={storeId}&limit=50"));
        return options.EnumerateArray().Where(o => o.GetProperty("variantId").GetGuid() == variantId)
            .Select(o => o.GetProperty("availableQuantity").GetDecimal()).Single();
    }

    private static object Item(Guid variantId, decimal quantity, decimal expectedUnitPrice, decimal discount = 0m, Guid? lotId = null)
        => new { variantId, quantity, discount, expectedUnitPrice, lotId };

    private static object Pay(string method, decimal amount) => new { method, amount };

    private static Task<HttpResponseMessage> Sell(HttpClient client, Guid storeId, object[] items, object[] payments,
        decimal? cashReceived = null, bool confirmWarnings = false, Guid? clientId = null)
        => client.PostAsJsonAsync(Route, new { storeId, clientId, items, payments, cashReceived, confirmWarnings });

    private static async Task<JsonElement> Sold(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await Json(response);
    }

    [Fact]
    [Trait("Rule", "RN-VD15")]
    public async Task Should_CreatePaidOrderAndPostSaleMovements_When_QuickSaleIsValid()
    {
        var client = await api.AdminAsync();
        var (storeId, shopId) = await CreateStore(client);
        var first = await CreateProduct(client, price: 10m);
        var second = await CreateProduct(client, price: 2.5m);
        await Opening(client, first.VariantId, shopId, 10m);
        await Opening(client, second.VariantId, shopId, 10m);

        var order = await Sold(await Sell(client, storeId,
            [Item(first.VariantId, 2m, 10m, discount: 1m), Item(second.VariantId, 3m, 2.5m)],
            [Pay("Pix", 20m), Pay("DebitCard", 6.5m)]));

        order.GetProperty("status").GetString().ShouldBe("Paid");
        order.GetProperty("channel").GetString().ShouldBe("Counter");
        order.GetProperty("number").GetInt32().ShouldBeGreaterThan(0);
        order.GetProperty("saleLocation").GetProperty("id").GetGuid().ShouldBe(shopId);
        order.GetProperty("subtotal").GetDecimal().ShouldBe(27.5m);
        order.GetProperty("discountTotal").GetDecimal().ShouldBe(1m);
        order.GetProperty("total").GetDecimal().ShouldBe(26.5m);
        order.GetProperty("paidAt").ValueKind.ShouldNotBe(JsonValueKind.Null);
        order.GetProperty("items").GetArrayLength().ShouldBe(2);
        order.GetProperty("items")[0].GetProperty("lineTotal").GetDecimal().ShouldBe(19m);
        order.GetProperty("items")[0].GetProperty("unitCost").GetDecimal().ShouldBe(4m);
        order.GetProperty("payments").GetArrayLength().ShouldBe(2);

        (await Available(client, storeId, first.VariantId)).ShouldBe(8m);
        (await Available(client, storeId, second.VariantId)).ShouldBe(7m);

        var orderId = order.GetProperty("id").GetGuid();
        var kardex = await Json(await client.GetAsync(
            $"/api/stock-movements?limit=50&filters={Filters(new { variantId = first.VariantId.ToString(), reason = "Sale" })}"));
        var sale = kardex.GetProperty("items").EnumerateArray().Single();
        sale.GetProperty("quantity").GetDecimal().ShouldBe(-2m);
        sale.GetProperty("source").GetProperty("type").GetString().ShouldBe("Order");
        sale.GetProperty("source").GetProperty("id").GetGuid().ShouldBe(orderId);
    }

    [Fact]
    [Trait("Rule", "RN-VD02")]
    public async Task Should_ChargeEffectivePriceWithPromotion_When_QuickSale()
    {
        await using var host = api.HostWithClock(Today);
        var client = await api.AdminAsync(host);
        var (storeId, shopId) = await CreateStore(client);
        var product = await CreateProduct(client, price: 10m);
        await Opening(client, product.VariantId, shopId, 5m, unitCost: 1m);
        var promotion = await client.PostAsJsonAsync("/api/promotions", new
        {
            productId = product.Id,
            discountPercent = 20m,
            startDate = Today.ToString("yyyy-MM-dd"),
            endDate = Today.AddDays(3).ToString("yyyy-MM-dd"),
        });
        promotion.StatusCode.ShouldBe(HttpStatusCode.Created, await promotion.Content.ReadAsStringAsync());
        var promotionId = (await Json(promotion)).GetProperty("id").GetGuid();

        var order = await Sold(await Sell(client, storeId, [Item(product.VariantId, 2m, 8m)], [Pay("Cash", 16m)]));

        var item = order.GetProperty("items")[0];
        item.GetProperty("listPrice").GetDecimal().ShouldBe(10m);
        item.GetProperty("unitPrice").GetDecimal().ShouldBe(8m);
        item.GetProperty("promotion").GetProperty("id").GetGuid().ShouldBe(promotionId);
        order.GetProperty("total").GetDecimal().ShouldBe(16m);
    }

    [Fact]
    [Trait("Rule", "RN-VD02")]
    public async Task Should_AskConfirmation_When_PriceChangedSinceSearch()
    {
        var client = await api.AdminAsync();
        var (storeId, shopId) = await CreateStore(client);
        var product = await CreateProduct(client, price: 12m);
        await Opening(client, product.VariantId, shopId, 5m);

        var changed = await Sell(client, storeId, [Item(product.VariantId, 1m, 10m)], [Pay("Pix", 12m)]);
        changed.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        FieldErrors(await Json(changed)).ShouldContain("confirm");
        (await Available(client, storeId, product.VariantId)).ShouldBe(5m);

        var confirmed = await Sold(await Sell(client, storeId, [Item(product.VariantId, 1m, 10m)], [Pay("Pix", 12m)], confirmWarnings: true));
        confirmed.GetProperty("total").GetDecimal().ShouldBe(12m);
    }

    [Fact]
    [Trait("Rule", "RN-VD05")]
    public async Task Should_Return422_When_PaymentsDoNotMatchTotal()
    {
        var client = await api.AdminAsync();
        var (storeId, shopId) = await CreateStore(client);
        var product = await CreateProduct(client, price: 10m);
        await Opening(client, product.VariantId, shopId, 5m);

        var response = await Sell(client, storeId, [Item(product.VariantId, 1m, 10m)], [Pay("Pix", 9.99m)]);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        FieldErrors(await Json(response)).ShouldContain("payments");
        (await Available(client, storeId, product.VariantId)).ShouldBe(5m);
    }

    [Fact]
    [Trait("Rule", "RN-VD03")]
    public async Task Should_Return422_When_DiscountExceedsLineTotal()
    {
        var client = await api.AdminAsync();
        var (storeId, shopId) = await CreateStore(client);
        var product = await CreateProduct(client, price: 10m);
        await Opening(client, product.VariantId, shopId, 5m);

        var response = await Sell(client, storeId, [Item(product.VariantId, 2m, 10m, discount: 20.01m)], [Pay("Pix", 1m)]);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        FieldErrors(await Json(response)).ShouldContain("items[0].discount");
    }

    [Fact]
    [Trait("Rule", "RN-VD01")]
    public async Task Should_Return422_When_QuickSaleHasNoItems()
    {
        var client = await api.AdminAsync();
        var (storeId, shopId) = await CreateStore(client);
        var product = await CreateProduct(client);
        await Opening(client, product.VariantId, shopId, 5m);

        var empty = await Sell(client, storeId, [], [Pay("Pix", 10m)]);
        empty.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        FieldErrors(await Json(empty)).ShouldContain("items");

        var withLot = await Sell(client, storeId, [Item(product.VariantId, 1m, 10m, lotId: Guid.NewGuid())], [Pay("Pix", 10m)]);
        withLot.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        FieldErrors(await Json(withLot)).ShouldContain("items[0].lotId");
    }

    [Fact]
    [Trait("Rule", "RN-CE32")]
    public async Task Should_Return422AndKeepStock_When_StockInsufficient()
    {
        var client = await api.AdminAsync();
        var (storeId, shopId) = await CreateStore(client);
        var stocked = await CreateProduct(client, price: 5m);
        var scarce = await CreateProduct(client, price: 5m);
        await Opening(client, stocked.VariantId, shopId, 5m);
        await Opening(client, scarce.VariantId, shopId, 1m);

        var response = await Sell(client, storeId,
            [Item(stocked.VariantId, 1m, 5m), Item(scarce.VariantId, 2m, 5m)], [Pay("Pix", 15m)]);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        FieldErrors(await Json(response)).ShouldContain("items[1].quantity");
        (await Available(client, storeId, stocked.VariantId)).ShouldBe(5m);
        (await Available(client, storeId, scarce.VariantId)).ShouldBe(1m);
    }

    [Fact]
    [Trait("Rule", "RN-CE32")]
    public async Task Should_SellBelowZero_When_ProductAllowsNegative()
    {
        var client = await api.AdminAsync();
        var (storeId, _) = await CreateStore(client);
        var product = await CreateProduct(client, price: 3m, allowsNegative: true);

        await Sold(await Sell(client, storeId, [Item(product.VariantId, 2m, 3m)], [Pay("Cash", 6m)]));

        (await Available(client, storeId, product.VariantId)).ShouldBe(-2m);
    }

    [Fact]
    [Trait("Rule", "RN-VD02")]
    public async Task Should_ReturnTotalsWithoutSaving_When_PreviewingQuickSale()
    {
        var client = await api.AdminAsync();
        var (storeId, shopId) = await CreateStore(client);
        var product = await CreateProduct(client, price: 4.5m);
        await Opening(client, product.VariantId, shopId, 5m);

        var response = await client.PostAsJsonAsync($"{Route}/preview", new
        {
            storeId,
            items = new[] { Item(product.VariantId, 3m, 4.5m, discount: 0.5m) },
            payments = new[] { Pay("Cash", 10m) },
            cashReceived = 20m,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var preview = await Json(response);
        preview.GetProperty("items")[0].GetProperty("unitPrice").GetDecimal().ShouldBe(4.5m);
        preview.GetProperty("subtotal").GetDecimal().ShouldBe(13.5m);
        preview.GetProperty("total").GetDecimal().ShouldBe(13m);
        preview.GetProperty("remaining").GetDecimal().ShouldBe(3m);
        preview.GetProperty("change").GetDecimal().ShouldBe(10m);
        (await Available(client, storeId, product.VariantId)).ShouldBe(5m);

        var invalid = await client.PostAsJsonAsync($"{Route}/preview", new
        {
            storeId,
            items = new[] { Item(product.VariantId, 0m, 4.5m), Item(product.VariantId, 1m, 4.5m, lotId: Guid.NewGuid()) },
            payments = Array.Empty<object>(),
        });
        invalid.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var fields = FieldErrors(await Json(invalid)).ToList();
        fields.ShouldContain("items[0].quantity");
        fields.ShouldContain("items[1].lotId");
        fields.ShouldNotContain("payments");
    }

    [Fact]
    [Trait("Rule", "RN-VD05")]
    public async Task Should_RecordChange_When_CashReceivedExceedsCashPayment()
    {
        var client = await api.AdminAsync();
        var (storeId, shopId) = await CreateStore(client);
        var product = await CreateProduct(client, price: 7.5m);
        await Opening(client, product.VariantId, shopId, 5m);

        var order = await Sold(await Sell(client, storeId, [Item(product.VariantId, 2m, 7.5m)],
            [Pay("Cash", 10m), Pay("Pix", 5m)], cashReceived: 20m));
        order.GetProperty("cashReceived").GetDecimal().ShouldBe(20m);
        order.GetProperty("change").GetDecimal().ShouldBe(10m);

        var withoutCash = await Sell(client, storeId, [Item(product.VariantId, 1m, 7.5m)], [Pay("Pix", 7.5m)], cashReceived: 10m);
        withoutCash.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        FieldErrors(await Json(withoutCash)).ShouldContain("cashReceived");

        var short_ = await Sell(client, storeId, [Item(product.VariantId, 1m, 7.5m)], [Pay("Cash", 7.5m)], cashReceived: 5m);
        short_.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        FieldErrors(await Json(short_)).ShouldContain("cashReceived");
    }

    [Fact]
    public async Task Should_FilterSalesHistory_When_FilteringBySellerProductAndTotal()
    {
        var client = await api.AdminAsync();
        var (storeId, shopId) = await CreateStore(client);
        var candy = await CreateProduct(client, price: 10m);
        var gum = await CreateProduct(client, price: 2m);
        await Opening(client, candy.VariantId, shopId, 10m);
        await Opening(client, gum.VariantId, shopId, 10m);

        var big = await Sold(await Sell(client, storeId, [Item(candy.VariantId, 3m, 10m)], [Pay("Pix", 30m)]));
        var small = await Sold(await Sell(client, storeId, [Item(gum.VariantId, 2m, 2m)], [Pay("Cash", 4m)]));
        var mixed = await Sold(await Sell(client, storeId,
            [Item(candy.VariantId, 1m, 10m), Item(gum.VariantId, 1m, 2m)], [Pay("Pix", 12m)]));
        var sellerId = big.GetProperty("user").GetProperty("id").GetGuid();

        async Task<Guid[]> List(object filters)
        {
            var page = await Json(await client.GetAsync($"/api/orders?storeId={storeId}&limit=50&filters={Filters(filters)}"));
            return [.. page.GetProperty("items").EnumerateArray().Select(o => o.GetProperty("id").GetGuid())];
        }

        Guid Id(JsonElement order) => order.GetProperty("id").GetGuid();

        (await List(new { productId = candy.Id.ToString() })).ShouldBe([Id(mixed), Id(big)], ignoreOrder: true);
        (await List(new { productId = gum.Id.ToString() })).ShouldBe([Id(mixed), Id(small)], ignoreOrder: true);
        (await List(new { minTotal = "10" })).ShouldBe([Id(mixed), Id(big)], ignoreOrder: true);
        (await List(new { maxTotal = "12" })).ShouldBe([Id(mixed), Id(small)], ignoreOrder: true);
        (await List(new { minTotal = "5", maxTotal = "20" })).ShouldBe([Id(mixed)]);
        (await List(new { userId = sellerId.ToString() })).Length.ShouldBe(3);
        (await List(new { userId = Guid.NewGuid().ToString() })).ShouldBeEmpty();
        (await List(new { productId = candy.Id.ToString(), paymentMethod = "Pix", maxTotal = "15" })).ShouldBe([Id(mixed)]);
    }
}
