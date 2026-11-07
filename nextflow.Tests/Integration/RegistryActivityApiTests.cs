using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Nextflow.Domain.Utils;
using Nextflow.Infrastructure.Seeders.Demo;

namespace Nextflow.Tests.Integration;

/// <summary>
/// Abas Produtos e Compras do fornecedor e do cliente, e os filtros das listagens que dependem de endereço, vendas e
/// pedidos de compra, pela API no Postgres real.
/// </summary>
[Collection(AccessApiCollection.Name)]
public class RegistryActivityApiTests(AccessApiFixture api)
{
    private static int _sequence;

    private static string Unique(string prefix) => $"{prefix}-{Interlocked.Increment(ref _sequence)}-{Guid.NewGuid():N}"[..24];

    private static async Task<JsonElement> Json(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<JsonElement>();

    private static Guid Id(JsonElement element) => Guid.Parse(element.GetProperty("id").GetString()!);

    private static string Filters(object filters) => Uri.EscapeDataString(JsonSerializer.Serialize(filters));

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

    private static async Task<Guid> CreateSupplier(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/suppliers", new { name, cnpj = NewCnpj(), leadTimeDays = 10 });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return Id(await Json(response));
    }

    private static async Task<Guid> CreateClient(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/clients", new { name, lastName = "Teste" });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return Id(await Json(response));
    }

    private static async Task<(Guid ProductId, Guid VariantId)> CreateProduct(HttpClient client, string name, object[]? suppliers = null)
    {
        var response = await client.PostAsJsonAsync("/api/products", new
        {
            name,
            productType = "Standard",
            saleUnit = "Unit",
            decimalPlaces = 0,
            allowsNegative = true,
            variants = new[] { new { sku = Unique("ATV"), price = 10m, isActive = true } },
            suppliers = suppliers ?? [],
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var productId = Id(await Json(response));
        var variantId = Id((await Json(await client.GetAsync($"/api/products/{productId}/variants")))[0]);
        return (productId, variantId);
    }

    private static async Task<(Guid StoreId, Guid ShopId)> CreateStore(HttpClient client)
    {
        var store = await Json(await client.PostAsJsonAsync("/api/stores", new { name = Unique("Loja") }));
        return (Id(store), Id(store.GetProperty("locations")[0]));
    }

    private static async Task<string> CityId(HttpClient client, string ibgeCode)
        => (await Json(await client.GetAsync($"/api/cities/options?filters={Filters(new { ibgeCode })}")))[0].GetProperty("value").GetString()!;

    private static async Task AddAddress(HttpClient client, string ownerType, Guid ownerId, string cityId)
    {
        var response = await client.PostAsJsonAsync("/api/addresses", new
        {
            ownerType,
            ownerId,
            zipCode = "13450021",
            street = "Rua Teste",
            number = "1",
            neighborhood = "Centro",
            cityId,
            isMain = true,
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
    }

    private static async Task<List<string>> ListIds(HttpClient client, string route, object filters)
        => [.. (await Json(await client.GetAsync($"{route}?limit=100&filters={Filters(filters)}")))
            .GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetString()!)];

    [Fact]
    public async Task Should_ListLinkedProducts_When_OpeningSupplierProductsTab()
    {
        var client = await api.AdminAsync();
        var supplierId = await CreateSupplier(client, Unique("Forn"));
        var preferred = Unique("Trufa");
        await CreateProduct(client, preferred, [new { supplierId, isPreferred = true, supplierSku = "F-01", leadTimeDays = 4 }]);
        await CreateProduct(client, Unique("Avulso"));

        var products = await Json(await client.GetAsync($"/api/suppliers/{supplierId}/products"));

        products.GetArrayLength().ShouldBe(1);
        products[0].GetProperty("productName").GetString().ShouldBe(preferred);
        products[0].GetProperty("supplierSku").GetString().ShouldBe("F-01");
        products[0].GetProperty("isPreferred").GetBoolean().ShouldBeTrue();
        products[0].GetProperty("leadTimeDays").GetInt32().ShouldBe(4);
    }

    [Fact]
    public async Task Should_FilterAndSummarizePurchases_When_ListingSuppliers()
    {
        var client = await api.AdminAsync();
        var tag = Unique("ATIV");
        var withOrder = await CreateSupplier(client, $"{tag} com pedido");
        var withoutOrder = await CreateSupplier(client, $"{tag} sem pedido");
        var (_, variantId) = await CreateProduct(client, Unique("Bala"), [new { supplierId = withOrder, isPreferred = true }]);
        var (_, shopId) = await CreateStore(client);

        var created = await Json(await client.PostAsJsonAsync("/api/purchase-orders", new
        {
            supplierId = withOrder,
            receiveLocationId = shopId,
            items = new[] { new { variantId, quantity = 10m, unitCost = 2m } },
        }));
        var sent = await client.PostAsJsonAsync($"/api/purchase-orders/{Id(created)}/send", new { version = created.GetProperty("version").GetUInt32() });
        sent.StatusCode.ShouldBe(HttpStatusCode.OK, await sent.Content.ReadAsStringAsync());

        (await ListIds(client, "/api/suppliers", new { search = tag, purchases = "with" })).ShouldBe([withOrder.ToString()]);
        (await ListIds(client, "/api/suppliers", new { search = tag, purchases = "without" })).ShouldBe([withoutOrder.ToString()]);
        (await ListIds(client, "/api/suppliers", new { search = tag, purchases = "last30" })).ShouldBe([withOrder.ToString()]);
        (await ListIds(client, "/api/suppliers", new { search = tag, purchases = "inactive90" })).ShouldBeEmpty();
        (await ListIds(client, "/api/suppliers", new { search = tag, pendingOrders = true })).ShouldBe([withOrder.ToString()]);
        (await ListIds(client, "/api/suppliers", new { search = tag, pendingOrders = false })).ShouldBe([withoutOrder.ToString()]);

        var row = (await Json(await client.GetAsync($"/api/suppliers?filters={Filters(new { search = $"{tag} com" })}"))).GetProperty("items")[0];
        row.GetProperty("lastPurchaseOn").ValueKind.ShouldBe(JsonValueKind.String);

        var orders = await Json(await client.GetAsync($"/api/purchase-orders?filters={Filters(new { supplierId = withOrder })}"));
        orders.GetProperty("items").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task Should_FilterAndSummarizeSales_When_ListingClients()
    {
        var client = await api.AdminAsync();
        var tag = Unique("CLI");
        var buyer = await CreateClient(client, $"{tag} comprador");
        var idle = await CreateClient(client, $"{tag} parado");
        var (_, variantId) = await CreateProduct(client, Unique("Pirulito"));
        var (storeId, _) = await CreateStore(client);

        var sale = await client.PostAsJsonAsync("/api/sales/quick", new
        {
            storeId,
            clientId = buyer,
            items = new[] { new { variantId, quantity = 2m, discount = 0m, expectedUnitPrice = 10m } },
            payments = new[] { new { method = "Pix", amount = 20m } },
        });
        sale.StatusCode.ShouldBe(HttpStatusCode.Created, await sale.Content.ReadAsStringAsync());

        (await ListIds(client, "/api/clients", new { search = tag, purchases = "with" })).ShouldBe([buyer.ToString()]);
        (await ListIds(client, "/api/clients", new { search = tag, purchases = "without" })).ShouldBe([idle.ToString()]);

        var row = (await Json(await client.GetAsync($"/api/clients?filters={Filters(new { search = $"{tag} comprador" })}"))).GetProperty("items")[0];
        row.GetProperty("orderCount").GetInt32().ShouldBe(1);
        row.GetProperty("totalSpent").GetDecimal().ShouldBe(20m);
        row.GetProperty("lastPurchaseOn").ValueKind.ShouldBe(JsonValueKind.String);

        var orders = await Json(await client.GetAsync($"/api/orders?filters={Filters(new { clientId = buyer })}"));
        orders.GetProperty("items").GetArrayLength().ShouldBe(1);
        (await Json(await client.GetAsync($"/api/orders?filters={Filters(new { clientId = idle })}"))).GetProperty("items").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Should_FilterByCityAndState_When_ListingClients()
    {
        var client = await api.AdminAsync();
        var tag = Unique("UF");
        var inSantaBarbara = await CreateClient(client, $"{tag} sbo");
        var inRio = await CreateClient(client, $"{tag} rio");
        await AddAddress(client, "Client", inSantaBarbara, await CityId(client, "3545803"));
        await AddAddress(client, "Client", inRio, await CityId(client, "3304557"));
        var sp = (await Json(await client.GetAsync("/api/states/options?search=paulo")))[0].GetProperty("value").GetString();

        (await ListIds(client, "/api/clients", new { search = tag, cityId = await CityId(client, "3545803") })).ShouldBe([inSantaBarbara.ToString()]);
        (await ListIds(client, "/api/clients", new { search = tag, stateId = sp })).ShouldBe([inSantaBarbara.ToString()]);
    }

    [Fact]
    public void Should_UseValidAndUniqueCpfs_When_SeedingDemoClients()
    {
        var cpfs = DemoRegistry.Clients.Select(c => c.Cpf).ToList();
        cpfs.ShouldAllBe(cpf => BrazilianDocument.IsValidCpf(cpf));
        cpfs.Distinct().Count().ShouldBe(cpfs.Count);
        DemoRegistry.SupplierAddresses.Length.ShouldBe(DemoCatalog.Suppliers.Length);
    }
}
