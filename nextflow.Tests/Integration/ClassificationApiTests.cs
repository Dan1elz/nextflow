using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Nextflow.Tests.Integration;

/// <summary>RN-CE23, RN-CE38..40 no que os testes de use case não enxergam: SQL real, rotas e envelope de erro.</summary>
[Collection(AccessApiCollection.Name)]
public class ClassificationApiTests(AccessApiFixture api)
{
    private static async Task<JsonElement> Json(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<JsonElement>();

    private static string UniqueName(string prefix) => $"{prefix} {Guid.NewGuid():N}"[..30];

    private static string Filters(object filters) => Uri.EscapeDataString(JsonSerializer.Serialize(filters));

    private static string Id(JsonElement element) => element.GetProperty("id").GetString()!;

    private static async Task<string> CreateCategory(HttpClient client, string? parentId = null, string? name = null)
    {
        var response = await client.PostAsJsonAsync("/api/categories", new { name = name ?? UniqueName("Categoria"), parentId });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return Id(await Json(response));
    }

    [Theory]
    [InlineData("/api/categories/tree")]
    [InlineData("/api/categories/options")]
    [InlineData("/api/collections")]
    [InlineData("/api/collections/options")]
    public async Task Should_Return401_When_CallingWithoutToken(string uri)
        => (await api.Anonymous().GetAsync(uri)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

    [Fact]
    [Trait("Rule", "RN-CE23")]
    public async Task Should_Return422OnParentId_When_PostingGrandchild()
    {
        using var admin = await api.AdminAsync();
        var root = await CreateCategory(admin);
        var child = await CreateCategory(admin, root);

        var response = await admin.PostAsJsonAsync("/api/categories", new { name = UniqueName("Neta"), parentId = child });

        response.StatusCode.ShouldBe((HttpStatusCode)422);
        var errors = (await Json(response)).GetProperty("fieldErrors").GetProperty("parentId");
        errors[0].GetString().ShouldBe("Uma subcategoria não pode ter subcategorias: escolha uma categoria de nível 1.");
    }

    [Fact]
    [Trait("Rule", "RN-CE23")]
    public async Task Should_Return422OnParentId_When_ParentWithChildrenBecomesChild()
    {
        using var admin = await api.AdminAsync();
        var name = UniqueName("Pai");
        var root = await CreateCategory(admin, name: name);
        await CreateCategory(admin, root);
        var other = await CreateCategory(admin);

        var response = await admin.PutAsJsonAsync($"/api/categories/{root}", new { name, parentId = other });

        response.StatusCode.ShouldBe((HttpStatusCode)422);
        (await Json(response)).GetProperty("fieldErrors").TryGetProperty("parentId", out _).ShouldBeTrue();
    }

    [Fact]
    [Trait("Rule", "RN-CE23")]
    public async Task Should_ReturnChildrenUnderParent_When_ReadingTree()
    {
        using var admin = await api.AdminAsync();
        var root = await CreateCategory(admin);
        var second = await CreateCategory(admin, root, "B filha");
        var first = await CreateCategory(admin, root, "A filha");

        var tree = await Json(await admin.GetAsync("/api/categories/tree"));

        var node = tree.EnumerateArray().Single(n => Id(n) == root);
        node.GetProperty("productCount").GetInt32().ShouldBe(0);
        node.GetProperty("children").EnumerateArray().Select(Id).ShouldBe([first, second]);
        tree.EnumerateArray().ShouldNotContain(n => Id(n) == first);
    }

    [Fact]
    [Trait("Rule", "RN-CE23")]
    public async Task Should_ListOnlyLevelOne_When_OptionsFilterRootsOnly()
    {
        using var admin = await api.AdminAsync();
        var marker = Guid.NewGuid().ToString("N")[..12];
        var root = await CreateCategory(admin, name: $"Pai {marker}");
        var child = await CreateCategory(admin, root, $"Filha {marker}");

        var all = await Json(await admin.GetAsync($"/api/categories/options?search={marker}"));
        var roots = await Json(await admin.GetAsync($"/api/categories/options?search={marker}&filters={Filters(new { rootsOnly = "true" })}"));

        all.EnumerateArray().Select(Id).ShouldBe([root, child]);
        all[1].GetProperty("path").GetString().ShouldBe($"Pai {marker} > Filha {marker}");
        roots.EnumerateArray().Select(Id).ShouldBe([root]);
    }

    [Fact]
    [Trait("Rule", "RN-CE38")]
    public async Task Should_Return422OnName_When_LevelOneNameRepeatsInSql()
    {
        using var admin = await api.AdminAsync();
        var name = UniqueName("Nível um");
        await CreateCategory(admin, name: name);

        var response = await admin.PostAsJsonAsync("/api/categories", new { name = name.ToUpperInvariant() });

        response.StatusCode.ShouldBe((HttpStatusCode)422);
        (await Json(response)).GetProperty("fieldErrors").TryGetProperty("name", out _).ShouldBeTrue();
    }

    [Fact]
    [Trait("Rule", "RN-CE39")]
    public async Task Should_FollowActiveParentRule_When_InactivatingAndReactivating()
    {
        using var admin = await api.AdminAsync();
        var root = await CreateCategory(admin);
        var child = await CreateCategory(admin, root);

        (await admin.DeleteAsync($"/api/categories/{root}")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await admin.DeleteAsync($"/api/categories/{child}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await admin.DeleteAsync($"/api/categories/{root}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await admin.PatchAsync($"/api/categories/{child}/reactivate", null)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await admin.PatchAsync($"/api/categories/{root}/reactivate", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await admin.PatchAsync($"/api/categories/{child}/reactivate", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    [Trait("Rule", "RN-CE40")]
    public async Task Should_RunCollectionCrud_When_DatesAreValid()
    {
        using var admin = await api.AdminAsync();
        var name = UniqueName("Verão");

        var created = await admin.PostAsJsonAsync("/api/collections", new { name, startsOn = "2026-12-01", endsOn = "2027-03-01" });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = Id(await Json(created));

        var page = await Json(await admin.GetAsync($"/api/collections?filters={Filters(new { search = name })}"));
        var item = page.GetProperty("items")[0];
        item.GetProperty("startsOn").GetString().ShouldBe("2026-12-01");
        item.GetProperty("endsOn").GetString().ShouldBe("2027-03-01");
        item.GetProperty("productCount").GetInt32().ShouldBe(0);

        var invalid = await admin.PutAsJsonAsync($"/api/collections/{id}", new { name, startsOn = "2026-12-01", endsOn = "2026-11-30" });
        invalid.StatusCode.ShouldBe((HttpStatusCode)422);
        (await Json(invalid)).GetProperty("fieldErrors").TryGetProperty("endsOn", out _).ShouldBeTrue();

        (await admin.DeleteAsync($"/api/collections/{id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var options = await Json(await admin.GetAsync($"/api/collections/options?search={Uri.EscapeDataString(name)}"));
        options.GetArrayLength().ShouldBe(0);
    }
}
