using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Nextflow.Application.Services;

namespace Nextflow.Tests.Integration;

/// <summary>Regras de RN-CD01 a RN-CD09 que só aparecem com a API inteira e o SQL de verdade.</summary>
[Collection(AccessApiCollection.Name)]
public class RegistryRulesApiTests(AccessApiFixture api)
{
    private const string InvalidBodyMessage = "O corpo da requisição está vazio ou não é um JSON válido.";

    private static async Task<JsonElement> Json(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<JsonElement>();

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

    private static string UniqueName(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    private static async Task<string> CreateSupplier(HttpClient client, string name, string? cnpj = null, int leadTimeDays = 7)
    {
        var response = await client.PostAsJsonAsync("/api/suppliers", new { name, cnpj = cnpj ?? NewCnpj(), leadTimeDays });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await Json(response)).GetProperty("id").GetString()!;
    }

    private static async Task<string> CreateClient(HttpClient client, string name, string? cpf = null, string? birthDate = null)
    {
        var response = await client.PostAsJsonAsync("/api/clients", new { name, cpf, birthDate });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await Json(response)).GetProperty("id").GetString()!;
    }

    private static async Task<string> CityId(HttpClient client, string ibgeCode)
    {
        var options = await Json(await client.GetAsync($"/api/cities/options?filters={Filters(new { ibgeCode })}"));
        return options[0].GetProperty("value").GetString()!;
    }

    private static object AddressBody(string cityId, string street = "Rua A", bool isMain = false, object? ownerType = null, object? ownerId = null) => new
    {
        ownerType,
        ownerId,
        zipCode = "13010111",
        street,
        number = "10",
        neighborhood = "Centro",
        cityId,
        isMain,
    };

    private static async Task<List<JsonElement>> Owned(HttpClient client, string resource, string ownerType, string ownerId)
    {
        var body = await Json(await client.GetAsync($"/api/{resource}?filters={Filters(new { ownerType, ownerId })}"));
        return [.. body.GetProperty("items").EnumerateArray()];
    }

    private static ByteArrayContent RawBytes(params byte[] bytes)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return content;
    }

    private static StringContent RawJson(string json) => new(json, Encoding.UTF8, "application/json");

    // --- Autorização

    [Theory]
    [InlineData("/api/clients")]
    [InlineData("/api/clients/options")]
    [InlineData("/api/suppliers")]
    [InlineData("/api/suppliers/options")]
    [InlineData("/api/addresses")]
    [InlineData("/api/contacts")]
    [InlineData("/api/countries/options")]
    [InlineData("/api/states/options")]
    [InlineData("/api/cities/options")]
    public async Task Should_Return401_When_CallingRegistryWithoutToken(string uri)
        => (await api.Anonymous().GetAsync(uri)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

    // --- Busca por documento: o cast (string)(object) só vale traduzido para SQL

    [Fact]
    [Trait("Rule", "RN-CD01")]
    public async Task Should_FindOnlyTheClient_When_SearchingListByCpfDigitsInAnyFormat()
    {
        var client = await api.AdminAsync();
        var (_, cpf) = api.NewIdentity();
        var (_, otherCpf) = api.NewIdentity();
        var id = await CreateClient(client, UniqueName("ComCpf"), cpf);
        var otherId = await CreateClient(client, UniqueName("OutroCpf"), otherCpf);
        var withoutCpfId = await CreateClient(client, UniqueName("SemCpf"));
        var masked = $"{cpf[..3]}.{cpf[3..6]}.{cpf[6..9]}-{cpf[9..]}";

        foreach (var search in new[] { cpf, masked })
        {
            var ids = (await Json(await client.GetAsync($"/api/clients?limit=100&filters={Filters(new { search })}")))
                .GetProperty("items").EnumerateArray().Select(c => c.GetProperty("id").GetString()).ToList();

            ids.ShouldBe([id]);
        }

        var partial = (await Json(await client.GetAsync($"/api/clients?limit=100&filters={Filters(new { search = cpf[2..9] })}")))
            .GetProperty("items").EnumerateArray().Select(c => c.GetProperty("id").GetString()).ToList();
        partial.ShouldContain(id);
        partial.ShouldNotContain(otherId);
        partial.ShouldNotContain(withoutCpfId);
    }

    [Fact]
    [Trait("Rule", "RN-CD02")]
    public async Task Should_FindOnlyTheSupplier_When_SearchingListAndOptionsByCnpjDigits()
    {
        var client = await api.AdminAsync();
        var cnpj = NewCnpj();
        var id = await CreateSupplier(client, UniqueName("ComCnpj"), cnpj);
        await CreateSupplier(client, UniqueName("OutroCnpj"));

        var list = await Json(await client.GetAsync($"/api/suppliers?limit=100&filters={Filters(new { search = cnpj })}"));
        list.GetProperty("items").EnumerateArray().Select(s => s.GetProperty("id").GetString()).ShouldBe([id]);

        var options = await Json(await client.GetAsync($"/api/suppliers/options?search={cnpj}"));
        options.EnumerateArray().Select(o => o.GetProperty("value").GetString()).ShouldBe([id]);
    }

    [Fact]
    public async Task Should_ReturnEmpty_When_DigitsMatchNoDocument()
    {
        var client = await api.AdminAsync();

        var clients = await Json(await client.GetAsync($"/api/clients?filters={Filters(new { search = "00000000000000" })}"));
        var suppliers = await Json(await client.GetAsync($"/api/suppliers?filters={Filters(new { search = "00000000000000" })}"));

        clients.GetProperty("total").GetInt32().ShouldBe(0);
        suppliers.GetProperty("total").GetInt32().ShouldBe(0);
    }

    // --- Regras de cliente e fornecedor

    [Fact]
    [Trait("Rule", "RN-CD01")]
    [Trait("Rule", "L-CP6")]
    public async Task Should_RejectCpfOfAnotherAndAllowRemovingOwn_When_UpdatingClient()
    {
        var client = await api.AdminAsync();
        var (_, cpf) = api.NewIdentity();
        var (_, otherCpf) = api.NewIdentity();
        var id = await CreateClient(client, UniqueName("Dono"), cpf);
        await CreateClient(client, UniqueName("Outro"), otherCpf);

        var repeated = await client.PutAsJsonAsync($"/api/clients/{id}", new { name = "Dono", cpf = otherCpf });
        repeated.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await Json(repeated)).GetProperty("fieldErrors").GetProperty("cpf")[0].GetString().ShouldBe("Já existe um cliente com este CPF.");

        var removed = await client.PutAsJsonAsync($"/api/clients/{id}", new { name = "Dono", cpf = "" });
        removed.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Json(await client.GetAsync($"/api/clients/{id}"))).GetProperty("cpf").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Theory]
    [InlineData("2020-01-01")]
    [InlineData(null)]
    public async Task Should_CreateClient_When_BirthDateIsPastOrAbsent(string? birthDate)
        => await CreateClient(await api.AdminAsync(), UniqueName("Nascimento"), birthDate: birthDate);

    [Fact]
    [Trait("Rule", "RN-CD04")]
    public async Task Should_Return400AndKeepData_When_EditingInactiveClientOrSupplier()
    {
        var client = await api.AdminAsync();
        var clientName = UniqueName("InativoCliente");
        var supplierName = UniqueName("InativoFornecedor");
        var clientId = await CreateClient(client, clientName);
        var cnpj = NewCnpj();
        var supplierId = await CreateSupplier(client, supplierName, cnpj);
        await client.DeleteAsync($"/api/clients/{clientId}");
        await client.DeleteAsync($"/api/suppliers/{supplierId}");

        (await client.PutAsJsonAsync($"/api/clients/{clientId}", new { name = "Mudou" })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PutAsJsonAsync($"/api/suppliers/{supplierId}", new { name = "Mudou", cnpj })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await Json(await client.GetAsync($"/api/clients/{clientId}"))).GetProperty("name").GetString().ShouldBe(clientName);
        (await Json(await client.GetAsync($"/api/suppliers/{supplierId}"))).GetProperty("name").GetString().ShouldBe(supplierName);
    }

    [Fact]
    [Trait("Rule", "RN-CD04")]
    public async Task Should_LeaveClientOptions_When_ClientIsInactive()
    {
        var client = await api.AdminAsync();
        var name = UniqueName("SaiDoCombo");
        var id = await CreateClient(client, name);

        await client.DeleteAsync($"/api/clients/{id}");

        (await Json(await client.GetAsync($"/api/clients/options?search={name}"))).GetArrayLength().ShouldBe(0);
    }

    // --- Endereço e contato

    [Fact]
    [Trait("Rule", "RN-CD05")]
    public async Task Should_KeepOwner_When_UpdateBodyTriesToChangeIt()
    {
        var client = await api.AdminAsync();
        var cityId = await CityId(client, "3509502");
        var owner = await CreateSupplier(client, UniqueName("Dono"));
        var other = await CreateSupplier(client, UniqueName("Outro"));
        var address = await Json(await client.PostAsJsonAsync("/api/addresses", AddressBody(cityId, ownerType: "Supplier", ownerId: owner)));

        var update = await client.PutAsJsonAsync(
            $"/api/addresses/{address.GetProperty("id").GetString()}",
            AddressBody(cityId, street: "Rua Editada", isMain: true, ownerType: "Client", ownerId: other));

        update.StatusCode.ShouldBe(HttpStatusCode.OK);
        var owned = await Owned(client, "addresses", "Supplier", owner);
        owned.Single().GetProperty("street").GetString().ShouldBe("Rua Editada");
        (await Owned(client, "addresses", "Supplier", other)).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Rule", "RN-CD06")]
    public async Task Should_MoveMainInDatabase_When_EditingAddressAsMain()
    {
        var client = await api.AdminAsync();
        var cityId = await CityId(client, "3509502");
        var owner = await CreateClient(client, UniqueName("Enderecos"));
        var first = await Json(await client.PostAsJsonAsync("/api/addresses", AddressBody(cityId, "Rua Um", ownerType: "Client", ownerId: owner)));
        var second = await Json(await client.PostAsJsonAsync("/api/addresses", AddressBody(cityId, "Rua Dois", ownerType: "Client", ownerId: owner)));
        second.GetProperty("isMain").GetBoolean().ShouldBeFalse();

        var update = await client.PutAsJsonAsync($"/api/addresses/{second.GetProperty("id").GetString()}", AddressBody(cityId, "Rua Dois", isMain: true));

        update.StatusCode.ShouldBe(HttpStatusCode.OK);
        var owned = await Owned(client, "addresses", "Client", owner);
        owned.Single(a => a.GetProperty("isMain").GetBoolean()).GetProperty("id").GetString().ShouldBe(second.GetProperty("id").GetString());
        owned.Single(a => a.GetProperty("id").GetString() == first.GetProperty("id").GetString()).GetProperty("isMain").GetBoolean().ShouldBeFalse();
        (await Json(await client.GetAsync($"/api/clients/{owner}"))).GetProperty("mainAddress").GetProperty("street").GetString().ShouldBe("Rua Dois");
    }

    [Fact]
    [Trait("Rule", "RN-CD06")]
    public async Task Should_MoveMainInDatabase_When_EditingContactAsMain()
    {
        var client = await api.AdminAsync();
        var owner = await CreateClient(client, UniqueName("Contatos"));
        await client.PostAsJsonAsync("/api/contacts", new { ownerType = "Client", ownerId = owner, type = "Phone", value = "11988887766" });
        var second = await Json(await client.PostAsJsonAsync("/api/contacts", new { ownerType = "Client", ownerId = owner, type = "Email", value = "a@b.com" }));

        var update = await client.PutAsJsonAsync($"/api/contacts/{second.GetProperty("id").GetString()}", new { type = "Email", value = "c@d.com", isMain = true });

        update.StatusCode.ShouldBe(HttpStatusCode.OK);
        var owned = await Owned(client, "contacts", "Client", owner);
        owned.Single(c => c.GetProperty("isMain").GetBoolean()).GetProperty("value").GetString().ShouldBe("c@d.com");
    }

    [Fact]
    [Trait("Rule", "RN-CD06")]
    public async Task Should_LeaveWithoutMain_When_UnmarkingTheOnlyAddress()
    {
        var client = await api.AdminAsync();
        var cityId = await CityId(client, "3509502");
        var owner = await CreateClient(client, UniqueName("Unico"));
        var only = await Json(await client.PostAsJsonAsync("/api/addresses", AddressBody(cityId, ownerType: "Client", ownerId: owner)));

        await client.PutAsJsonAsync($"/api/addresses/{only.GetProperty("id").GetString()}", AddressBody(cityId, isMain: false));

        (await Owned(client, "addresses", "Client", owner)).Single().GetProperty("isMain").GetBoolean().ShouldBeFalse();
        (await Json(await client.GetAsync($"/api/clients/{owner}"))).GetProperty("mainAddress").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Theory]
    [InlineData("Client")]
    [InlineData("Supplier")]
    [Trait("Rule", "RN-CD10")]
    public async Task Should_Return400UntilReactivated_When_OwnerIsInactive(string ownerType)
    {
        var client = await api.AdminAsync();
        var cityId = await CityId(client, "3509502");
        var (route, owner) = ownerType == "Client"
            ? ("clients", await CreateClient(client, UniqueName("DonoInativo")))
            : ("suppliers", await CreateSupplier(client, UniqueName("DonoInativo")));
        var address = await Json(await client.PostAsJsonAsync("/api/addresses", AddressBody(cityId, ownerType: ownerType, ownerId: owner)));
        var contact = await Json(await client.PostAsJsonAsync("/api/contacts", new { ownerType, ownerId = owner, type = "Phone", value = "11988887766" }));
        var addressUri = $"/api/addresses/{address.GetProperty("id").GetString()}";
        var contactUri = $"/api/contacts/{contact.GetProperty("id").GetString()}";
        var contactEdit = new { type = "Phone", value = "1133334444" };

        (await client.DeleteAsync($"/api/{route}/{owner}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var responses = new[]
        {
            await client.PostAsJsonAsync("/api/addresses", AddressBody(cityId, ownerType: ownerType, ownerId: owner)),
            await client.PutAsJsonAsync(addressUri, AddressBody(cityId, street: "Rua Nova")),
            await client.PostAsJsonAsync("/api/contacts", new { ownerType, ownerId = owner, type = "Phone", value = "11977776666" }),
            await client.PutAsJsonAsync(contactUri, contactEdit),
        };
        foreach (var response in responses)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await Json(response)).GetProperty("message").GetString()!.ShouldContain("inativo");
        }

        var addresses = await Owned(client, "addresses", ownerType, owner);
        addresses.Single().GetProperty("street").GetString().ShouldBe("Rua A");
        (await Owned(client, "contacts", ownerType, owner)).Single().GetProperty("value").GetString().ShouldBe("11988887766");

        (await client.PatchAsync($"/api/{route}/{owner}/reactivate", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await client.PutAsJsonAsync(addressUri, AddressBody(cityId, street: "Rua Nova"))).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.PutAsJsonAsync(contactUri, contactEdit)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Rule", "RN-CD06")]
    public async Task Should_ShowPhoneOnlyFromMainContact_When_ListingAndInCombo()
    {
        var client = await api.AdminAsync();
        var name = UniqueName("TelefonePrincipal");
        var owner = await CreateClient(client, name);
        await client.PostAsJsonAsync("/api/contacts", new { ownerType = "Client", ownerId = owner, type = "Email", value = "a@b.com" });
        var phone = await Json(await client.PostAsJsonAsync("/api/contacts", new { ownerType = "Client", ownerId = owner, type = "Phone", value = "11988887766" }));
        var phoneUri = $"/api/contacts/{phone.GetProperty("id").GetString()}";

        async Task ShouldShow(string? expected)
        {
            var list = await Json(await client.GetAsync($"/api/clients?filters={Filters(new { search = name })}"));
            var option = await Json(await client.GetAsync($"/api/clients/options?search={Uri.EscapeDataString(name)}"));
            list.GetProperty("items")[0].GetProperty("mainPhone").GetString().ShouldBe(expected);
            option[0].GetProperty("phone").GetString().ShouldBe(expected);
        }

        await ShouldShow(null); // principal é o e-mail

        await client.PutAsJsonAsync(phoneUri, new { type = "Phone", value = "11988887766", isMain = true });
        await ShouldShow("11988887766");

        await client.PutAsJsonAsync(phoneUri, new { type = "Phone", value = "11988887766", isMain = false });
        await ShouldShow(null); // zero principal
    }

    [Fact]
    [Trait("Rule", "RN-CD06")]
    public async Task Should_NotPromoteAnother_When_DeletingTheMainContact()
    {
        var client = await api.AdminAsync();
        var owner = await CreateClient(client, UniqueName("SemPromocao"));
        var main = await Json(await client.PostAsJsonAsync("/api/contacts", new { ownerType = "Client", ownerId = owner, type = "Phone", value = "11988887766" }));
        await client.PostAsJsonAsync("/api/contacts", new { ownerType = "Client", ownerId = owner, type = "Email", value = "a@b.com" });

        (await client.DeleteAsync($"/api/contacts/{main.GetProperty("id").GetString()}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var owned = await Owned(client, "contacts", "Client", owner);
        owned.Single().GetProperty("isMain").GetBoolean().ShouldBeFalse();
        (await Json(await client.GetAsync($"/api/clients/{owner}"))).GetProperty("mainContact").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    [Trait("Rule", "RN-CD05")]
    public async Task Should_Return422OnCity_When_CityDoesNotExist()
    {
        var client = await api.AdminAsync();
        var owner = await CreateClient(client, UniqueName("SemCidade"));

        var response = await client.PostAsJsonAsync("/api/addresses", AddressBody(Guid.NewGuid().ToString(), ownerType: "Client", ownerId: owner));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await Json(response)).GetProperty("fieldErrors").TryGetProperty("cityId", out _).ShouldBeTrue();
    }

    // --- Aniversariantes, localidade e cadastro rápido

    [Theory]
    [InlineData("false")]
    [InlineData("nao-e-booleano")]
    [Trait("Rule", "RN-CD07")]
    [Trait("Rule", "L-CP7")]
    public async Task Should_NotFilterByBirthday_When_FlagIsNotTrue(string flag)
    {
        var client = await api.AdminAsync();
        var name = UniqueName("SemNascimento");
        await CreateClient(client, name);

        var body = await client.GetAsync($"/api/clients?filters={Filters(new { search = name, birthdaysThisMonth = flag })}");

        (await Json(body)).GetProperty("total").GetInt32().ShouldBe(1);
    }

    [Fact]
    [Trait("Rule", "RN-CD07")]
    [Trait("Rule", "L-CP7")]
    public async Task Should_CombineBirthdayWithSearch_When_ClientIsBornThisMonthInAnotherYear()
    {
        var client = await api.AdminAsync();
        var today = DateOnly.FromDateTime(new BusinessClock().Now);
        var name = UniqueName("Combinado");
        await CreateClient(client, name, birthDate: new DateOnly(1975, today.Month, 1).ToString("yyyy-MM-dd"));

        var body = await client.GetAsync($"/api/clients?filters={Filters(new { search = name, birthdaysThisMonth = "true" })}");

        (await Json(body)).GetProperty("total").GetInt32().ShouldBe(1);
    }

    [Theory]
    [InlineData("POST", "/api/countries")]
    [InlineData("POST", "/api/states")]
    [InlineData("POST", "/api/cities")]
    [InlineData("DELETE", "/api/countries/00000000-0000-0000-0000-000000000001")]
    [InlineData("DELETE", "/api/states/00000000-0000-0000-0000-000000000001")]
    [InlineData("DELETE", "/api/cities/00000000-0000-0000-0000-000000000001")]
    [InlineData("PUT", "/api/cities/00000000-0000-0000-0000-000000000001")]
    [Trait("Rule", "RN-CD08")]
    public async Task Should_HaveNoWriteRoute_When_ResourceIsLocation(string method, string uri)
    {
        var client = await api.AdminAsync();
        var request = new HttpRequestMessage(new HttpMethod(method), uri) { Content = RawJson("{}") };

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
    }

    [Theory]
    [InlineData("1133334444", true)]
    [InlineData("11988887766", true)]
    [InlineData("119888877", false)]
    [InlineData("119888877660", false)]
    [Trait("Rule", "RN-CD09")]
    public async Task Should_CreateWithMainPhoneOrNothing_When_QuickCreating(string phone, bool valid)
    {
        var client = await api.AdminAsync();
        var name = UniqueName("Rapido");

        var response = await client.PostAsJsonAsync("/api/clients/quick", new { name, phone });

        var total = (await Json(await client.GetAsync($"/api/clients?filters={Filters(new { search = name })}"))).GetProperty("total").GetInt32();
        total.ShouldBe(valid ? 1 : 0);
        response.StatusCode.ShouldBe(valid ? HttpStatusCode.Created : HttpStatusCode.UnprocessableEntity);

        if (valid)
        {
            var id = (await Json(response)).GetProperty("id").GetString()!;
            var contact = (await Owned(client, "contacts", "Client", id)).Single();
            contact.GetProperty("isMain").GetBoolean().ShouldBeTrue();
            contact.GetProperty("value").GetString().ShouldBe(phone);
        }
    }

    // --- Ordenação traduzida para SQL (T-CD1, T-CD3)

    [Theory]
    [InlineData("/api/suppliers", "name")]
    [InlineData("/api/suppliers", "leadTimeDays")]
    [InlineData("/api/suppliers", "createdAt")]
    [InlineData("/api/suppliers", "campoQueNaoExiste")]
    [InlineData("/api/clients", "fullName")]
    [InlineData("/api/clients", "birthDate")]
    [InlineData("/api/clients", "createdAt")]
    public async Task Should_Return200_When_SortingList(string uri, string sort)
    {
        var client = await api.AdminAsync();

        (await client.GetAsync($"{uri}?sort={sort}&order=desc")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync($"{uri}?sort={sort}&order=asc")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Should_OrderByLeadTime_When_SortingSuppliers()
    {
        var client = await api.AdminAsync();
        var prefix = UniqueName("Ordem");
        await CreateSupplier(client, $"{prefix} a", leadTimeDays: 5);
        await CreateSupplier(client, $"{prefix} b", leadTimeDays: 90);

        var body = await Json(await client.GetAsync($"/api/suppliers?sort=leadTimeDays&order=desc&filters={Filters(new { search = prefix })}"));

        body.GetProperty("items").EnumerateArray().Select(s => s.GetProperty("leadTimeDays").GetInt32()).ShouldBe([90, 5]);
    }

    // --- Corpo inválido

    [Theory]
    [InlineData("/api/auth/login")]
    [InlineData("/api/clients")]
    [InlineData("/api/suppliers")]
    public async Task Should_Return400LikeBrokenJson_When_BodyIsInvalidUtf8(string uri)
    {
        var client = await api.AdminAsync();

        // {"name":"<0xC3 0x28>"}: 0xC3 abre uma sequência de dois bytes que 0x28 não completa.
        var response = await client.PostAsync(uri, RawBytes([.. "{\"name\":\""u8, 0xC3, 0x28, .. "\"}"u8]));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var raw = await response.Content.ReadAsStringAsync();
        raw.ShouldNotContain("StackTrace");
        JsonDocument.Parse(raw).RootElement.GetProperty("message").GetString().ShouldBe(InvalidBodyMessage);
    }

    [Theory]
    [InlineData("/api/clients", """{"name":"Ana","birthDate":"xx"}""", "birthDate")]
    [InlineData("/api/suppliers", """{"name":"Ana","cnpj":"11222333000181","leadTimeDays":"abc"}""", "leadTimeDays")]
    [InlineData("/api/contacts", """{"ownerType":"Nada","ownerId":"00000000-0000-0000-0000-000000000001","type":"Phone","value":"11988887766"}""", "ownerType")]
    [InlineData("/api/addresses", """{"ownerType":"Client","ownerId":"nao-e-guid","zipCode":"13010111","street":"A","number":"1","neighborhood":"B","cityId":"00000000-0000-0000-0000-000000000001"}""", "ownerId")]
    public async Task Should_Return422OnTheField_When_ValueCannotBeConverted(string uri, string json, string field)
    {
        var response = await (await api.AdminAsync()).PostAsync(uri, RawJson(json));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await Json(response)).GetProperty("fieldErrors").EnumerateObject().Select(f => f.Name).ShouldBe([field]);
    }
}
