using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Nextflow.Tests.Integration;

/// <summary>RN-CE41 a RN-CE47 no que os testes de use case não enxergam: SQL real, seed no startup, rotas e envelope de erro.</summary>
[Collection(AccessApiCollection.Name)]
public class CalendarEventsApiTests(AccessApiFixture api)
{
    private const string Route = "/api/calendar-events";

    private static async Task<JsonElement> Json(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<JsonElement>();

    private static string UniqueName(string prefix) => $"{prefix} {Guid.NewGuid():N}"[..30];

    private static string Filters(object filters) => Uri.EscapeDataString(JsonSerializer.Serialize(filters));

    private static async Task<JsonElement> National(HttpClient client, string name)
    {
        var page = await Json(await client.GetAsync($"{Route}?limit=100&filters={Filters(new { search = name, scope = "National" })}"));
        return page.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("name").GetString() == name);
    }

    private static async Task<string> CreateFixed(HttpClient client, string name, int month = 6, int day = 24)
    {
        var response = await client.PostAsJsonAsync(Route, new { name, dateRule = "Fixed", month, day, leadDays = 7 });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await Json(response)).GetProperty("id").GetString()!;
    }

    private static List<string> Names(JsonElement page)
        => [.. page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("name").GetString()!)];

    private static async Task ShouldFailOn(HttpResponseMessage response, string field)
    {
        response.StatusCode.ShouldBe((HttpStatusCode)422);
        (await Json(response)).GetProperty("fieldErrors").TryGetProperty(field, out _).ShouldBeTrue(field);
    }

    [Fact]
    public async Task Should_Return401_When_CallingWithoutToken()
        => (await api.Anonymous().GetAsync(Route)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

    [Fact]
    [Trait("Rule", "RN-CE47")]
    public async Task Should_ListSeededNationalEvents_When_FilteringByScope()
    {
        using var admin = await api.AdminAsync();

        var page = await Json(await admin.GetAsync($"{Route}?limit=100&filters={Filters(new { scope = "National" })}"));

        page.GetProperty("total").GetInt32().ShouldBe(7);
        var mothersDay = await National(admin, "Dia das Mães");
        mothersDay.GetProperty("dateRule").GetString().ShouldBe("NthWeekday");
        mothersDay.GetProperty("nthWeekday").GetProperty("nth").GetInt32().ShouldBe(2);
        mothersDay.GetProperty("productCount").GetInt32().ShouldBe(0);
        mothersDay.GetProperty("nextDate").GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    [Trait("Rule", "RN-CE22")]
    public async Task Should_PageInNameOrder_When_ListingWithOffsetAndLimit()
    {
        using var admin = await api.AdminAsync();

        var first = await Json(await admin.GetAsync($"{Route}?offset=0&limit=3&filters={Filters(new { scope = "National" })}"));
        var second = await Json(await admin.GetAsync($"{Route}?offset=3&limit=3&filters={Filters(new { scope = "National" })}"));

        first.GetProperty("total").GetInt32().ShouldBe(7);
        Names(first).ShouldBe(["Black Friday", "Dia das Crianças", "Dia das Mães"]);
        Names(second).ShouldBe(["Dia dos Namorados", "Dia dos Pais", "Natal"]);
    }

    [Fact]
    [Trait("Rule", "RN-CE22")]
    public async Task Should_FindByNameIgnoringCase_When_SearchingWithAccentedUppercase()
    {
        using var admin = await api.AdminAsync();

        var page = await Json(await admin.GetAsync($"{Route}?filters={Filters(new { search = "MÃES" })}"));

        Names(page).ShouldBe(["Dia das Mães"]);
    }

    [Fact]
    [Trait("Rule", "RN-CE43")]
    public async Task Should_FilterByStatusAndScope_When_CombiningFilters()
    {
        using var admin = await api.AdminAsync();
        var name = UniqueName("Status");
        var id = await CreateFixed(admin, name);
        (await admin.DeleteAsync($"{Route}/{id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        async Task<List<string>> List(object filters)
            => Names(await Json(await admin.GetAsync($"{Route}?limit=100&filters={Filters(filters)}")));

        (await List(new { search = name, isActive = "false", scope = "Company" })).ShouldBe([name]);
        (await List(new { search = name, isActive = "true" })).ShouldBeEmpty();
        (await List(new { search = name, scope = "National" })).ShouldBeEmpty();
        (await List(new { isActive = "false", scope = "National" })).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Rule", "RN-CE42")]
    [Trait("Rule", "RN-CE41")]
    public async Task Should_CreateCompanyEvent_When_BodyTriesToSetScope()
    {
        using var admin = await api.AdminAsync();

        var response = await admin.PostAsJsonAsync(Route, new
        {
            name = UniqueName("Carnaval"),
            scope = "National",
            dateRule = "EasterOffset",
            easterOffsetDays = -47,
            month = 2,
            leadDays = 10,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await Json(response);
        body.GetProperty("scope").GetString().ShouldBe("Company");
        body.GetProperty("easterOffsetDays").GetInt32().ShouldBe(-47);
        body.GetProperty("month").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    [Trait("Rule", "RN-CE41")]
    public async Task Should_Return422WithNestedFieldError_When_NthIsOutOfRange()
    {
        using var admin = await api.AdminAsync();

        var response = await admin.PostAsJsonAsync(Route, new
        {
            name = UniqueName("Evento"),
            dateRule = "NthWeekday",
            month = 5,
            nthWeekday = new { weekday = 0, nth = 5 },
            leadDays = 10,
        });

        response.StatusCode.ShouldBe((HttpStatusCode)422);
        (await Json(response)).GetProperty("fieldErrors").TryGetProperty("nthWeekday.nth", out _).ShouldBeTrue();
    }

    /// <summary>A chave em <c>fieldErrors</c> é o que o formulário usa para marcar o campo; o nome no C# é outro.</summary>
    public static TheoryData<string, string> InvalidBodies => new()
    {
        { """{"name":"","dateRule":"Easter","leadDays":1}""", "name" },
        { $$"""{"name":"{{new string('a', 101)}}","dateRule":"Easter","leadDays":1}""", "name" },
        { """{"name":"X","leadDays":1}""", "dateRule" },
        { """{"name":"X","dateRule":"Lunar","leadDays":1}""", "dateRule" },
        { """{"name":"X","dateRule":"Easter"}""", "leadDays" },
        { """{"name":"X","dateRule":"Easter","leadDays":121}""", "leadDays" },
        { """{"name":"X","dateRule":"Fixed","day":10,"leadDays":1}""", "month" },
        { """{"name":"X","dateRule":"Fixed","month":13,"day":10,"leadDays":1}""", "month" },
        { """{"name":"X","dateRule":"Fixed","month":6,"leadDays":1}""", "day" },
        { """{"name":"X","dateRule":"Fixed","month":4,"day":31,"leadDays":1}""", "day" },
        { """{"name":"X","dateRule":"Fixed","month":2,"day":29,"leadDays":1}""", "day" },
        { """{"name":"X","dateRule":"NthWeekday","nthWeekday":{"weekday":0,"nth":1},"leadDays":1}""", "month" },
        { """{"name":"X","dateRule":"NthWeekday","month":5,"leadDays":1}""", "nthWeekday" },
        { """{"name":"X","dateRule":"NthWeekday","month":5,"nthWeekday":{"weekday":7,"nth":1},"leadDays":1}""", "nthWeekday.weekday" },
        { """{"name":"X","dateRule":"NthWeekday","month":5,"nthWeekday":{"nth":1},"leadDays":1}""", "nthWeekday.weekday" },
        { """{"name":"X","dateRule":"NthWeekday","month":5,"nthWeekday":{"weekday":0},"leadDays":1}""", "nthWeekday.nth" },
        { """{"name":"X","dateRule":"EasterOffset","leadDays":1}""", "easterOffsetDays" },
        { """{"name":"X","dateRule":"EasterOffset","easterOffsetDays":61,"leadDays":1}""", "easterOffsetDays" },
        { """{"name":"X","dateRule":"EasterOffset","easterOffsetDays":-61,"leadDays":1}""", "easterOffsetDays" },
    };

    [Theory]
    [MemberData(nameof(InvalidBodies))]
    [Trait("Rule", "RN-CE41")]
    [Trait("Rule", "RN-CE45")]
    public async Task Should_Return422OnFieldKey_When_BodyIsInvalid(string body, string field)
    {
        using var admin = await api.AdminAsync();

        var response = await admin.PostAsync(Route, new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        await ShouldFailOn(response, field);
    }

    [Fact]
    [Trait("Rule", "RN-CE41")]
    public async Task Should_ExplainNoLeapDay_When_PostingFebruary29()
    {
        using var admin = await api.AdminAsync();

        var response = await admin.PostAsJsonAsync(Route, new { name = UniqueName("Bissexto"), dateRule = "Fixed", month = 2, day = 29, leadDays = 1 });

        response.StatusCode.ShouldBe((HttpStatusCode)422);
        (await Json(response)).GetProperty("fieldErrors").GetProperty("day")[0].GetString()!.ShouldContain("29 de fevereiro");
    }

    [Fact]
    [Trait("Rule", "RN-CE41")]
    public async Task Should_StoreNullOutsideTheRule_When_CompanyEventChangesRule()
    {
        using var admin = await api.AdminAsync();
        var name = UniqueName("Troca");
        var id = await CreateFixed(admin, name);

        var response = await admin.PutAsJsonAsync($"{Route}/{id}", new { name, dateRule = "BlackFriday", month = 6, day = 24, leadDays = 9 });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var stored = await Json(await admin.GetAsync($"{Route}/{id}"));
        stored.GetProperty("dateRule").GetString().ShouldBe("BlackFriday");
        stored.GetProperty("month").ValueKind.ShouldBe(JsonValueKind.Null);
        stored.GetProperty("day").ValueKind.ShouldBe(JsonValueKind.Null);
        stored.GetProperty("leadDays").GetInt32().ShouldBe(9);
    }

    [Fact]
    [Trait("Rule", "RN-CE44")]
    public async Task Should_Return422OnName_When_NameMatchesNationalEventInOtherCase()
    {
        using var admin = await api.AdminAsync();

        var response = await admin.PostAsJsonAsync(Route, new { name = "BLACK FRIDAY", dateRule = "Easter", leadDays = 1 });

        response.StatusCode.ShouldBe((HttpStatusCode)422);
        (await Json(response)).GetProperty("fieldErrors").TryGetProperty("name", out _).ShouldBeTrue();
    }

    [Theory]
    [InlineData("PÁSCOA")]
    [InlineData("  dia das MÃES ")]
    [Trait("Rule", "RN-CE44")]
    public async Task Should_Return422OnName_When_AccentedNameDiffersOnlyInCase(string name)
    {
        using var admin = await api.AdminAsync();

        var response = await admin.PostAsJsonAsync(Route, new { name, dateRule = "BlackFriday", leadDays = 1 });

        await ShouldFailOn(response, "name");
    }

    [Fact]
    [Trait("Rule", "RN-CE44")]
    public async Task Should_Return422OnName_When_NameBelongsToInactiveEvent()
    {
        using var admin = await api.AdminAsync();
        var name = UniqueName("Inativo");
        var id = await CreateFixed(admin, name);
        (await admin.DeleteAsync($"{Route}/{id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var response = await admin.PostAsJsonAsync(Route, new { name = name.ToUpperInvariant(), dateRule = "Easter", leadDays = 1 });

        await ShouldFailOn(response, "name");
    }

    [Fact]
    [Trait("Rule", "RN-CE42")]
    public async Task Should_LockNameButAcceptLeadDays_When_UpdatingNationalEvent()
    {
        using var admin = await api.AdminAsync();
        var easter = await National(admin, "Páscoa");
        var id = easter.GetProperty("id").GetString();
        var leadDays = easter.GetProperty("leadDays").GetInt32();

        var renamed = await admin.PutAsJsonAsync($"{Route}/{id}", new { name = "Páscoa 2", dateRule = "Easter", leadDays });
        renamed.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var updated = await admin.PutAsJsonAsync($"{Route}/{id}", new { name = "Páscoa", dateRule = "Easter", leadDays });
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Rule", "RN-CE42")]
    public async Task Should_PersistOnlyLeadDays_When_NationalKeepsNthWeekdayDefinition()
    {
        using var admin = await api.AdminAsync();
        var fathersDay = await National(admin, "Dia dos Pais");
        var id = fathersDay.GetProperty("id").GetString();
        var original = fathersDay.GetProperty("leadDays").GetInt32();
        var definition = new { weekday = 0, nth = 2 };

        var updated = await admin.PutAsJsonAsync($"{Route}/{id}", new { name = "Dia dos Pais", dateRule = "NthWeekday", month = 8, nthWeekday = definition, leadDays = original + 1 });

        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        var stored = await Json(await admin.GetAsync($"{Route}/{id}"));
        stored.GetProperty("leadDays").GetInt32().ShouldBe(original + 1);
        stored.GetProperty("scope").GetString().ShouldBe("National");
        stored.GetProperty("month").GetInt32().ShouldBe(8);

        (await admin.PutAsJsonAsync($"{Route}/{id}", new { name = "Dia dos Pais", dateRule = "NthWeekday", month = 8, nthWeekday = definition, leadDays = original }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Rule", "RN-CE42")]
    public async Task Should_Return400AndKeepEvent_When_NationalChangesRuleFields()
    {
        using var admin = await api.AdminAsync();
        var christmas = await National(admin, "Natal");
        var id = christmas.GetProperty("id").GetString();
        var leadDays = christmas.GetProperty("leadDays").GetInt32();

        (await admin.PutAsJsonAsync($"{Route}/{id}", new { name = "Natal", dateRule = "Easter", leadDays = leadDays + 1 })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await admin.PutAsJsonAsync($"{Route}/{id}", new { name = "Natal", dateRule = "Fixed", month = 12, day = 24, leadDays = leadDays + 1 })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await admin.PutAsJsonAsync($"{Route}/{id}", new { name = "NATAL", dateRule = "Fixed", month = 12, day = 25, leadDays = leadDays + 1 })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var stored = await Json(await admin.GetAsync($"{Route}/{id}"));
        stored.GetProperty("day").GetInt32().ShouldBe(25);
        stored.GetProperty("leadDays").GetInt32().ShouldBe(leadDays);
    }

    [Fact]
    [Trait("Rule", "RN-CE42")]
    [Trait("Rule", "RN-CE44")]
    public async Task Should_EditEverything_When_UpdatingCompanyEvent()
    {
        using var admin = await api.AdminAsync();
        var name = UniqueName("Empresa");
        var id = await CreateFixed(admin, name);
        var renamed = UniqueName("Renomeado");

        var sameNameInOtherCase = await admin.PutAsJsonAsync($"{Route}/{id}", new { name = name.ToUpperInvariant(), dateRule = "Fixed", month = 7, day = 1, leadDays = 8 });
        sameNameInOtherCase.StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await admin.PutAsJsonAsync($"{Route}/{id}", new { name = renamed, scope = "National", dateRule = "NthWeekday", month = 9, nthWeekday = new { weekday = 1, nth = 3 }, leadDays = 8 });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await Json(response);
        body.GetProperty("name").GetString().ShouldBe(renamed);
        body.GetProperty("scope").GetString().ShouldBe("Company");
        body.GetProperty("day").ValueKind.ShouldBe(JsonValueKind.Null);
        body.GetProperty("nthWeekday").GetProperty("nth").GetInt32().ShouldBe(3);
    }

    [Fact]
    [Trait("Rule", "RN-CE44")]
    public async Task Should_Return422OnName_When_RenamingCompanyEventToNationalName()
    {
        using var admin = await api.AdminAsync();
        var id = await CreateFixed(admin, UniqueName("Renomear"));

        var response = await admin.PutAsJsonAsync($"{Route}/{id}", new { name = "natal", dateRule = "Easter", leadDays = 1 });

        await ShouldFailOn(response, "name");
    }

    [Fact]
    [Trait("Rule", "RN-CE43")]
    public async Task Should_LeaveOptionsAndReturn_When_InactivatingAndReactivating()
    {
        using var admin = await api.AdminAsync();
        var name = UniqueName("Festa");
        var created = await Json(await admin.PostAsJsonAsync(Route, new { name, dateRule = "Fixed", month = 6, day = 24, leadDays = 7 }));
        var id = created.GetProperty("id").GetString();

        async Task<int> OptionCount() => (await Json(await admin.GetAsync($"{Route}/options?search={Uri.EscapeDataString(name)}"))).GetArrayLength();

        (await OptionCount()).ShouldBe(1);

        (await admin.DeleteAsync($"{Route}/{id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await OptionCount()).ShouldBe(0);
        (await Json(await admin.GetAsync($"{Route}/{id}"))).GetProperty("isActive").GetBoolean().ShouldBeFalse();

        (await admin.PatchAsync($"{Route}/{id}/reactivate", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await OptionCount()).ShouldBe(1);
    }

    [Fact]
    [Trait("Rule", "RN-CE43")]
    public async Task Should_InactivateAndReactivate_When_EventIsNational()
    {
        using var admin = await api.AdminAsync();
        var id = (await National(admin, "Dia dos Namorados")).GetProperty("id").GetString();

        (await admin.DeleteAsync($"{Route}/{id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var options = await Json(await admin.GetAsync($"{Route}/options?search=namorados"));
        options.GetArrayLength().ShouldBe(0);

        (await admin.PatchAsync($"{Route}/{id}/reactivate", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Json(await admin.GetAsync($"{Route}/{id}"))).GetProperty("isActive").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    [Trait("Rule", "RN-CE43")]
    public async Task Should_Return400_When_EditingInactiveOrReactivatingActive()
    {
        using var admin = await api.AdminAsync();
        var name = UniqueName("Bloqueio");
        var id = await CreateFixed(admin, name);

        (await admin.PatchAsync($"{Route}/{id}/reactivate", null)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await admin.DeleteAsync($"{Route}/{id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await admin.PutAsJsonAsync($"{Route}/{id}", new { name, dateRule = "Easter", leadDays = 2 })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_Return404_When_EventDoesNotExist()
    {
        using var admin = await api.AdminAsync();
        var id = Guid.NewGuid();

        (await admin.GetAsync($"{Route}/{id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await admin.PutAsJsonAsync($"{Route}/{id}", new { name = "X", dateRule = "Easter", leadDays = 1 })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await admin.DeleteAsync($"{Route}/{id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await admin.PatchAsync($"{Route}/{id}/reactivate", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("2026-10-31", "Natal", "2026-12-25")]
    [InlineData("2026-10-31", "Páscoa", "2027-03-28")]
    [InlineData("2026-10-31", "Dia das Crianças", "2027-10-12")]
    [InlineData("2026-10-31", "Black Friday", "2026-11-27")]
    [InlineData("2026-12-25", "Natal", "2026-12-25")] // hoje ainda conta
    [InlineData("2026-12-26", "Natal", "2027-12-25")]
    [InlineData("2026-12-31", "Páscoa", "2027-03-28")] // virada de ano
    [InlineData("2027-01-01", "Natal", "2027-12-25")]
    [Trait("Rule", "RN-CE46")]
    public async Task Should_ReturnNextDateFromBusinessClock_When_ReadingEvent(string today, string name, string expected)
    {
        await using var host = api.HostWithClock(DateOnly.Parse(today));
        using var admin = await api.AdminAsync(host);

        (await National(admin, name)).GetProperty("nextDate").GetString().ShouldBe(expected);
    }

    /// <summary>A data fixa fica no passado: com o relógio real, o evento de 20/02 só cairia em 2027 ou depois.</summary>
    [Fact]
    [Trait("Rule", "RN-CE46")]
    public async Task Should_UseBusinessClock_When_PostingPuttingAndGettingById()
    {
        await using var host = api.HostWithClock(new DateOnly(2026, 2, 15));
        using var admin = await api.AdminAsync(host);
        var name = UniqueName("Relógio");

        var created = await admin.PostAsJsonAsync(Route, new { name, dateRule = "Fixed", month = 2, day = 20, leadDays = 1 });
        var body = await Json(created);
        body.GetProperty("nextDate").GetString().ShouldBe("2026-02-20");
        var id = body.GetProperty("id").GetString();

        var updated = await admin.PutAsJsonAsync($"{Route}/{id}", new { name, dateRule = "Fixed", month = 2, day = 21, leadDays = 1 });
        (await Json(updated)).GetProperty("nextDate").GetString().ShouldBe("2026-02-21");

        (await Json(await admin.GetAsync($"{Route}/{id}"))).GetProperty("nextDate").GetString().ShouldBe("2026-02-21");
    }

    [Fact]
    [Trait("Rule", "RN-CE47")]
    public async Task Should_KeepLeadDaysAndStatus_When_SeedRunsAgainAtStartup()
    {
        using var admin = await api.AdminAsync();
        var mothersDay = await National(admin, "Dia das Mães");
        var id = mothersDay.GetProperty("id").GetString();
        var original = mothersDay.GetProperty("leadDays").GetInt32();
        object Body(int leadDays) => new { name = "Dia das Mães", dateRule = "NthWeekday", month = 5, nthWeekday = new { weekday = 0, nth = 2 }, leadDays };

        (await admin.PutAsJsonAsync($"{Route}/{id}", Body(original + 9))).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await admin.DeleteAsync($"{Route}/{id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        try
        {
            await using var restarted = api.HostWithClock(new DateOnly(2026, 10, 31));
            using var client = await api.AdminAsync(restarted);

            var page = await Json(await client.GetAsync($"{Route}?limit=100&filters={Filters(new { scope = "National" })}"));
            page.GetProperty("total").GetInt32().ShouldBe(7);
            var stored = await Json(await client.GetAsync($"{Route}/{id}"));
            stored.GetProperty("leadDays").GetInt32().ShouldBe(original + 9);
            stored.GetProperty("isActive").GetBoolean().ShouldBeFalse();
        }
        finally
        {
            (await admin.PatchAsync($"{Route}/{id}/reactivate", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
            (await admin.PutAsJsonAsync($"{Route}/{id}", Body(original))).StatusCode.ShouldBe(HttpStatusCode.OK);
        }
    }
}
