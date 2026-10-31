using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Nextflow.Domain.Utils;

namespace Nextflow.Tests.Integration;

/// <summary>Cadastros de ponta a ponta: API inteira sobre o Postgres, com o seed do IBGE e os filtros traduzidos para SQL.</summary>
[Collection(AccessApiCollection.Name)]
public class RegistryApiTests(AccessApiFixture api)
{
    private static async Task<JsonElement> Json(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<JsonElement>();

    private static string Filters(object filters) => Uri.EscapeDataString(JsonSerializer.Serialize(filters));

    /// <summary>CNPJ válido exclusivo do teste, a partir de 12 dígitos.</summary>
    private static string NewCnpj()
    {
        var digits = (Random.Shared.NextInt64(10_000_000, 99_999_999) + "0001").Select(c => c - '0').ToList();
        foreach (var weights in new[] { new[] { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 }, new[] { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 } })
        {
            var rest = digits.Select((digit, index) => digit * weights[index]).Sum() % 11;
            digits.Add(rest < 2 ? 0 : 11 - rest);
        }
        var cnpj = string.Concat(digits);
        BrazilianDocument.IsValidCnpj(cnpj).ShouldBeTrue();
        return cnpj;
    }

    private static object ClientBody(string name, string? cpf = null, string? birthDate = null, string? lastName = "Souza")
        => new { name, lastName, cpf, birthDate };

    private static object AddressBody(string ownerType, object ownerId, string cityId, bool isMain = false) => new
    {
        ownerType,
        ownerId,
        zipCode = "13010-111",
        street = "Rua das Flores",
        number = "10",
        neighborhood = "Centro",
        cityId,
        isMain,
    };

    private async Task<string> CityId(HttpClient client, string ibgeCode)
    {
        var options = await Json(await client.GetAsync($"/api/cities/options?filters={Filters(new { ibgeCode })}"));
        return options[0].GetProperty("value").GetString()!;
    }

    private static async Task<string> CreateSupplier(HttpClient client, string name, string cnpj)
    {
        var response = await client.PostAsJsonAsync("/api/suppliers", new { name, cnpj });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await Json(response)).GetProperty("id").GetString()!;
    }

    [Fact]
    public async Task Should_SeedIbgeLocationsAndServeOptions_When_ApiStarts()
    {
        var client = await api.AdminAsync();

        var states = await Json(await client.GetAsync("/api/states/options"));
        states.GetArrayLength().ShouldBe(27);

        var cities = await Json(await client.GetAsync("/api/cities/options?search=campinas"));
        cities.EnumerateArray().Select(c => c.GetProperty("label").GetString()).ShouldContain("Campinas/SP");

        var countries = await Json(await client.GetAsync("/api/countries/options?search=br"));
        countries[0].GetProperty("label").GetString().ShouldBe("Brasil");
    }

    [Fact]
    public async Task Should_LimitToTwentyItems_When_ListingCityOptions()
    {
        var client = await api.AdminAsync();

        var cities = await Json(await client.GetAsync("/api/cities/options"));

        cities.GetArrayLength().ShouldBe(20);
    }

    [Fact]
    [Trait("Rule", "RN-CD02")]
    [Trait("Rule", "RN-CD03")]
    public async Task Should_ApplyCnpjAndLeadTimeRules_When_SavingSupplier()
    {
        var client = await api.AdminAsync();
        var cnpj = NewCnpj();

        var created = await client.PostAsJsonAsync("/api/suppliers", new { name = "Doces Ltda", cnpj });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await Json(created)).GetProperty("leadTimeDays").GetInt32().ShouldBe(7);

        var duplicate = await client.PostAsJsonAsync("/api/suppliers", new { name = "Outra", cnpj });
        duplicate.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await Json(duplicate)).GetProperty("fieldErrors").TryGetProperty("cnpj", out _).ShouldBeTrue();

        var invalid = await client.PostAsJsonAsync("/api/suppliers", new { name = "Outra", cnpj = "11111111111111", leadTimeDays = 181 });
        var fields = (await Json(invalid)).GetProperty("fieldErrors");
        fields.TryGetProperty("cnpj", out _).ShouldBeTrue();
        fields.TryGetProperty("leadTimeDays", out _).ShouldBeTrue();
    }

    [Fact]
    [Trait("Rule", "RN-CD01")]
    [Trait("Rule", "L-CP6")]
    public async Task Should_AllowManyWithoutCpfAndRejectRepeated_When_SavingClient()
    {
        var client = await api.AdminAsync();

        (await client.PostAsJsonAsync("/api/clients", ClientBody("Sem Cpf Um"))).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await client.PostAsJsonAsync("/api/clients", ClientBody("Sem Cpf Dois", cpf: ""))).StatusCode.ShouldBe(HttpStatusCode.Created);

        var (_, cpf) = api.NewIdentity();
        (await client.PostAsJsonAsync("/api/clients", ClientBody("Com Cpf", cpf))).StatusCode.ShouldBe(HttpStatusCode.Created);

        var duplicate = await client.PostAsJsonAsync("/api/clients", ClientBody("Repetido", cpf));
        duplicate.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await Json(duplicate)).GetProperty("fieldErrors").TryGetProperty("cpf", out _).ShouldBeTrue();
    }

    [Fact]
    [Trait("Rule", "RN-CD04")]
    public async Task Should_KeepRecordAndLeaveOptions_When_InactivatingAndReactivating()
    {
        var client = await api.AdminAsync();
        var cnpj = NewCnpj();
        var id = await CreateSupplier(client, "Inativavel Ltda", cnpj);

        (await client.DeleteAsync($"/api/suppliers/{id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Json(await client.GetAsync($"/api/suppliers/{id}"))).GetProperty("isActive").GetBoolean().ShouldBeFalse();
        (await Json(await client.GetAsync("/api/suppliers/options?search=Inativavel"))).GetArrayLength().ShouldBe(0);

        (await client.PatchAsync($"/api/suppliers/{id}/reactivate", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var options = await Json(await client.GetAsync("/api/suppliers/options?search=Inativavel"));
        options[0].GetProperty("label").GetString().ShouldBe($"Inativavel Ltda · {BrazilianDocument.Mask(cnpj)}");
    }

    [Fact]
    [Trait("Rule", "RN-CD05")]
    public async Task Should_Return404_When_OwnerDoesNotExist()
    {
        var client = await api.AdminAsync();
        var cityId = await CityId(client, "3509502");

        var address = await client.PostAsJsonAsync("/api/addresses", AddressBody("Supplier", Guid.NewGuid(), cityId));
        var contact = await client.PostAsJsonAsync("/api/contacts", new { ownerType = "Client", ownerId = Guid.NewGuid(), type = "Phone", value = "19999990000" });

        address.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        contact.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Rule", "RN-CD05")]
    public async Task Should_KeepOneMainAndShowItOnOwner_When_SavingAddressesAndContacts()
    {
        var client = await api.AdminAsync();
        var cityId = await CityId(client, "3509502");
        var supplierId = await CreateSupplier(client, "Principal Ltda", NewCnpj());

        var first = await Json(await client.PostAsJsonAsync("/api/addresses", AddressBody("Supplier", supplierId, cityId)));
        first.GetProperty("isMain").GetBoolean().ShouldBeTrue();
        first.GetProperty("cityName").GetString().ShouldBe("Campinas");
        first.GetProperty("stateAbbreviation").GetString().ShouldBe("SP");
        first.GetProperty("zipCode").GetString().ShouldBe("13010111");

        var second = await Json(await client.PostAsJsonAsync("/api/addresses", AddressBody("Supplier", supplierId, await CityId(client, "3550308"), isMain: true)));
        second.GetProperty("isMain").GetBoolean().ShouldBeTrue();

        var filter = Filters(new { ownerType = "Supplier", ownerId = supplierId });
        var addresses = await Json(await client.GetAsync($"/api/addresses?filters={filter}"));
        addresses.GetProperty("total").GetInt32().ShouldBe(2);
        addresses.GetProperty("items").EnumerateArray().Count(a => a.GetProperty("isMain").GetBoolean()).ShouldBe(1);

        var contact = await client.PostAsJsonAsync("/api/contacts", new { ownerType = "Supplier", ownerId = supplierId, type = "WhatsApp", value = "(19) 99999-0000" });
        (await Json(contact)).GetProperty("value").GetString().ShouldBe("19999990000");

        var detail = await Json(await client.GetAsync($"/api/suppliers/{supplierId}"));
        detail.GetProperty("mainAddress").GetProperty("cityName").GetString().ShouldBe("São Paulo");
        detail.GetProperty("mainContact").GetProperty("value").GetString().ShouldBe("19999990000");

        var list = await Json(await client.GetAsync($"/api/suppliers?filters={Filters(new { search = "Principal" })}"));
        var row = list.GetProperty("items")[0];
        row.GetProperty("cityState").GetString().ShouldBe("São Paulo/SP");
        row.GetProperty("mainContact").GetString().ShouldBe("19999990000");
        row.GetProperty("productCount").GetInt32().ShouldBe(0);
        row.GetProperty("lastPurchaseOn").ValueKind.ShouldBe(JsonValueKind.Null);

        (await client.DeleteAsync($"/api/addresses/{second.GetProperty("id").GetString()}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Json(await client.GetAsync($"/api/addresses?filters={filter}"))).GetProperty("total").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Should_Return400_When_ListingAddressesOrContactsWithoutOwner()
    {
        var client = await api.AdminAsync();

        (await client.GetAsync("/api/addresses")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.GetAsync($"/api/contacts?filters={Filters(new { ownerType = "Nada", ownerId = Guid.NewGuid() })}")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_FindByNameDocumentAndCity_When_SearchingSuppliers()
    {
        var client = await api.AdminAsync();
        var cnpj = NewCnpj();
        var id = await CreateSupplier(client, "Buscavel Ltda", cnpj);
        var cityId = await CityId(client, "3304557");
        await client.PostAsJsonAsync("/api/addresses", AddressBody("Supplier", id, cityId));

        foreach (var filter in new object[]
        {
            new { search = "BUSCAVEL" },
            new { search = BrazilianDocument.Mask(cnpj) },
            new { search = cnpj[..6] },
            new { cityId, search = "buscavel" },
        })
        {
            var list = await Json(await client.GetAsync($"/api/suppliers?filters={Filters(filter)}"));
            list.GetProperty("items").EnumerateArray().Select(s => s.GetProperty("id").GetString()).ShouldContain(id);
        }

        var other = await Json(await client.GetAsync($"/api/suppliers?filters={Filters(new { cityId = await CityId(client, "3550308"), search = "buscavel" })}"));
        other.GetProperty("total").GetInt32().ShouldBe(0);
    }

    [Fact]
    [Trait("Rule", "L-CP7")]
    [Trait("Rule", "RN-CD07")]
    public async Task Should_ListOnlyBirthdaysOfCurrentBusinessMonth_When_Filtering()
    {
        var client = await api.AdminAsync();
        var today = DateOnly.FromDateTime(new Nextflow.Application.Services.BusinessClock().Now);
        var thisMonth = new DateOnly(1990, today.Month, 15).ToString("yyyy-MM-dd");
        var otherMonth = new DateOnly(1990, today.AddMonths(1).Month, 15).ToString("yyyy-MM-dd");

        await client.PostAsJsonAsync("/api/clients", ClientBody("Aniversariante", birthDate: thisMonth));
        await client.PostAsJsonAsync("/api/clients", ClientBody("Fora Do Mes", birthDate: otherMonth));
        await client.PostAsJsonAsync("/api/clients", ClientBody("Sem Nascimento"));

        var list = await Json(await client.GetAsync($"/api/clients?limit=100&filters={Filters(new { birthdaysThisMonth = "true" })}"));
        var names = list.GetProperty("items").EnumerateArray().Select(c => c.GetProperty("fullName").GetString()).ToList();

        names.ShouldContain("Aniversariante Souza");
        names.ShouldNotContain("Fora Do Mes Souza");
        names.ShouldNotContain("Sem Nascimento Souza");
    }

    [Fact]
    public async Task Should_CreateClientWithMainPhone_When_QuickCreating()
    {
        var client = await api.AdminAsync();

        var response = await client.PostAsJsonAsync("/api/clients/quick", new { name = "Rapida Cliente", phone = "(11) 97712-3456" });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await Json(response);
        created.GetProperty("label").GetString().ShouldBe("Rapida Cliente");
        created.GetProperty("phone").GetString().ShouldBe("11977123456");

        var byPhone = await Json(await client.GetAsync($"/api/clients?filters={Filters(new { search = "97712-3456" })}"));
        byPhone.GetProperty("items")[0].GetProperty("mainPhone").GetString().ShouldBe("11977123456");

        var options = await Json(await client.GetAsync("/api/clients/options?search=Rapida"));
        options[0].GetProperty("id").GetString().ShouldBe(created.GetProperty("id").GetString());
        options[0].GetProperty("phone").GetString().ShouldBe("11977123456");

        var invalid = await client.PostAsJsonAsync("/api/clients/quick", new { name = "", phone = "123" });
        var fields = (await Json(invalid)).GetProperty("fieldErrors");
        fields.TryGetProperty("name", out _).ShouldBeTrue();
        fields.TryGetProperty("phone", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Should_MaskCpfOnOption_When_ClientHasCpf()
    {
        var client = await api.AdminAsync();
        var (_, cpf) = api.NewIdentity();
        await client.PostAsJsonAsync("/api/clients", ClientBody("Mascarada", cpf));

        var byCpf = await Json(await client.GetAsync($"/api/clients/options?search={cpf}"));

        byCpf[0].GetProperty("label").GetString().ShouldBe($"Mascarada Souza · {cpf[..3]}.***.***-{cpf[9..]}");
    }
}
