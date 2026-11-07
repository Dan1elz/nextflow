using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Nextflow.Tests.Integration;

/// <summary>Promoções (T-VD7) pela API, no Postgres real, com a data de negócio fixa (F2.3).</summary>
[Collection(AccessApiCollection.Name)]
public class PromotionsApiTests(AccessApiFixture api)
{
    private const string Route = "/api/promotions";
    private static readonly DateOnly Today = new(2027, 3, 10);
    private static int _sequence;

    private static string Unique(string prefix) => $"{prefix}-{Interlocked.Increment(ref _sequence)}-{Guid.NewGuid():N}"[..24];

    private static async Task<JsonElement> Json(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<JsonElement>();

    private static string Filters(object filters) => Uri.EscapeDataString(JsonSerializer.Serialize(filters));

    private static string Day(int offset) => Today.AddDays(offset).ToString("yyyy-MM-dd");

    private static IEnumerable<string> FieldErrors(JsonElement body)
        => body.GetProperty("fieldErrors").EnumerateObject().Select(field => field.Name);

    private sealed record Product(Guid Id, string Name, Guid FirstVariantId, Guid SecondVariantId);

    private static async Task<Product> CreateProduct(HttpClient client, decimal price = 10m)
    {
        var name = Unique("Bombom");
        var response = await client.PostAsJsonAsync("/api/products", new
        {
            name,
            productType = "Standard",
            saleUnit = "Unit",
            decimalPlaces = 0,
            attributes = new[]
            {
                new { name = "Sabor", values = new[] { new { tempKey = "a", value = "Ao leite" }, new { tempKey = "b", value = "Amargo" } } },
            },
            variants = new[]
            {
                new { attributeValueIds = new[] { "a" }, sku = Unique("BOM"), price, isActive = true },
                new { attributeValueIds = new[] { "b" }, sku = Unique("BOM"), price = price * 2, isActive = true },
            },
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var productId = (await Json(response)).GetProperty("id").GetGuid();
        var variants = (await Json(await client.GetAsync($"/api/products/{productId}/variants"))).EnumerateArray().ToList();
        return new(productId, name, variants[0].GetProperty("id").GetGuid(), variants[1].GetProperty("id").GetGuid());
    }

    private static async Task<(Guid StoreId, Guid ShopId)> CreateStore(HttpClient client)
    {
        var store = await Json(await client.PostAsJsonAsync("/api/stores", new { name = Unique("Loja") }));
        return (store.GetProperty("id").GetGuid(), store.GetProperty("locations")[0].GetProperty("id").GetGuid());
    }

    private static async Task Opening(HttpClient client, Guid variantId, Guid locationId, decimal unitCost)
    {
        var response = await client.PostAsJsonAsync("/api/stock-movements/manual", new
        {
            variantId,
            locationId,
            type = "In",
            reason = "Opening",
            quantity = 10m,
            unitCost,
        });
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
    }

    private static Task<HttpResponseMessage> Create(HttpClient client, Guid productId, decimal percent, int start, int end,
        Guid? variantId = null, bool confirmWarnings = false)
        => client.PostAsJsonAsync(Route, new
        {
            productId,
            variantId,
            discountPercent = percent,
            startDate = Day(start),
            endDate = Day(end),
            confirmWarnings,
        });

    private static async Task<JsonElement> Created(HttpClient client, Guid productId, decimal percent, int start, int end, Guid? variantId = null)
    {
        var response = await Create(client, productId, percent, start, end, variantId);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await Json(response);
    }

    [Fact]
    [Trait("Rule", "RN-VD10")]
    public async Task Should_Return422_When_PromotionOutOfLimits()
    {
        await using var host = api.HostWithClock(Today);
        var client = await api.AdminAsync(host);
        var product = await CreateProduct(client);

        async Task Expect(string field, decimal percent, int start, int end)
        {
            var response = await Create(client, product.Id, percent, start, end);
            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            FieldErrors(await Json(response)).ShouldContain(field);
        }

        await Expect("discountPercent", 0m, 0, 1);
        await Expect("discountPercent", 90.01m, 0, 1);
        await Expect("startDate", 10m, -1, 1);
        await Expect("endDate", 10m, 2, 1);
        await Expect("endDate", 10m, 0, 90);

        var inactive = await CreateProduct(client);
        (await client.DeleteAsync($"/api/products/{inactive.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var refused = await Create(client, inactive.Id, 10m, 0, 1);
        refused.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        FieldErrors(await Json(refused)).ShouldContain("productId");

        var foreign = await CreateProduct(client);
        var wrongVariant = await Create(client, product.Id, 10m, 0, 1, foreign.FirstVariantId);
        wrongVariant.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        FieldErrors(await Json(wrongVariant)).ShouldContain("variantId");

        // Limites aceitos: 1% e 90%, hoje, 90 dias contando início e fim.
        await Created(client, product.Id, 1m, 0, 89, product.FirstVariantId);
        await Created(client, product.Id, 90m, 0, 89, product.SecondVariantId);
    }

    [Fact]
    [Trait("Rule", "RN-VD11")]
    public async Task Should_Return422_When_PromotionOverlapsSameScope()
    {
        await using var host = api.HostWithClock(Today);
        var client = await api.AdminAsync(host);
        var product = await CreateProduct(client);

        await Created(client, product.Id, 10m, 5, 10, product.FirstVariantId);

        // Outra variação no mesmo período não conflita; a mesma variação ou o produto inteiro, sim (L-VD10).
        await Created(client, product.Id, 10m, 5, 10, product.SecondVariantId);
        foreach (var variantId in new Guid?[] { product.FirstVariantId, null })
        {
            var overlap = await Create(client, product.Id, 20m, 10, 12, variantId);
            overlap.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            FieldErrors(await Json(overlap)).ShouldBe(["startDate"]);
        }

        // Fora do período, ou depois de cancelar a que cruzava, entra.
        await Created(client, product.Id, 20m, 11, 12);
        var whole = await Created(client, product.Id, 20m, 20, 25);
        (await client.PostAsync($"{Route}/{whole.GetProperty("id").GetGuid()}/cancel", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await Created(client, product.Id, 30m, 20, 25, product.FirstVariantId);
    }

    [Fact]
    [Trait("Rule", "RN-VD13")]
    public async Task Should_AskConfirmation_When_PromoPriceBelowAverageCost()
    {
        await using var host = api.HostWithClock(Today);
        var client = await api.AdminAsync(host);
        var product = await CreateProduct(client, price: 10m);
        var (_, shopId) = await CreateStore(client);
        await Opening(client, product.FirstVariantId, shopId, unitCost: 8m);

        // 10 × 0,8 = 8 não fica abaixo do custo; 10 × 0,79 = 7,90 fica. A segunda variação não tem custo: sem aviso.
        var preview = await Json(await client.GetAsync($"{Route}/preview?productId={product.Id}&discountPercent=21"));
        var rows = preview.GetProperty("variants").EnumerateArray().ToList();
        rows.Count.ShouldBe(2);
        rows[0].GetProperty("promoPrice").GetDecimal().ShouldBe(7.9m);
        rows[0].GetProperty("averageCost").GetDecimal().ShouldBe(8m);
        rows[0].GetProperty("belowCost").GetBoolean().ShouldBeTrue();
        rows[1].GetProperty("averageCost").ValueKind.ShouldBe(JsonValueKind.Null);
        rows[1].GetProperty("belowCost").GetBoolean().ShouldBeFalse();

        (await Created(client, product.Id, 20m, 0, 1)).GetProperty("belowCostConfirmed").GetBoolean().ShouldBeFalse();

        var warned = await Create(client, product.Id, 21m, 2, 3);
        warned.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        FieldErrors(await Json(warned)).ShouldBe(["confirm"]);

        // Só na variação sem custo não há aviso.
        await Created(client, product.Id, 50m, 2, 3, product.SecondVariantId);
    }

    [Fact]
    [Trait("Rule", "RN-VD13")]
    public async Task Should_SaveBelowCostConfirmed_When_WarningsConfirmed()
    {
        await using var host = api.HostWithClock(Today);
        var client = await api.AdminAsync(host);
        var product = await CreateProduct(client, price: 10m);
        var (_, shopId) = await CreateStore(client);
        await Opening(client, product.FirstVariantId, shopId, unitCost: 8m);

        var response = await Create(client, product.Id, 40m, 0, 6, confirmWarnings: true);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var body = await Json(response);
        body.GetProperty("belowCostConfirmed").GetBoolean().ShouldBeTrue();
        body.GetProperty("status").GetString().ShouldBe("Active");
        body.GetProperty("source").GetString().ShouldBe("Manual");
        body.GetProperty("productName").GetString().ShouldBe(product.Name);
        body.GetProperty("minListPrice").GetDecimal().ShouldBe(10m);
        body.GetProperty("maxPromoPrice").GetDecimal().ShouldBe(12m);
        body.GetProperty("createdByName").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    [Trait("Rule", "RN-VD12")]
    public async Task Should_CancelOnlyScheduledOrActive_When_CancelingPromotion()
    {
        var product = await CreateProduct(await api.AdminAsync());
        Guid active, scheduled, ended;
        await using (var past = api.HostWithClock(Today.AddDays(-10)))
        {
            var client = await api.AdminAsync(past);
            ended = (await Created(client, product.Id, 10m, -10, -8)).GetProperty("id").GetGuid();
        }

        await using var host = api.HostWithClock(Today);
        var admin = await api.AdminAsync(host);
        active = (await Created(admin, product.Id, 10m, 0, 3)).GetProperty("id").GetGuid();
        scheduled = (await Created(admin, product.Id, 10m, 5, 6)).GetProperty("id").GetGuid();

        var page = await Json(await admin.GetAsync($"{Route}?limit=50&filters={Filters(new { productId = product.Id })}"));
        page.GetProperty("total").GetInt32().ShouldBe(3);
        var status = page.GetProperty("items").EnumerateArray()
            .ToDictionary(i => i.GetProperty("id").GetGuid(), i => i.GetProperty("status").GetString());
        status[active].ShouldBe("Active");
        status[scheduled].ShouldBe("Scheduled");
        status[ended].ShouldBe("Ended");

        (await admin.PostAsync($"{Route}/{ended}/cancel", null)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await admin.PostAsync($"{Route}/{active}/cancel", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await admin.PostAsync($"{Route}/{scheduled}/cancel", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await admin.PostAsync($"{Route}/{scheduled}/cancel", null)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await admin.PostAsync($"{Route}/{Guid.NewGuid()}/cancel", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var canceled = await Json(await admin.GetAsync($"{Route}?filters={Filters(new { productId = product.Id, status = "Canceled" })}"));
        canceled.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ShouldBe([scheduled, active], ignoreOrder: true);
    }

    [Fact]
    [Trait("Rule", "RN-VD02")]
    public async Task Should_ReturnActivePromotion_When_SearchingSaleOptions()
    {
        await using var host = api.HostWithClock(Today);
        var client = await api.AdminAsync(host);
        var product = await CreateProduct(client, price: 10m);
        var (storeId, _) = await CreateStore(client);
        var promotion = await Created(client, product.Id, 25m, 0, 4, product.FirstVariantId);
        await Created(client, product.Id, 30m, 1, 4, product.SecondVariantId);

        var options = (await Json(await client.GetAsync(
                $"/api/sales/variant-options?search={Uri.EscapeDataString(product.Name)}&storeId={storeId}")))
            .EnumerateArray().ToDictionary(o => o.GetProperty("variantId").GetGuid());

        var promoted = options[product.FirstVariantId];
        promoted.GetProperty("listPrice").GetDecimal().ShouldBe(10m);
        promoted.GetProperty("effectivePrice").GetDecimal().ShouldBe(7.5m);
        var summary = promoted.GetProperty("activePromotion");
        summary.GetProperty("id").GetGuid().ShouldBe(promotion.GetProperty("id").GetGuid());
        summary.GetProperty("discountPercent").GetDecimal().ShouldBe(25m);
        summary.GetProperty("endDate").GetString().ShouldBe(Day(4));

        // A da segunda variação ainda está agendada.
        var scheduled = options[product.SecondVariantId];
        scheduled.GetProperty("effectivePrice").GetDecimal().ShouldBe(20m);
        scheduled.GetProperty("activePromotion").ValueKind.ShouldBe(JsonValueKind.Null);

        var detail = await Json(await client.GetAsync($"/api/products/{product.Id}"));
        detail.GetProperty("activePromotionPercent").GetDecimal().ShouldBe(25m);
        detail.GetProperty("minEffectivePrice").GetDecimal().ShouldBe(7.5m);
        var list = await Json(await client.GetAsync($"/api/products?filters={Filters(new { search = product.Name })}"));
        list.GetProperty("items")[0].GetProperty("activePromotionPercent").GetDecimal().ShouldBe(25m);
    }
}
