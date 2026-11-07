using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Nextflow.Tests.Integration;

/// <summary>Pedido de compra (T-CP1, T-CP2, T-CP3, T-CP5) pela API, no Postgres real (F2.7). Recebimento é da F2.8.</summary>
[Collection(AccessApiCollection.Name)]
public class PurchaseOrdersApiTests(AccessApiFixture api)
{
    private const string Route = "/api/purchase-orders";
    private static int _sequence;

    private static string Unique(string prefix) => $"{prefix}-{Interlocked.Increment(ref _sequence)}-{Guid.NewGuid():N}"[..24];

    private static async Task<JsonElement> Json(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<JsonElement>();

    private static Guid Id(JsonElement element) => Guid.Parse(element.GetProperty("id").GetString()!);

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

    private static async Task<Guid> CreateSupplier(HttpClient client, int leadTimeDays = 10, string? name = null)
    {
        var response = await client.PostAsJsonAsync("/api/suppliers", new { name = name ?? Unique("Forn"), cnpj = NewCnpj(), leadTimeDays });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return Id(await Json(response));
    }

    private sealed record Product(Guid Id, string Name, Guid VariantId, string Sku);

    private static async Task<Product> CreateProduct(HttpClient client, object[]? suppliers = null, short decimalPlaces = 0)
    {
        var name = Unique("Bombom");
        var sku = Unique("BOM");
        var response = await client.PostAsJsonAsync("/api/products", new
        {
            name,
            productType = "Standard",
            saleUnit = decimalPlaces > 0 ? "Kilogram" : "Unit",
            decimalPlaces,
            variants = new[] { new { sku, price = 10m, isActive = true } },
            suppliers = suppliers ?? [],
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var productId = Id(await Json(response));
        var variantId = Id((await Json(await client.GetAsync($"/api/products/{productId}/variants")))[0]);
        return new(productId, name, variantId, sku);
    }

    private static async Task<(Guid StoreId, Guid ShopId)> CreateStore(HttpClient client)
    {
        var store = await Json(await client.PostAsJsonAsync("/api/stores", new { name = Unique("Loja") }));
        return (Id(store), Id(store.GetProperty("locations")[0]));
    }

    private static object Body(Guid supplierId, Guid locationId, object[] items, uint? version = null, bool confirmWarnings = false, string? note = null)
        => new { supplierId, receiveLocationId = locationId, note, version, confirmWarnings, items };

    private static object Item(Guid variantId, decimal quantity, decimal unitCost, Guid? id = null)
        => new { id, variantId, quantity, unitCost };

    private static async Task<JsonElement> CreateOrder(HttpClient client, object body)
    {
        var response = await client.PostAsJsonAsync(Route, body);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await Json(response);
    }

    private static async Task ShouldFailOn(HttpResponseMessage response, string field, HttpStatusCode status = HttpStatusCode.UnprocessableEntity)
    {
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(status, text);
        (await Json(response)).GetProperty("fieldErrors").TryGetProperty(field, out _).ShouldBeTrue(text);
    }

    private static uint Version(JsonElement order) => order.GetProperty("version").GetUInt32();

    [Fact]
    [Trait("Rule", "RN-CP07")]
    public async Task Should_CreateBudgetWithNumberAndTotals_When_SavingPurchaseOrder()
    {
        var client = await api.AdminAsync();
        var supplierId = await CreateSupplier(client);
        var first = await CreateProduct(client, [new { supplierId, isPreferred = true, supplierSku = "F-01" }]);
        var second = await CreateProduct(client);
        var (_, shop) = await CreateStore(client);

        var created = await CreateOrder(client, Body(supplierId, shop, [Item(first.VariantId, 10, 2.5m), Item(second.VariantId, 3, 4.1m)], note: "Páscoa"));
        var other = await CreateOrder(client, Body(supplierId, shop, [Item(first.VariantId, 1, 1m)]));

        created.GetProperty("status").GetString().ShouldBe("Budget");
        created.GetProperty("number").GetInt32().ShouldBeGreaterThan(0);
        other.GetProperty("number").GetInt32().ShouldBe(created.GetProperty("number").GetInt32() + 1);
        created.GetProperty("total").GetDecimal().ShouldBe(37.3m);
        created.GetProperty("note").GetString().ShouldBe("Páscoa");
        created.GetProperty("supplier").GetProperty("id").GetString().ShouldBe(supplierId.ToString());
        created.GetProperty("receiveLocation").GetProperty("id").GetString().ShouldBe(shop.ToString());
        created.GetProperty("createdByName").GetString().ShouldNotBeNullOrWhiteSpace();
        created.GetProperty("sentAt").ValueKind.ShouldBe(JsonValueKind.Null);
        var items = created.GetProperty("items").EnumerateArray().ToList();
        items.Count.ShouldBe(2);
        var line = items.Single(i => i.GetProperty("variantId").GetString() == first.VariantId.ToString());
        line.GetProperty("subtotal").GetDecimal().ShouldBe(25m);
        line.GetProperty("supplierSku").GetString().ShouldBe("F-01");
        line.GetProperty("sku").GetString().ShouldBe(first.Sku);
        line.GetProperty("productName").GetString().ShouldBe(first.Name);

        // PUT substitui os itens: o que ficou preserva o id, o que saiu some, o novo entra.
        var lineId = Guid.Parse(line.GetProperty("id").GetString()!);
        var updated = await client.PutAsJsonAsync($"{Route}/{Id(created)}",
            Body(supplierId, shop, [Item(first.VariantId, 4, 2.5m, lineId)], Version(created)));
        updated.StatusCode.ShouldBe(HttpStatusCode.OK, await updated.Content.ReadAsStringAsync());
        var body = await Json(updated);
        body.GetProperty("total").GetDecimal().ShouldBe(10m);
        var kept = body.GetProperty("items").EnumerateArray().Single();
        kept.GetProperty("id").GetString().ShouldBe(lineId.ToString());
        kept.GetProperty("quantity").GetDecimal().ShouldBe(4m);
    }

    [Fact]
    [Trait("Rule", "RN-CP01")]
    public async Task Should_Return422_When_PurchaseOrderHasNoItems()
    {
        var client = await api.AdminAsync();
        var supplierId = await CreateSupplier(client);
        var (_, shop) = await CreateStore(client);

        await ShouldFailOn(await client.PostAsJsonAsync(Route, Body(supplierId, shop, [])), "items");
    }

    [Fact]
    public async Task Should_Return422_When_SupplierOrVariantInvalid()
    {
        var client = await api.AdminAsync();
        var supplierId = await CreateSupplier(client);
        var inactiveSupplier = await CreateSupplier(client);
        (await client.DeleteAsync($"/api/suppliers/{inactiveSupplier}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var product = await CreateProduct(client);
        var weighed = await CreateProduct(client, decimalPlaces: 2);
        var (_, shop) = await CreateStore(client);

        await ShouldFailOn(await client.PostAsJsonAsync(Route, Body(inactiveSupplier, shop, [Item(product.VariantId, 1, 1)])), "supplierId");
        await ShouldFailOn(await client.PostAsJsonAsync(Route, Body(supplierId, Guid.NewGuid(), [Item(product.VariantId, 1, 1)])), "receiveLocationId");
        await ShouldFailOn(await client.PostAsJsonAsync(Route, Body(supplierId, shop, [Item(Guid.NewGuid(), 1, 1)])), "items[0].variantId");
        await ShouldFailOn(await client.PostAsJsonAsync(Route, Body(supplierId, shop, [Item(product.VariantId, 1, 1), Item(product.VariantId, 2, 1)])), "items[1].variantId");
        await ShouldFailOn(await client.PostAsJsonAsync(Route, Body(supplierId, shop, [Item(product.VariantId, 1.5m, 1)])), "items[0].quantity");
        await ShouldFailOn(await client.PostAsJsonAsync(Route, Body(supplierId, shop, [Item(weighed.VariantId, 1.255m, 1)])), "items[0].quantity");
        await ShouldFailOn(await client.PostAsJsonAsync(Route, Body(supplierId, shop, [Item(product.VariantId, 0, 1)])), "items[0].quantity");
        await ShouldFailOn(await client.PostAsJsonAsync(Route, Body(supplierId, shop, [Item(product.VariantId, 1, -1)])), "items[0].unitCost");

        // Variação inativa bloqueia; produto pesado aceita as casas dele.
        (await client.DeleteAsync($"/api/products/{product.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await ShouldFailOn(await client.PostAsJsonAsync(Route, Body(supplierId, shop, [Item(product.VariantId, 1, 1)])), "items[0].variantId");
        await CreateOrder(client, Body(supplierId, shop, [Item(weighed.VariantId, 1.25m, 30)]));
    }

    [Fact]
    [Trait("Rule", "RN-CE30")]
    public async Task Should_AskConfirmation_When_BelowMinimumOrNotMultiple()
    {
        var client = await api.AdminAsync();
        var supplierId = await CreateSupplier(client);
        var product = await CreateProduct(client, [new { supplierId, isPreferred = true, minOrderQty = 24, orderMultiple = 12 }]);
        var (_, shop) = await CreateStore(client);

        foreach (var quantity in new[] { 12m, 30m })
        {
            var response = await client.PostAsJsonAsync(Route, Body(supplierId, shop, [Item(product.VariantId, quantity, 1)]));
            await ShouldFailOn(response, "confirm");
        }

        await CreateOrder(client, Body(supplierId, shop, [Item(product.VariantId, 36, 1)]));
        var confirmed = await CreateOrder(client, Body(supplierId, shop, [Item(product.VariantId, 30, 1)], confirmWarnings: true));
        confirmed.GetProperty("items")[0].GetProperty("quantity").GetDecimal().ShouldBe(30m);
    }

    [Fact]
    [Trait("Rule", "RN-CE19")]
    public async Task Should_AskConfirmation_When_ProductDiscontinued()
    {
        var client = await api.AdminAsync();
        var supplierId = await CreateSupplier(client);
        var product = await CreateProduct(client);
        var (_, shop) = await CreateStore(client);
        (await client.PatchAsJsonAsync($"/api/products/{product.Id}/lifecycle", new { lifecycle = "Discontinued" })).IsSuccessStatusCode.ShouldBeTrue();

        await ShouldFailOn(await client.PostAsJsonAsync(Route, Body(supplierId, shop, [Item(product.VariantId, 1, 1)])), "confirm");
        await CreateOrder(client, Body(supplierId, shop, [Item(product.VariantId, 1, 1)], confirmWarnings: true));
    }

    [Fact]
    [Trait("Rule", "RN-CP08")]
    public async Task Should_MoveToPendingWithExpectedOn_When_SendingToSupplier()
    {
        var client = await api.AdminAsync();
        var supplierId = await CreateSupplier(client, leadTimeDays: 10);
        var fast = await CreateProduct(client, [new { supplierId, isPreferred = true, leadTimeDays = 3 }]);
        var slow = await CreateProduct(client, [new { supplierId, isPreferred = true, leadTimeDays = 15 }]);
        var (_, shop) = await CreateStore(client);
        var order = await CreateOrder(client, Body(supplierId, shop, [Item(fast.VariantId, 1, 1), Item(slow.VariantId, 1, 1)]));

        var response = await client.PostAsJsonAsync($"{Route}/{Id(order)}/send", new { version = Version(order) });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var sent = await Json(response);

        sent.GetProperty("status").GetString().ShouldBe("Pending");
        sent.GetProperty("sentAt").ValueKind.ShouldBe(JsonValueKind.String);
        var sentOn = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(sent.GetProperty("sentAt").GetDateTime().ToUniversalTime(),
            TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")));
        DateOnly.Parse(sent.GetProperty("expectedOn").GetString()!).ShouldBe(sentOn.AddDays(15));

        // Enviar de novo: já não é orçamento.
        var again = await client.PostAsJsonAsync($"{Route}/{Id(order)}/send", new { version = Version(sent) });
        again.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // Pendente ainda edita (RN-CP03) e continua Pendente.
        var form = await Json(await client.GetAsync($"{Route}/{Id(order)}/form"));
        form.GetProperty("leadTimeDays").GetInt32().ShouldBe(15);
        var edited = await client.PutAsJsonAsync($"{Route}/{Id(order)}", Body(supplierId, shop, [Item(fast.VariantId, 2, 1)], Version(sent)));
        edited.StatusCode.ShouldBe(HttpStatusCode.OK, await edited.Content.ReadAsStringAsync());
        var editedBody = await Json(edited);
        editedBody.GetProperty("status").GetString().ShouldBe("Pending");
        DateOnly.Parse(editedBody.GetProperty("expectedOn").GetString()!).ShouldBe(sentOn.AddDays(3));
    }

    [Fact]
    public async Task Should_Return409_When_VersionIsStale()
    {
        var client = await api.AdminAsync();
        var supplierId = await CreateSupplier(client);
        var product = await CreateProduct(client);
        var (_, shop) = await CreateStore(client);
        var order = await CreateOrder(client, Body(supplierId, shop, [Item(product.VariantId, 1, 1)]));
        var stale = Version(order);

        (await client.PutAsJsonAsync($"{Route}/{Id(order)}", Body(supplierId, shop, [Item(product.VariantId, 2, 1)], stale))).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await client.PutAsJsonAsync($"{Route}/{Id(order)}", Body(supplierId, shop, [Item(product.VariantId, 3, 1)], stale))).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await client.PostAsJsonAsync($"{Route}/{Id(order)}/send", new { version = stale })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await client.PostAsJsonAsync($"{Route}/{Id(order)}/cancel", new { version = stale, reason = "Erro" })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("Rule", "RN-CP03")]
    public async Task Should_CancelAndBlockEdit_When_CancelingPurchaseOrder()
    {
        var client = await api.AdminAsync();
        var supplierId = await CreateSupplier(client);
        var product = await CreateProduct(client);
        var (_, shop) = await CreateStore(client);
        var order = await CreateOrder(client, Body(supplierId, shop, [Item(product.VariantId, 1, 1)]));

        await ShouldFailOn(await client.PostAsJsonAsync($"{Route}/{Id(order)}/cancel", new { version = Version(order), reason = " " }), "reason");

        var response = await client.PostAsJsonAsync($"{Route}/{Id(order)}/cancel", new { version = Version(order), reason = "Fornecedor sem estoque" });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var canceled = await Json(response);
        canceled.GetProperty("status").GetString().ShouldBe("Canceled");
        canceled.GetProperty("cancelReason").GetString().ShouldBe("Fornecedor sem estoque");
        canceled.GetProperty("canceledAt").ValueKind.ShouldBe(JsonValueKind.String);

        (await client.PutAsJsonAsync($"{Route}/{Id(order)}", Body(supplierId, shop, [Item(product.VariantId, 2, 1)], Version(canceled)))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync($"{Route}/{Id(order)}/send", new { version = Version(canceled) })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync($"{Route}/{Id(order)}/cancel", new { version = Version(canceled), reason = "De novo" })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Rule", "RN-CP05")]
    public async Task Should_ReturnSuggestion_When_ChoosingItem()
    {
        var client = await api.AdminAsync();
        var supplierId = await CreateSupplier(client);
        var product = await CreateProduct(client, [new { supplierId, isPreferred = true, supplierSku = "CX-9", minOrderQty = 6, orderMultiple = 6 }]);
        var (storeId, shop) = await CreateStore(client);
        var (_, otherShop) = await CreateStore(client);
        var opening = await client.PostAsJsonAsync("/api/stock-movements/manual", new
        {
            variantId = product.VariantId,
            locationId = shop,
            type = "In",
            reason = "Opening",
            quantity = 8m,
            unitCost = 2m,
        });
        opening.StatusCode.ShouldBe(HttpStatusCode.NoContent, await opening.Content.ReadAsStringAsync());
        // A caminho: orçamento e pendente da loja contam; cancelado e outra loja não.
        await CreateOrder(client, Body(supplierId, shop, [Item(product.VariantId, 12, 1)]));
        var pending = await CreateOrder(client, Body(supplierId, shop, [Item(product.VariantId, 6, 1)]));
        (await client.PostAsJsonAsync($"{Route}/{Id(pending)}/send", new { version = Version(pending) })).StatusCode.ShouldBe(HttpStatusCode.OK);
        var canceled = await CreateOrder(client, Body(supplierId, shop, [Item(product.VariantId, 60, 1)]));
        (await client.PostAsJsonAsync($"{Route}/{Id(canceled)}/cancel", new { version = Version(canceled), reason = "x" })).StatusCode.ShouldBe(HttpStatusCode.OK);
        await CreateOrder(client, Body(supplierId, otherShop, [Item(product.VariantId, 600, 1)]));

        var response = await client.GetAsync($"{Route}/item-suggestion?supplierId={supplierId}&variantId={product.VariantId}&storeId={storeId}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var suggestion = await Json(response);

        suggestion.GetProperty("productId").GetString().ShouldBe(product.Id.ToString());
        suggestion.GetProperty("sku").GetString().ShouldBe(product.Sku);
        suggestion.GetProperty("supplierSku").GetString().ShouldBe("CX-9");
        suggestion.GetProperty("minOrderQty").GetDecimal().ShouldBe(6m);
        suggestion.GetProperty("orderMultiple").GetDecimal().ShouldBe(6m);
        suggestion.GetProperty("averageCost").GetDecimal().ShouldBe(2m);
        suggestion.GetProperty("lastCostFromSupplier").ValueKind.ShouldBe(JsonValueKind.Null);
        suggestion.GetProperty("availableAtStore").GetDecimal().ShouldBe(8m);
        suggestion.GetProperty("incomingQuantity").GetDecimal().ShouldBe(18m);
        suggestion.GetProperty("saleUnit").GetString().ShouldBe("Unit");
        suggestion.GetProperty("lifecycle").GetString().ShouldBe("Active");

        // A busca de itens marca o que é do fornecedor e, com onlyLinked, mostra só eles.
        var unlinked = await CreateProduct(client);
        var linkedOnly = await Json(await client.GetAsync($"{Route}/variant-options?supplierId={supplierId}&onlyLinked=true&search={Uri.EscapeDataString(product.Name)}"));
        linkedOnly.EnumerateArray().Single().GetProperty("isLinked").GetBoolean().ShouldBeTrue();
        (await Json(await client.GetAsync($"{Route}/variant-options?supplierId={supplierId}&onlyLinked=true&search={Uri.EscapeDataString(unlinked.Name)}"))).GetArrayLength().ShouldBe(0);
        var all = await Json(await client.GetAsync($"{Route}/variant-options?supplierId={supplierId}&onlyLinked=false&search={Uri.EscapeDataString(unlinked.Name)}"));
        all.EnumerateArray().Single().GetProperty("isLinked").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Should_FilterByStoreAndFlagLate_When_ListingPurchaseOrders()
    {
        var client = await api.AdminAsync();
        var supplierName = Unique("Forn");
        var supplierId = await CreateSupplier(client, leadTimeDays: 5, name: supplierName);
        var product = await CreateProduct(client);
        var (storeId, shop) = await CreateStore(client);
        var (_, otherShop) = await CreateStore(client);
        var budget = await CreateOrder(client, Body(supplierId, shop, [Item(product.VariantId, 2, 3)]));
        var pending = await CreateOrder(client, Body(supplierId, shop, [Item(product.VariantId, 1, 1)]));
        (await client.PostAsJsonAsync($"{Route}/{Id(pending)}/send", new { version = Version(pending) })).StatusCode.ShouldBe(HttpStatusCode.OK);
        await CreateOrder(client, Body(supplierId, otherShop, [Item(product.VariantId, 1, 1)]));

        var filters = Uri.EscapeDataString(JsonSerializer.Serialize(new { search = supplierName }));
        var page = await Json(await client.GetAsync($"{Route}?storeId={storeId}&limit=50&filters={filters}&sort=number&order=asc"));
        page.GetProperty("total").GetInt32().ShouldBe(2);
        var rows = page.GetProperty("items").EnumerateArray().ToList();
        rows[0].GetProperty("id").GetString().ShouldBe(Id(budget).ToString());
        rows[0].GetProperty("itemCount").GetInt32().ShouldBe(1);
        rows[0].GetProperty("total").GetDecimal().ShouldBe(6m);
        rows[0].GetProperty("supplierName").GetString().ShouldBe(supplierName);
        rows[0].GetProperty("storeName").GetString().ShouldNotBeNullOrWhiteSpace();
        rows[0].GetProperty("fromForecast").GetBoolean().ShouldBeFalse();
        rows.ShouldAllBe(r => !r.GetProperty("isLate").GetBoolean());

        var onlyPending = Uri.EscapeDataString(JsonSerializer.Serialize(new { search = supplierName, status = "Pending" }));
        (await Json(await client.GetAsync($"{Route}?storeId={storeId}&filters={onlyPending}"))).GetProperty("total").GetInt32().ShouldBe(1);

        // Dez dias depois o pendente passou da previsão (5 dias); o orçamento nunca atrasa.
        using var later = api.HostWithClock(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10));
        var laterClient = await api.AdminAsync(later);
        var late = Uri.EscapeDataString(JsonSerializer.Serialize(new { search = supplierName, onlyLate = true }));
        var latePage = await Json(await laterClient.GetAsync($"{Route}?storeId={storeId}&filters={late}"));
        latePage.GetProperty("total").GetInt32().ShouldBe(1);
        var lateRow = latePage.GetProperty("items")[0];
        lateRow.GetProperty("id").GetString().ShouldBe(Id(pending).ToString());
        lateRow.GetProperty("isLate").GetBoolean().ShouldBeTrue();
    }
}
