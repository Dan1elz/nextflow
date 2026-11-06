using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Nextflow.Tests.Integration;

/// <summary>
/// Seletor de venda (T-VD1) e aviso de falta (T-VD1a), pela API, no Postgres real (F2.1).
/// </summary>
[Collection(AccessApiCollection.Name)]
public class SaleOptionsApiTests(AccessApiFixture api)
{
    private const string OptionsRoute = "/api/sales/variant-options";
    private const string AvailabilityRoute = "/api/stock/availability";
    private static int _sequence;

    private static string Unique(string prefix) => $"{prefix}-{Interlocked.Increment(ref _sequence)}-{Guid.NewGuid():N}"[..24];

    private static async Task<JsonElement> Json(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<JsonElement>();

    private static string Id(JsonElement element) => element.GetProperty("id").GetString()!;

    private sealed record Product(Guid Id, string Name, Guid VariantId, string Sku);

    private static async Task<Product> CreateProduct(HttpClient client, string? sku = null, string? barcode = null, decimal price = 10m)
    {
        var name = Unique("Trufa");
        sku ??= Unique("TRU");
        var response = await client.PostAsJsonAsync("/api/products", new
        {
            name,
            productType = "Standard",
            saleUnit = "Unit",
            decimalPlaces = 0,
            variants = new[] { new { sku, barcode, price, isActive = true } },
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var productId = Id(await Json(response));
        var variantId = Guid.Parse(Id((await Json(await client.GetAsync($"/api/products/{productId}/variants")))[0]));
        return new(Guid.Parse(productId), name, variantId, sku);
    }

    private static async Task<(Guid StoreId, Guid ShopId, Guid DepositId)> CreateStore(HttpClient client)
    {
        var store = await Json(await client.PostAsJsonAsync("/api/stores", new { name = Unique("Loja") }));
        var storeId = Guid.Parse(Id(store));
        var deposit = await Json(await client.PostAsJsonAsync("/api/stock-locations", new { storeId, name = Unique("Depósito"), isSellable = false }));
        return (storeId, Guid.Parse(Id(store.GetProperty("locations")[0])), Guid.Parse(Id(deposit)));
    }

    private static async Task Opening(HttpClient client, Guid variantId, Guid locationId, decimal quantity)
    {
        var response = await client.PostAsJsonAsync("/api/stock-movements/manual", new
        {
            variantId,
            locationId,
            type = "In",
            reason = "Opening",
            quantity,
            unitCost = 2m,
        });
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
    }

    private static async Task<List<JsonElement>> Options(HttpClient client, string search, Guid storeId, Guid? locationId = null)
    {
        var url = $"{OptionsRoute}?search={Uri.EscapeDataString(search)}&storeId={storeId}"
            + (locationId is { } id ? $"&locationId={id}" : string.Empty);
        var response = await client.GetAsync(url);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await Json(response)).EnumerateArray().ToList();
    }

    [Fact]
    [Trait("Rule", "RN-CE08")]
    public async Task Should_ReturnAvailableAtSaleLocation_When_SearchingOptions()
    {
        var client = await api.AdminAsync();
        var product = await CreateProduct(client);
        var (storeId, shop, deposit) = await CreateStore(client);
        await Opening(client, product.VariantId, shop, 7);
        await Opening(client, product.VariantId, deposit, 30);

        var atShop = (await Options(client, product.Name, storeId, shop)).Single();
        atShop.GetProperty("variantId").GetString().ShouldBe(product.VariantId.ToString());
        atShop.GetProperty("productId").GetString().ShouldBe(product.Id.ToString());
        atShop.GetProperty("sku").GetString().ShouldBe(product.Sku);
        atShop.GetProperty("availableQuantity").GetDecimal().ShouldBe(7m);
        atShop.GetProperty("saleUnit").GetString().ShouldBe("Unit");
        atShop.GetProperty("allowsNegative").GetBoolean().ShouldBeFalse();
        atShop.GetProperty("isActive").GetBoolean().ShouldBeTrue();
        atShop.GetProperty("lifecycle").GetString().ShouldBe("Active");

        // Outra variação sem saldo no local: disponível zero, não some da lista.
        var other = await CreateProduct(client);
        (await Options(client, other.Name, storeId, shop)).Single().GetProperty("availableQuantity").GetDecimal().ShouldBe(0m);
    }

    [Fact]
    public async Task Should_UseDefaultSaleLocation_When_LocationNotInformed()
    {
        var client = await api.AdminAsync();
        var product = await CreateProduct(client);
        var (storeId, shop, deposit) = await CreateStore(client);
        await Opening(client, product.VariantId, shop, 4);
        await Opening(client, product.VariantId, deposit, 9);

        var option = (await Options(client, product.Sku, storeId)).Single();
        option.GetProperty("availableQuantity").GetDecimal().ShouldBe(4m);
        option.GetProperty("locationId").GetString().ShouldBe(shop.ToString());
    }

    [Fact]
    public async Task Should_PutExactBarcodeFirst_When_SearchingOptions()
    {
        var client = await api.AdminAsync();
        var barcode = $"789{Random.Shared.NextInt64(1_000_000_000, 9_999_999_999)}";
        // O SKU da primeira contém o código buscado, mas não é igual: entra na lista, depois da exata.
        var partial = await CreateProduct(client, sku: $"{barcode}9");
        var exact = await CreateProduct(client, barcode: barcode);
        var (storeId, _, _) = await CreateStore(client);

        var options = await Options(client, barcode, storeId);
        options.Count.ShouldBe(2);
        options[0].GetProperty("variantId").GetString().ShouldBe(exact.VariantId.ToString());
        options[0].GetProperty("barcode").GetString().ShouldBe(barcode);
        options[1].GetProperty("variantId").GetString().ShouldBe(partial.VariantId.ToString());
    }

    [Fact]
    [Trait("Rule", "RN-VD02")]
    public async Task Should_ReturnEffectivePrice_When_SearchingOptions()
    {
        var client = await api.AdminAsync();
        var product = await CreateProduct(client, price: 12.5m);
        var (storeId, _, _) = await CreateStore(client);

        var option = (await Options(client, product.Name, storeId)).Single();
        option.GetProperty("listPrice").GetDecimal().ShouldBe(12.5m);
        // Sem promoção (F2.3), o preço efetivo do IProductPricingService é o de tabela.
        option.GetProperty("effectivePrice").GetDecimal().ShouldBe(12.5m);
        option.TryGetProperty("activePromotion", out var promotion).ShouldBeTrue();
        promotion.ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Should_Return422_When_LocationNotSellableOrFromOtherStore()
    {
        var client = await api.AdminAsync();
        var (storeId, _, deposit) = await CreateStore(client);
        var (_, otherShop, _) = await CreateStore(client);

        foreach (var locationId in new[] { deposit, otherShop })
        {
            var response = await client.GetAsync($"{OptionsRoute}?search=x&storeId={storeId}&locationId={locationId}");
            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            (await response.Content.ReadAsStringAsync()).ShouldContain("locationId");
        }

        var unknownStore = await client.GetAsync($"{OptionsRoute}?search=x&storeId={Guid.NewGuid()}");
        unknownStore.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await unknownStore.Content.ReadAsStringAsync()).ShouldContain("storeId");
    }

    [Fact]
    [Trait("Rule", "RN-CE08")]
    public async Task Should_ListOtherLocationsWithStock_When_CheckingAvailability()
    {
        var client = await api.AdminAsync();
        var product = await CreateProduct(client);
        var (storeId, shop, deposit) = await CreateStore(client);
        var (otherStoreId, otherShop, otherDeposit) = await CreateStore(client);
        await Opening(client, product.VariantId, shop, 1);
        await Opening(client, product.VariantId, deposit, 5);
        await Opening(client, product.VariantId, otherShop, 20);
        // Local zerado não aparece.
        await Opening(client, product.VariantId, otherDeposit, 3);
        var zero = await client.PostAsJsonAsync("/api/stock-movements/manual", new
        {
            variantId = product.VariantId,
            locationId = otherDeposit,
            type = "Out",
            reason = "Breakage",
            quantity = 3m,
        });
        zero.StatusCode.ShouldBe(HttpStatusCode.NoContent, await zero.Content.ReadAsStringAsync());

        var response = await client.GetAsync($"{AvailabilityRoute}?variantId={product.VariantId}&excludeLocationId={shop}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await Json(response);
        body.GetProperty("variantId").GetString().ShouldBe(product.VariantId.ToString());

        var rows = body.GetProperty("byLocation").EnumerateArray().ToList();
        rows.Count.ShouldBe(2);
        // Mesma loja primeiro, mesmo com menos saldo.
        rows[0].GetProperty("locationId").GetString().ShouldBe(deposit.ToString());
        rows[0].GetProperty("storeId").GetString().ShouldBe(storeId.ToString());
        rows[0].GetProperty("sameStore").GetBoolean().ShouldBeTrue();
        rows[0].GetProperty("available").GetDecimal().ShouldBe(5m);
        rows[0].GetProperty("locationName").GetString().ShouldNotBeNullOrWhiteSpace();
        rows[1].GetProperty("locationId").GetString().ShouldBe(otherShop.ToString());
        rows[1].GetProperty("storeId").GetString().ShouldBe(otherStoreId.ToString());
        rows[1].GetProperty("sameStore").GetBoolean().ShouldBeFalse();
        rows[1].GetProperty("available").GetDecimal().ShouldBe(20m);
        rows[1].GetProperty("storeName").GetString().ShouldNotBeNullOrWhiteSpace();
    }
}
