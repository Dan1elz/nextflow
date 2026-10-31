using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Nextflow.Tests.Integration;

/// <summary>RN-CE12 e RN-CE35..37 no que os testes de use case não enxergam: SQL real, rotas e corpo inválido.</summary>
[Collection(AccessApiCollection.Name)]
public class StoresRulesApiTests(AccessApiFixture api)
{
    private static async Task<JsonElement> Json(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<JsonElement>();

    private static string UniqueName(string prefix) => $"{prefix} {Guid.NewGuid():N}"[..30];

    private static string Filters(object filters) => Uri.EscapeDataString(JsonSerializer.Serialize(filters));

    private static string Id(JsonElement element) => element.GetProperty("id").GetString()!;

    private static async Task<JsonElement> CreateStore(HttpClient client, string? name = null)
    {
        var response = await client.PostAsJsonAsync("/api/stores", new { name = name ?? UniqueName("Loja") });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await Json(response);
    }

    private static async Task<JsonElement> CreateLocation(HttpClient client, string storeId, string name = "Depósito", bool isSellable = true)
    {
        var response = await client.PostAsJsonAsync("/api/stock-locations", new { storeId, name, isSellable });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await Json(response);
    }

    private static async Task<int> LocationCountOnMe(HttpClient client, string storeId)
        => (await Json(await client.GetAsync("/api/auth/me"))).GetProperty("stores").EnumerateArray()
            .Single(s => Id(s) == storeId).GetProperty("locationCount").GetInt32();

    [Theory]
    [InlineData("/api/stores")]
    [InlineData("/api/stores/options")]
    [InlineData("/api/stock-locations/options")]
    public async Task Should_Return401_When_CallingWithoutToken(string uri)
        => (await api.Anonymous().GetAsync(uri)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

    [Fact]
    [Trait("Rule", "RN-CE12")]
    public async Task Should_CountOnlyActiveLocations_When_ReadingStoresOfCurrentUser()
    {
        var client = await api.AdminAsync();
        var store = await CreateStore(client);
        (await LocationCountOnMe(client, Id(store))).ShouldBe(1);

        var deposit = await CreateLocation(client, Id(store));
        (await LocationCountOnMe(client, Id(store))).ShouldBe(2);

        (await client.DeleteAsync($"/api/stock-locations/{Id(deposit)}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await LocationCountOnMe(client, Id(store))).ShouldBe(1);
    }

    [Fact]
    [Trait("Rule", "RN-CE35")]
    public async Task Should_PersistDefaultsAndName_When_SavingStore()
    {
        var client = await api.AdminAsync();
        var store = await CreateStore(client);
        var main = Id(store.GetProperty("locations")[0]);
        var deposit = Id(await CreateLocation(client, Id(store), isSellable: false));
        var newName = UniqueName("Renomeada");

        var saved = await client.PutAsJsonAsync($"/api/stores/{Id(store)}", new { name = newName, defaultSaleLocationId = main, defaultReceiveLocationId = deposit });

        saved.StatusCode.ShouldBe(HttpStatusCode.OK);
        var read = await Json(await client.GetAsync($"/api/stores/{Id(store)}"));
        read.GetProperty("name").GetString().ShouldBe(newName);
        read.GetProperty("defaultSaleLocationId").GetString().ShouldBe(main);
        read.GetProperty("defaultReceiveLocationId").GetString().ShouldBe(deposit);
    }

    [Fact]
    [Trait("Rule", "RN-CE35")]
    public async Task Should_Return422OnTheField_When_DefaultIsFromAnotherStoreOrInactive()
    {
        var client = await api.AdminAsync();
        var store = await CreateStore(client);
        var name = store.GetProperty("name").GetString();
        var main = Id(store.GetProperty("locations")[0]);
        var foreign = Id((await CreateStore(client)).GetProperty("locations")[0]);
        var inactive = Id(await CreateLocation(client, Id(store)));
        await client.DeleteAsync($"/api/stock-locations/{inactive}");

        var cases = new (object Body, string Field)[]
        {
            (new { name, defaultSaleLocationId = foreign, defaultReceiveLocationId = main }, "defaultSaleLocationId"),
            (new { name, defaultSaleLocationId = main, defaultReceiveLocationId = foreign }, "defaultReceiveLocationId"),
            (new { name, defaultSaleLocationId = inactive, defaultReceiveLocationId = main }, "defaultSaleLocationId"),
            (new { name, defaultSaleLocationId = main, defaultReceiveLocationId = inactive }, "defaultReceiveLocationId"),
        };

        foreach (var (body, field) in cases)
        {
            var response = await client.PutAsJsonAsync($"/api/stores/{Id(store)}", body);

            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            (await Json(response)).GetProperty("fieldErrors").EnumerateObject().Select(f => f.Name).ShouldBe([field]);
        }
    }

    [Theory]
    [InlineData("""{"name":"X","defaultSaleLocationId":"nao-e-guid","defaultReceiveLocationId":"00000000-0000-0000-0000-000000000001"}""", "defaultSaleLocationId")]
    [InlineData("""{"name":"X"}""", "defaultSaleLocationId")]
    public async Task Should_Return422_When_DefaultIsInvalidOrMissing(string json, string field)
    {
        var client = await api.AdminAsync();
        var store = await CreateStore(client);

        var response = await client.PutAsync($"/api/stores/{Id(store)}", new StringContent(json, Encoding.UTF8, "application/json"));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await Json(response)).GetProperty("fieldErrors").TryGetProperty(field, out _).ShouldBeTrue();
    }

    [Fact]
    [Trait("Rule", "RN-CE35")]
    public async Task Should_LeaveOptionsAndRejectEditing_When_LocationIsInactive()
    {
        var client = await api.AdminAsync();
        var store = await CreateStore(client);
        var deposit = Id(await CreateLocation(client, Id(store)));
        var filter = Filters(new { storeId = Id(store) });

        await client.DeleteAsync($"/api/stock-locations/{deposit}");

        var options = await Json(await client.GetAsync($"/api/stock-locations/options?filters={filter}"));
        options.EnumerateArray().Select(o => o.GetProperty("name").GetString()).ShouldBe(["Loja"]);
        (await client.PutAsJsonAsync($"/api/stock-locations/{deposit}", new { name = "Mudou", isSellable = true })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.DeleteAsync($"/api/stock-locations/{deposit}")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await client.PatchAsync($"/api/stock-locations/{deposit}/reactivate", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.PatchAsync($"/api/stock-locations/{deposit}/reactivate", null)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Rule", "RN-CE36")]
    public async Task Should_KeepDefaultsAndRefuseChanges_When_StoreIsInactiveThenReactivated()
    {
        var client = await api.AdminAsync();
        var store = await CreateStore(client);
        await CreateStore(client);
        var name = store.GetProperty("name").GetString();
        var main = Id(store.GetProperty("locations")[0]);
        var deposit = Id(await CreateLocation(client, Id(store), isSellable: false));
        (await client.PutAsJsonAsync($"/api/stores/{Id(store)}", new { name, defaultSaleLocationId = main, defaultReceiveLocationId = deposit })).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await client.DeleteAsync($"/api/stores/{Id(store)}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await client.PutAsJsonAsync($"/api/stores/{Id(store)}", new { name = "Mudou", defaultSaleLocationId = main, defaultReceiveLocationId = main })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PutAsJsonAsync($"/api/stock-locations/{deposit}", new { name = "Mudou", isSellable = true })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.DeleteAsync($"/api/stock-locations/{deposit}")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PatchAsync($"/api/stock-locations/{deposit}/reactivate", null)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await client.PatchAsync($"/api/stores/{Id(store)}/reactivate", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var read = await Json(await client.GetAsync($"/api/stores/{Id(store)}"));
        read.GetProperty("name").GetString().ShouldBe(name);
        read.GetProperty("defaultSaleLocationId").GetString().ShouldBe(main);
        read.GetProperty("defaultReceiveLocationId").GetString().ShouldBe(deposit);
        read.GetProperty("locations").EnumerateArray().ShouldAllBe(l => l.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task Should_FilterIgnoringCaseAndByStatus_When_ListingStores()
    {
        var client = await api.AdminAsync();
        var store = await CreateStore(client);
        await CreateStore(client);
        var name = store.GetProperty("name").GetString()!;
        await client.DeleteAsync($"/api/stores/{Id(store)}");

        async Task<int> Total(object filter)
            => (await Json(await client.GetAsync($"/api/stores?filters={Filters(filter)}"))).GetProperty("total").GetInt32();

        (await Total(new { search = name.ToUpperInvariant() })).ShouldBe(1);
        (await Total(new { search = name, isActive = "false" })).ShouldBe(1);
        (await Total(new { search = name, isActive = "true" })).ShouldBe(0);
    }

    [Fact]
    public async Task Should_Return422ByField_When_StoreOrLocationNameIsMissing()
    {
        var client = await api.AdminAsync();
        var store = await CreateStore(client);

        var noStore = await client.PostAsJsonAsync("/api/stores", new { name = "" });
        var noLocation = await client.PostAsJsonAsync("/api/stock-locations", new { storeId = Id(store), name = " " });

        noStore.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await Json(noStore)).GetProperty("fieldErrors").TryGetProperty("name", out _).ShouldBeTrue();
        noLocation.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await Json(noLocation)).GetProperty("fieldErrors").TryGetProperty("name", out _).ShouldBeTrue();
    }
}
