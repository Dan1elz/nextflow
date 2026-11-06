using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Nextflow.Tests.Integration;

/// <summary>
/// Kardex (T8), mínimos por variação × local (RN-CE33) e política ABC (T13, RN-CE29), pela API, no Postgres real (F1.8).
/// </summary>
[Collection(AccessApiCollection.Name)]
public class StockKardexApiTests(AccessApiFixture api)
{
    private const string MovementsRoute = "/api/stock-movements";
    private static int _sequence;

    private static string Unique(string prefix) => $"{prefix}-{Interlocked.Increment(ref _sequence)}-{Guid.NewGuid():N}"[..24];

    private static async Task<JsonElement> Json(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<JsonElement>();

    private static string Id(JsonElement element) => element.GetProperty("id").GetString()!;

    private static string Filters(object filters) => Uri.EscapeDataString(JsonSerializer.Serialize(filters));

    private sealed record Product(Guid Id, string Name, Guid VariantId);

    private static async Task<Product> CreateProduct(HttpClient client, decimal defaultMinQuantity = 0, object[]? locationMinimums = null)
    {
        var name = Unique("Bombom");
        var response = await client.PostAsJsonAsync("/api/products", new
        {
            name,
            productType = "Standard",
            saleUnit = "Unit",
            decimalPlaces = 0,
            defaultMinQuantity,
            locationMinimums = locationMinimums ?? [],
            variants = new[] { new { sku = Unique("BOM"), price = 10m, isActive = true } },
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var productId = Id(await Json(response));
        var variantId = Guid.Parse(Id((await Json(await client.GetAsync($"/api/products/{productId}/variants")))[0]));
        return new(Guid.Parse(productId), name, variantId);
    }

    private static async Task<(Guid StoreId, Guid FirstLocationId, Guid SecondLocationId)> CreateStore(HttpClient client)
    {
        var store = await Json(await client.PostAsJsonAsync("/api/stores", new { name = Unique("Loja") }));
        var storeId = Guid.Parse(Id(store));
        var deposit = await Json(await client.PostAsJsonAsync("/api/stock-locations", new { storeId, name = Unique("Depósito"), isSellable = false }));
        return (storeId, Guid.Parse(Id(store.GetProperty("locations")[0])), Guid.Parse(Id(deposit)));
    }

    private static async Task Post(HttpClient client, Guid variantId, Guid locationId, string type, string reason, decimal quantity, decimal? unitCost = null)
    {
        var response = await client.PostAsJsonAsync($"{MovementsRoute}/manual", new
        {
            variantId,
            locationId,
            type,
            reason,
            quantity,
            unitCost,
            note = reason == "Adjustment" ? "Contagem" : null,
        });
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
    }

    private static async Task<JsonElement> Kardex(HttpClient client, object filters)
    {
        var response = await client.GetAsync($"{MovementsRoute}?limit=50&filters={Filters(filters)}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await Json(response);
    }

    // ---------- Kardex ----------

    [Fact]
    [Trait("Rule", "RN-CE01")]
    public async Task Should_ComputeBalanceAfter_When_ListingMovements()
    {
        var client = await api.AdminAsync();
        var product = await CreateProduct(client);
        var (_, location, _) = await CreateStore(client);

        await Post(client, product.VariantId, location, "In", "Opening", 10, unitCost: 2m);
        await Post(client, product.VariantId, location, "Out", "Breakage", 3);
        await Post(client, product.VariantId, location, "In", "Adjustment", 5);

        var page = await Kardex(client, new { variantId = product.VariantId });
        page.GetProperty("total").GetInt32().ShouldBe(3);

        // Mais recente primeiro; o saldo após é o da variação naquele local, depois de cada lançamento.
        var items = page.GetProperty("items").EnumerateArray().ToList();
        items.Select(i => i.GetProperty("quantity").GetDecimal()).ShouldBe([5m, -3m, 10m]);
        items.Select(i => i.GetProperty("balanceAfter").GetDecimal()).ShouldBe([12m, 7m, 10m]);
        items.Select(i => i.GetProperty("reason").GetString()).ShouldBe(["Adjustment", "Breakage", "Opening"]);
        items[0].GetProperty("type").GetString().ShouldBe("In");
        items[1].GetProperty("type").GetString().ShouldBe("Out");
        items[2].GetProperty("unitCost").GetDecimal().ShouldBe(2m);
        items[0].GetProperty("productName").GetString().ShouldBe(product.Name);
        items[0].GetProperty("locationId").GetString().ShouldBe(location.ToString());
        items[0].GetProperty("userName").GetString().ShouldNotBeNullOrWhiteSpace();
        items[0].GetProperty("note").GetString().ShouldBe("Contagem");
    }

    [Fact]
    public async Task Should_FilterByLocationAndVariant_When_ListingMovements()
    {
        var client = await api.AdminAsync();
        var first = await CreateProduct(client);
        var second = await CreateProduct(client);
        var (storeId, shop, deposit) = await CreateStore(client);

        await Post(client, first.VariantId, shop, "In", "Opening", 4);
        await Post(client, first.VariantId, deposit, "In", "Opening", 6);
        await Post(client, second.VariantId, shop, "In", "Opening", 8);
        await Post(client, first.VariantId, deposit, "Out", "InternalUse", 1);

        var byBoth = await Kardex(client, new { variantId = first.VariantId, locationId = deposit });
        byBoth.GetProperty("total").GetInt32().ShouldBe(2);
        byBoth.GetProperty("items").EnumerateArray()
            .All(i => i.GetProperty("variantId").GetString() == first.VariantId.ToString()
                && i.GetProperty("locationId").GetString() == deposit.ToString())
            .ShouldBeTrue();

        var byLocation = await Kardex(client, new { locationId = shop });
        byLocation.GetProperty("total").GetInt32().ShouldBe(2);

        var byStoreAndProduct = await Kardex(client, new { storeId, productId = first.Id });
        byStoreAndProduct.GetProperty("total").GetInt32().ShouldBe(3);

        var byReason = await Kardex(client, new { storeId, reason = "InternalUse" });
        byReason.GetProperty("total").GetInt32().ShouldBe(1);

        var bySource = await Kardex(client, new { storeId, sourceType = "Manual" });
        bySource.GetProperty("total").GetInt32().ShouldBe(4);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-3));
        var byPeriod = await Kardex(client, new { storeId, from = today.AddDays(-1).ToString("yyyy-MM-dd"), to = today.ToString("yyyy-MM-dd") });
        byPeriod.GetProperty("total").GetInt32().ShouldBe(4);

        var future = await Kardex(client, new { storeId, from = today.AddDays(2).ToString("yyyy-MM-dd") });
        future.GetProperty("total").GetInt32().ShouldBe(0);
    }

    // ---------- Mínimos (RN-CE33) ----------

    [Fact]
    [Trait("Rule", "RN-CE33")]
    public async Task Should_FlagLowStock_When_AvailableBelowEffectiveMinimum()
    {
        var client = await api.AdminAsync();
        var (storeId, shop, deposit) = await CreateStore(client);

        // Mínimo padrão 5 nos dois locais; disponível 6 na loja e 6 no depósito: acima.
        var healthy = await CreateProduct(client, defaultMinQuantity: 5);
        await Post(client, healthy.VariantId, shop, "In", "Opening", 6);
        await Post(client, healthy.VariantId, deposit, "In", "Opening", 6);

        // Mínimo do local (2) vence o padrão (5): 3 no depósito não é baixo; 6 na loja também não.
        var overridden = await CreateProduct(client, defaultMinQuantity: 5, locationMinimums:
        [
            new { variantIndex = 0, locationId = deposit, minQuantity = 2m },
        ]);
        await Post(client, overridden.VariantId, shop, "In", "Opening", 6);
        await Post(client, overridden.VariantId, deposit, "In", "Opening", 3);

        // Sem mínimo no local, vale o padrão (5): 3 no depósito é baixo.
        var low = await CreateProduct(client, defaultMinQuantity: 5);
        await Post(client, low.VariantId, shop, "In", "Opening", 6);
        await Post(client, low.VariantId, deposit, "In", "Opening", 3);

        async Task<bool> HasLowStock(Product product)
        {
            var page = await Json(await client.GetAsync($"/api/products?filters={Filters(new { search = product.Name, storeId })}"));
            return page.GetProperty("items")[0].GetProperty("hasLowStock").GetBoolean();
        }

        (await HasLowStock(healthy)).ShouldBeFalse();
        (await HasLowStock(overridden)).ShouldBeFalse();
        (await HasLowStock(low)).ShouldBeTrue();

        var onlyLow = await Json(await client.GetAsync($"/api/products?limit=100&filters={Filters(new { storeId, lowStock = "true" })}"));
        var ids = onlyLow.GetProperty("items").EnumerateArray().Select(i => Guid.Parse(Id(i))).ToList();
        ids.ShouldContain(low.Id);
        ids.ShouldNotContain(healthy.Id);
        ids.ShouldNotContain(overridden.Id);

        // Na matriz do T3, a célula mostra o mínimo efetivo e se está abaixo dele.
        var matrix = await Json(await client.GetAsync($"/api/products/{overridden.Id}/stock?storeId={storeId}"));
        var cells = matrix.GetProperty("rows")[0].GetProperty("cells").EnumerateArray()
            .ToDictionary(c => Guid.Parse(c.GetProperty("locationId").GetString()!));
        cells[shop].GetProperty("minQuantity").GetDecimal().ShouldBe(5m);
        cells[deposit].GetProperty("minQuantity").GetDecimal().ShouldBe(2m);
        cells[deposit].GetProperty("isBelowMin").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    [Trait("Rule", "RN-CE33")]
    public async Task Should_ReturnMinimumsInForm_When_EditingProduct()
    {
        var client = await api.AdminAsync();
        var (_, _, deposit) = await CreateStore(client);
        var product = await CreateProduct(client, defaultMinQuantity: 4, locationMinimums:
        [
            new { variantIndex = 0, locationId = deposit, minQuantity = 1m },
        ]);

        var form = await Json(await client.GetAsync($"/api/products/{product.Id}/form"));
        form.GetProperty("defaultMinQuantity").GetDecimal().ShouldBe(4m);
        var minimum = form.GetProperty("locationMinimums")[0];
        minimum.GetProperty("variantIndex").GetInt32().ShouldBe(0);
        minimum.GetProperty("locationId").GetString().ShouldBe(deposit.ToString());
        minimum.GetProperty("minQuantity").GetDecimal().ShouldBe(1m);
        form.GetProperty("options").GetProperty("locations").EnumerateArray()
            .Any(l => Id(l) == deposit.ToString()).ShouldBeTrue();
    }

    [Fact]
    [Trait("Rule", "RN-CE33")]
    public async Task Should_Return422_When_LocationMinimumPointsToMissingVariant()
    {
        var client = await api.AdminAsync();
        var (_, shop, _) = await CreateStore(client);

        var response = await client.PostAsJsonAsync("/api/products", new
        {
            name = Unique("Bala"),
            productType = "Standard",
            saleUnit = "Unit",
            defaultMinQuantity = -1m,
            locationMinimums = new[] { new { variantIndex = 3, locationId = shop, minQuantity = 1m } },
            variants = new[] { new { sku = Unique("BAL"), price = 1m, isActive = true } },
        });

        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity, text);
        var errors = (await Json(response)).GetProperty("fieldErrors");
        errors.TryGetProperty("defaultMinQuantity", out _).ShouldBeTrue(text);
        errors.TryGetProperty("locationMinimums[0].variantIndex", out _).ShouldBeTrue(text);
    }

    // ---------- Política ABC (RN-CE29) ----------

    [Fact]
    [Trait("Rule", "RN-CE29")]
    public async Task Should_UpdatePolicy_When_ValidValues()
    {
        var client = await api.AdminAsync();

        var policies = (await Json(await client.GetAsync("/api/stock-policies"))).EnumerateArray().ToList();
        policies.Select(p => p.GetProperty("abcClass").GetString()).ShouldBe(["A", "B", "C"]);

        var policyB = policies[1];
        var response = await client.PutAsJsonAsync($"/api/stock-policies/{Id(policyB)}", new { targetCoverageDays = 40, safetyDays = 6 });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var updated = await Json(response);
        updated.GetProperty("abcClass").GetString().ShouldBe("B");
        updated.GetProperty("targetCoverageDays").GetInt32().ShouldBe(40);
        updated.GetProperty("safetyDays").GetInt32().ShouldBe(6);
        updated.GetProperty("productCount").GetInt32().ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    [Trait("Rule", "RN-CE29")]
    public async Task Should_Return422_When_PolicyDaysNegative()
    {
        var client = await api.AdminAsync();
        var policyA = (await Json(await client.GetAsync("/api/stock-policies")))[0];

        var response = await client.PutAsJsonAsync($"/api/stock-policies/{Id(policyA)}", new { targetCoverageDays = -1, safetyDays = -2 });

        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity, text);
        var errors = (await Json(response)).GetProperty("fieldErrors");
        errors.TryGetProperty("targetCoverageDays", out _).ShouldBeTrue(text);
        errors.TryGetProperty("safetyDays", out _).ShouldBeTrue(text);
    }
}
