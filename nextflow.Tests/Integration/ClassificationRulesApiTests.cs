using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Nextflow.Tests.Integration;

/// <summary>RN-CE23 e RN-CE38..40 no SQL real: o que falta além de <see cref="ClassificationApiTests"/>.</summary>
[Collection(AccessApiCollection.Name)]
public class ClassificationRulesApiTests(AccessApiFixture api)
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

    private static async Task<string> CreateCollection(HttpClient client, string? name = null)
    {
        var response = await client.PostAsJsonAsync("/api/collections", new { name = name ?? UniqueName("Colecao") });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return Id(await Json(response));
    }

    private static async Task<string[]> FieldsOf(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        return [.. (await Json(response)).GetProperty("fieldErrors").EnumerateObject().Select(f => f.Name)];
    }

    private static JsonElement? FindInTree(JsonElement tree, string id)
        => tree.EnumerateArray().Cast<JsonElement?>().FirstOrDefault(n => Id(n!.Value) == id);

    [Fact]
    [Trait("Rule", "RN-CE38")]
    public async Task Should_ApplyNameRuleOnlyAmongSiblings_When_SavingInSql()
    {
        using var admin = await api.AdminAsync();
        var firstParent = await CreateCategory(admin);
        var secondParent = await CreateCategory(admin);
        var name = UniqueName("Filha");
        var child = await CreateCategory(admin, firstParent, name);

        await CreateCategory(admin, secondParent, name);

        (await FieldsOf(await admin.PostAsJsonAsync("/api/categories", new { name = $"  {name.ToUpperInvariant()} ", parentId = firstParent }))).ShouldBe(["name"]);
        (await admin.PutAsJsonAsync($"/api/categories/{child}", new { name = name.ToUpperInvariant(), parentId = firstParent })).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Rule", "RN-CE38")]
    public async Task Should_Return422OnName_When_MovingChildToLevelOneWithRepeatedName()
    {
        using var admin = await api.AdminAsync();
        var name = UniqueName("Repetida");
        await CreateCategory(admin, name: name);
        var parent = await CreateCategory(admin);
        var child = await CreateCategory(admin, parent, name);

        (await FieldsOf(await admin.PutAsJsonAsync($"/api/categories/{child}", new { name, parentId = (string?)null }))).ShouldBe(["name"]);
    }

    [Fact]
    [Trait("Rule", "RN-CE23")]
    public async Task Should_MoveToLevelOne_When_ChildLosesItsParent()
    {
        using var admin = await api.AdminAsync();
        var parent = await CreateCategory(admin);
        var child = await CreateCategory(admin, parent);

        (await admin.PutAsJsonAsync($"/api/categories/{child}", new { name = UniqueName("Solta"), parentId = (string?)null })).StatusCode.ShouldBe(HttpStatusCode.OK);

        var tree = await Json(await admin.GetAsync("/api/categories/tree"));
        FindInTree(tree, child).ShouldNotBeNull();
        FindInTree(tree, parent)!.Value.GetProperty("children").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    [Trait("Rule", "RN-CE39")]
    public async Task Should_Return422OnParentId_When_ParentIsInactive()
    {
        using var admin = await api.AdminAsync();
        var parent = await CreateCategory(admin);
        (await admin.DeleteAsync($"/api/categories/{parent}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await FieldsOf(await admin.PostAsJsonAsync("/api/categories", new { name = UniqueName("Filha"), parentId = parent }))).ShouldBe(["parentId"]);
    }

    [Fact]
    [Trait("Rule", "RN-CE39")]
    public async Task Should_NotBecomeChild_When_ItsOnlyChildIsInactive()
    {
        using var admin = await api.AdminAsync();
        var category = await CreateCategory(admin);
        var child = await CreateCategory(admin, category);
        var other = await CreateCategory(admin);
        await admin.DeleteAsync($"/api/categories/{child}");

        var response = await admin.PutAsJsonAsync($"/api/categories/{category}", new { name = UniqueName("Mover"), parentId = other });

        (await FieldsOf(response)).ShouldBe(["parentId"]);
    }

    [Fact]
    [Trait("Rule", "RN-CE39")]
    public async Task Should_KeepInactiveInTreeAndOutOfOptions_When_CategoryIsInactivated()
    {
        using var admin = await api.AdminAsync();
        var name = UniqueName("Inativa");
        var id = await CreateCategory(admin, name: name);
        await admin.DeleteAsync($"/api/categories/{id}");

        var node = FindInTree(await Json(await admin.GetAsync("/api/categories/tree")), id)!.Value;
        node.GetProperty("isActive").GetBoolean().ShouldBeFalse();
        node.GetProperty("productCount").GetInt32().ShouldBe(0);
        (await Json(await admin.GetAsync($"/api/categories/options?search={name}"))).GetArrayLength().ShouldBe(0);
        (await admin.PutAsJsonAsync($"/api/categories/{id}", new { name = "Mudou" })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Rule", "RN-CE23")]
    public async Task Should_ReturnPathWithParent_When_ListingOptions()
    {
        using var admin = await api.AdminAsync();
        var parentName = UniqueName("Pai");
        var childName = UniqueName("Filha");
        var parent = await CreateCategory(admin, name: parentName);
        await CreateCategory(admin, parent, childName);

        var options = await Json(await admin.GetAsync($"/api/categories/options?search={childName}"));

        options[0].GetProperty("path").GetString().ShouldBe($"{parentName} > {childName}");
        options[0].GetProperty("parentId").GetString().ShouldBe(parent);
    }

    [Fact]
    [Trait("Rule", "RN-CE40")]
    public async Task Should_Return422OnEndsOn_When_EndIsBeforeStartOnPost()
    {
        using var admin = await api.AdminAsync();

        var response = await admin.PostAsJsonAsync("/api/collections", new { name = UniqueName("Datas"), startsOn = "2026-12-01", endsOn = "2026-11-30" });

        (await FieldsOf(response)).ShouldBe(["endsOn"]);
    }

    [Fact]
    [Trait("Rule", "RN-CE40")]
    public async Task Should_Return422OnField_When_DateCannotBeConverted()
    {
        using var admin = await api.AdminAsync();
        var json = $$"""{"name":"{{UniqueName("Datas")}}","startsOn":"xx"}""";

        var response = await admin.PostAsync("/api/collections", new StringContent(json, Encoding.UTF8, "application/json"));

        (await FieldsOf(response)).ShouldBe(["startsOn"]);
    }

    [Fact]
    [Trait("Rule", "RN-CE40")]
    public async Task Should_SoftDeleteFilterAndKeepNameTaken_When_CollectionIsInactive()
    {
        using var admin = await api.AdminAsync();
        var name = UniqueName("Colecao");
        var id = await CreateCollection(admin, name);

        (await admin.DeleteAsync($"/api/collections/{id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        async Task<int> Total(object filter)
            => (await Json(await admin.GetAsync($"/api/collections?filters={Filters(filter)}"))).GetProperty("total").GetInt32();

        (await Total(new { search = name.ToUpperInvariant() })).ShouldBe(1);
        (await Total(new { search = name, isActive = "false" })).ShouldBe(1);
        (await Total(new { search = name, isActive = "true" })).ShouldBe(0);
        (await Json(await admin.GetAsync($"/api/collections/options?search={name}"))).GetArrayLength().ShouldBe(0);
        (await FieldsOf(await admin.PostAsJsonAsync("/api/collections", new { name }))).ShouldBe(["name"]);
        (await admin.PutAsJsonAsync($"/api/collections/{id}", new { name = "Mudou" })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await admin.PatchAsync($"/api/collections/{id}/reactivate", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await Json(await admin.GetAsync($"/api/collections/options?search={name}"))).GetArrayLength().ShouldBe(1);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("startsOn")]
    [InlineData("endsOn")]
    public async Task Should_Return200_When_SortingCollections(string sort)
    {
        using var admin = await api.AdminAsync();

        (await admin.GetAsync($"/api/collections?sort={sort}&order=desc")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
