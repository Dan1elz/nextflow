using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Nextflow.Tests.Integration;

/// <summary>Lojas e locais com a API inteira: o commit único do par circular e os vínculos de <c>user_stores</c>.</summary>
[Collection(AccessApiCollection.Name)]
public class StoresApiTests(AccessApiFixture api)
{
    private static async Task<JsonElement> Json(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<JsonElement>();

    private static string UniqueName(string prefix) => $"{prefix} {Guid.NewGuid():N}"[..30];

    private static async Task<JsonElement> CreateStore(HttpClient client, string? name = null)
    {
        var response = await client.PostAsJsonAsync("/api/stores", new { name = name ?? UniqueName("Loja") });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await Json(response);
    }

    private static string Id(JsonElement element) => element.GetProperty("id").GetString()!;

    private static async Task<List<string>> StoresOfMe(HttpClient client)
        => [.. (await Json(await client.GetAsync("/api/auth/me"))).GetProperty("stores").EnumerateArray().Select(Id)];

    [Fact]
    [Trait("Rule", "RN-CE35")]
    public async Task Should_CreateLocationLojaAsBothDefaults_When_CreatingStore()
    {
        var client = await api.AdminAsync();

        var store = await CreateStore(client);

        var location = store.GetProperty("locations").EnumerateArray().Single();
        location.GetProperty("name").GetString().ShouldBe("Loja");
        location.GetProperty("isSellable").GetBoolean().ShouldBeTrue();
        location.GetProperty("hasStock").GetBoolean().ShouldBeFalse();
        store.GetProperty("defaultSaleLocationId").GetString().ShouldBe(Id(location));
        store.GetProperty("defaultReceiveLocationId").GetString().ShouldBe(Id(location));

        var read = await Json(await client.GetAsync($"/api/stores/{Id(store)}"));
        read.GetProperty("defaultSaleLocationId").GetString().ShouldBe(Id(location));
        read.GetProperty("locations").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    [Trait("Rule", "RN-CE35")]
    public async Task Should_ApplyDefaultRules_When_EditingLocationsAndStore()
    {
        var client = await api.AdminAsync();
        var name = UniqueName("Regras");
        var store = await CreateStore(client, name);
        var main = Id(store.GetProperty("locations")[0]);
        var deposit = await Json(await client.PostAsJsonAsync("/api/stock-locations", new { storeId = Id(store), name = "Depósito", isSellable = false }));

        var notSellable = await client.PutAsJsonAsync($"/api/stores/{Id(store)}", new { name, defaultSaleLocationId = Id(deposit), defaultReceiveLocationId = main });
        notSellable.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await Json(notSellable)).GetProperty("fieldErrors").TryGetProperty("defaultSaleLocationId", out _).ShouldBeTrue();

        (await client.DeleteAsync($"/api/stock-locations/{main}")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var moved = await client.PutAsJsonAsync($"/api/stores/{Id(store)}", new { name, defaultSaleLocationId = main, defaultReceiveLocationId = Id(deposit) });
        moved.StatusCode.ShouldBe(HttpStatusCode.OK);

        (await client.DeleteAsync($"/api/stock-locations/{Id(deposit)}")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PutAsJsonAsync($"/api/stock-locations/{main}", new { name = "Loja", isSellable = false })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        (await client.PutAsJsonAsync($"/api/stores/{Id(store)}", new { name, defaultSaleLocationId = main, defaultReceiveLocationId = main })).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.DeleteAsync($"/api/stock-locations/{Id(deposit)}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.PatchAsync($"/api/stock-locations/{Id(deposit)}/reactivate", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    [Trait("Rule", "RN-CE37")]
    public async Task Should_LinkUsersAndStoresBothWays_When_Creating()
    {
        var admin = await api.AdminAsync();
        var before = await CreateStore(admin);
        (await StoresOfMe(admin)).ShouldContain(Id(before));

        var user = await api.CreateUserAsync();
        var session = api.Authenticated((await api.LoginAsync(user.Email, user.Password)).GetProperty("token").GetString()!);
        (await StoresOfMe(session)).ShouldContain(Id(before));

        var after = await CreateStore(admin);
        (await StoresOfMe(session)).ShouldContain(Id(after));

        var login = await api.LoginAsync(user.Email, user.Password);
        login.GetProperty("user").GetProperty("stores").EnumerateArray().Select(Id).ShouldContain(Id(after));
    }

    [Fact]
    [Trait("Rule", "RN-CE36")]
    public async Task Should_LeaveCurrentUserAndOptionsUntilReactivated_When_InactivatingStore()
    {
        var client = await api.AdminAsync();
        var name = UniqueName("Inativa");
        var store = await CreateStore(client, name);
        await CreateStore(client);

        (await client.DeleteAsync($"/api/stores/{Id(store)}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await StoresOfMe(client)).ShouldNotContain(Id(store));
        (await Json(await client.GetAsync($"/api/stores/options?search={name}"))).GetArrayLength().ShouldBe(0);
        (await Json(await client.GetAsync($"/api/stock-locations/options?search={name}"))).GetArrayLength().ShouldBe(0);
        var location = await client.PostAsJsonAsync("/api/stock-locations", new { storeId = Id(store), name = "Depósito" });
        location.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await client.PatchAsync($"/api/stores/{Id(store)}/reactivate", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await StoresOfMe(client)).ShouldContain(Id(store));
        (await Json(await client.GetAsync($"/api/stock-locations/options?search={name}"))).GetArrayLength().ShouldBe(1);
    }

    [Fact]
    [Trait("Rule", "RN-CE37")]
    public async Task Should_SeeStoreCreatedWhileInactive_When_UserIsReactivated()
    {
        var admin = await api.AdminAsync();
        var user = await api.CreateUserAsync();
        (await admin.DeleteAsync($"/api/users/{user.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var store = await CreateStore(admin);

        (await admin.PatchAsync($"/api/users/{user.Id}/reactivate", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var session = api.Authenticated((await api.LoginAsync(user.Email, user.Password)).GetProperty("token").GetString()!);
        (await StoresOfMe(session)).ShouldContain(Id(store));
    }

    [Fact]
    public async Task Should_FilterBySellableAndStore_When_ListingLocationOptions()
    {
        var client = await api.AdminAsync();
        var store = await CreateStore(client);
        await client.PostAsJsonAsync("/api/stock-locations", new { storeId = Id(store), name = "Depósito", isSellable = false });
        var filter = Uri.EscapeDataString(JsonSerializer.Serialize(new { storeId = Id(store), isSellable = "true" }));

        var options = await Json(await client.GetAsync($"/api/stock-locations/options?filters={filter}"));

        var option = options.EnumerateArray().Single();
        option.GetProperty("name").GetString().ShouldBe("Loja");
        option.GetProperty("storeName").GetString().ShouldBe(store.GetProperty("name").GetString());
        option.GetProperty("isSellable").GetBoolean().ShouldBeTrue();
    }
}
