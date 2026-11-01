using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Models;

namespace Nextflow.Tests.Integration;

/// <summary>
/// F1.3 no que os testes de use case não enxergam: SQL real (índices únicos, cascatas, change tracker), filtros traduzidos
/// para o Postgres, rotas, envelope de erro e multipart. Storage em memória, sem Silo.
/// </summary>
[Collection(AccessApiCollection.Name)]
public class ProductsApiTests(AccessApiFixture api)
{
    private const string Route = "/api/products";
    private static int _sequence;

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 1, 2, 3, 4];

    private static string Unique(string prefix) => $"{prefix}-{Interlocked.Increment(ref _sequence)}-{Guid.NewGuid():N}"[..24];

    private static string Filters(object filters) => Uri.EscapeDataString(JsonSerializer.Serialize(filters));

    private static async Task<JsonElement> Json(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<JsonElement>();

    private static object SimpleBody(string sku, string? barcode = null, string name = "Caderno", object? categoryIds = null, string? collectionId = null, string productType = "Standard") => new
    {
        name,
        productType,
        saleUnit = "Unit",
        decimalPlaces = 0,
        categoryIds = categoryIds ?? Array.Empty<string>(),
        collectionId,
        variants = new[] { new { sku, barcode, price = 12.5m, isActive = true } },
    };

    private static object ApparelBody(string prefix, string name = "Camiseta") => new
    {
        name,
        productType = "Apparel",
        saleUnit = "Unit",
        decimalPlaces = 0,
        attributes = new[]
        {
            new
            {
                name = "Tamanho",
                values = new[]
                {
                    new { tempKey = "p", value = "P" },
                    new { tempKey = "m", value = "M" },
                    new { tempKey = "g", value = "G" },
                },
            },
        },
        variants = new[]
        {
            new { attributeValueIds = new[] { "p" }, sku = $"{prefix}-P", price = 30m, isActive = true },
            new { attributeValueIds = new[] { "m" }, sku = $"{prefix}-M", price = 30m, isActive = true },
            new { attributeValueIds = new[] { "g" }, sku = $"{prefix}-G", price = 35m, isActive = true },
        },
    };

    private static async Task<string> Create(HttpClient client, object body)
    {
        var response = await client.PostAsJsonAsync(Route, body);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await Json(response)).GetProperty("id").GetString()!;
    }

    private static async Task<JsonElement> Form(HttpClient client, string id)
        => await Json(await client.GetAsync($"{Route}/{id}/form"));

    private static async Task<List<string>> VariantLabels(HttpClient client, string id)
    {
        var variants = await Json(await client.GetAsync($"{Route}/{id}/variants"));
        return [.. variants.EnumerateArray().Select(v => v.GetProperty("label").GetString()!)];
    }

    private static async Task ShouldFailOn(HttpResponseMessage response, string field)
    {
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe((HttpStatusCode)422, text);
        (await Json(response)).GetProperty("fieldErrors").TryGetProperty(field, out _).ShouldBeTrue(text);
    }

    /// <summary>Monta o corpo do PUT a partir do formulário, como a tela faz, sem as opções.</summary>
    private static Dictionary<string, object?> EditBody(JsonElement form)
    {
        var body = JsonSerializer.Deserialize<Dictionary<string, object?>>(form.GetRawText())!;
        body.Remove("options");
        body.Remove("imageUrl");
        return body;
    }

    private static async Task<string> CreateCategory(HttpClient client, string? parentId = null)
    {
        var response = await client.PostAsJsonAsync("/api/categories", new { name = Unique("Cat"), parentId });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await Json(response)).GetProperty("id").GetString()!;
    }

    private static async Task<string> CreateCollection(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/collections", new { name = Unique("Col") });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await Json(response)).GetProperty("id").GetString()!;
    }

    private static List<string> Names(JsonElement page)
        => [.. page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("name").GetString()!)];

    [Fact]
    public async Task Should_Return401_When_CallingWithoutToken()
        => (await api.Anonymous().GetAsync(Route)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

    // ---------- Pronto quando ----------

    [Fact]
    [Trait("Rule", "RN-CE48")]
    public async Task Should_PersistOneHiddenVariant_When_ProductHasNoGrid()
    {
        using var admin = await api.AdminAsync();
        var sku = Unique("SKU");

        var id = await Create(admin, SimpleBody(sku));

        var detail = await Json(await admin.GetAsync($"{Route}/{id}"));
        detail.GetProperty("hasVariants").GetBoolean().ShouldBeFalse();
        detail.GetProperty("variantCount").GetInt32().ShouldBe(1);
        (await VariantLabels(admin, id)).ShouldBe([""]);

        await using var context = api.CreateContext();
        var variant = await context.Set<ProductVariant>().Include(v => v.AttributeValues).SingleAsync(v => v.ProductId == Guid.Parse(id));
        variant.Sku.ShouldBe(sku);
        variant.Price.ShouldBe(12.5m);
        variant.AttributeValues.ShouldBeEmpty();
        (await context.Set<ProductAttribute>().CountAsync(a => a.ProductId == Guid.Parse(id))).ShouldBe(0);
    }

    [Fact]
    [Trait("Rule", "RN-CE50")]
    public async Task Should_GeneratePMG_When_ApparelSavedWithTempKeys()
    {
        using var admin = await api.AdminAsync();

        var id = await Create(admin, ApparelBody(Unique("CAM")));

        var detail = await Json(await admin.GetAsync($"{Route}/{id}"));
        detail.GetProperty("productType").GetString().ShouldBe("Apparel");
        detail.GetProperty("hasVariants").GetBoolean().ShouldBeTrue();
        detail.GetProperty("variantCount").GetInt32().ShouldBe(3);
        detail.GetProperty("minEffectivePrice").GetDecimal().ShouldBe(30m);
        detail.GetProperty("maxEffectivePrice").GetDecimal().ShouldBe(35m);
        (await VariantLabels(admin, id)).ShouldBe(["P", "M", "G"]);

        await using var context = api.CreateContext();
        var links = await context.Set<ProductVariantValue>().CountAsync(v => v.Variant.ProductId == Guid.Parse(id));
        links.ShouldBe(3);
    }

    [Fact]
    [Trait("Rule", "RN-CE49")]
    public async Task Should_LabelInGridOrder_When_TwoAttributes()
    {
        using var admin = await api.AdminAsync();
        var prefix = Unique("CAM");

        var id = await Create(admin, new
        {
            name = "Camiseta",
            productType = "Apparel",
            saleUnit = "Unit",
            attributes = new[]
            {
                new { name = "Tamanho", values = new[] { new { tempKey = "p", value = "P" } } },
                new { name = "Cor", values = new[] { new { tempKey = "a", value = "Azul" } } },
            },
            variants = new[] { new { attributeValueIds = new[] { "a", "p" }, sku = prefix, price = 30m, isActive = true } },
        });

        (await VariantLabels(admin, id)).ShouldBe(["P / Azul"]);
    }

    [Fact]
    [Trait("Rule", "RN-CE50")]
    public async Task Should_ReturnRealIds_When_ReadingForm()
    {
        using var admin = await api.AdminAsync();

        var form = await Form(admin, await Create(admin, ApparelBody(Unique("CAM"))));

        var values = form.GetProperty("attributes")[0].GetProperty("values").EnumerateArray().ToList();
        values.Select(v => v.GetProperty("value").GetString()).ShouldBe(["P", "M", "G"]);
        var ids = values.Select(v => v.GetProperty("id").GetString()).ToList();
        form.GetProperty("variants").EnumerateArray()
            .Select(v => v.GetProperty("attributeValueIds")[0].GetString())
            .ShouldBe(ids);
    }

    [Fact]
    public async Task Should_ReturnOnlyOptions_When_ReadingNewForm()
    {
        using var admin = await api.AdminAsync();
        var category = await CreateCategory(admin);
        var collection = await CreateCollection(admin);

        var form = await Json(await admin.GetAsync($"{Route}/form"));

        form.GetProperty("id").ValueKind.ShouldBe(JsonValueKind.Null);
        form.GetProperty("variants").GetArrayLength().ShouldBe(0);
        form.GetProperty("options").GetProperty("categories").EnumerateArray().ShouldContain(c => c.GetProperty("id").GetString() == category);
        form.GetProperty("options").GetProperty("collections").EnumerateArray().ShouldContain(c => c.GetProperty("value").GetString() == collection);
    }

    [Fact]
    [Trait("Rule", "RN-CE53")]
    public async Task Should_ListChildAfterParentWithPath_When_ReadingFormOptions()
    {
        using var admin = await api.AdminAsync();
        // A filha vem logo depois da mãe mesmo com o próprio nome ordenando antes de todas as raízes.
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var earlierRoot = await CreateNamedCategory(admin, $"Zm {suffix}");
        var root = await CreateNamedCategory(admin, $"Zz {suffix}");
        var child = await CreateNamedCategory(admin, $"Aa {suffix}", root);

        var options = (await Json(await admin.GetAsync($"{Route}/form"))).GetProperty("options").GetProperty("categories").EnumerateArray().ToList();

        var rootIndex = options.FindIndex(c => c.GetProperty("id").GetString() == root);
        options.FindIndex(c => c.GetProperty("id").GetString() == earlierRoot).ShouldBeLessThan(rootIndex);
        options[rootIndex + 1].GetProperty("id").GetString().ShouldBe(child);
        options[rootIndex + 1].GetProperty("parentId").GetString().ShouldBe(root);
        options[rootIndex + 1].GetProperty("path").GetString().ShouldBe($"{options[rootIndex].GetProperty("name").GetString()} > {options[rootIndex + 1].GetProperty("name").GetString()}");
    }

    [Fact]
    [Trait("Rule", "RN-CE53")]
    public async Task Should_OfferInactiveCategoryOnlyToLinkedProduct_When_ReadingFormOptions()
    {
        using var admin = await api.AdminAsync();
        var linked = await CreateCategory(admin);
        var unlinked = await CreateCategory(admin);
        var product = await Create(admin, SimpleBody(Unique("form"), categoryIds: new[] { linked }));
        (await admin.DeleteAsync($"/api/categories/{linked}?confirmWarnings=true")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await admin.DeleteAsync($"/api/categories/{unlinked}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var newForm = (await Json(await admin.GetAsync($"{Route}/form"))).GetProperty("options").GetProperty("categories").EnumerateArray().Select(c => c.GetProperty("id").GetString()).ToList();
        var productForm = (await Json(await admin.GetAsync($"{Route}/{product}/form"))).GetProperty("options").GetProperty("categories").EnumerateArray().Select(c => c.GetProperty("id").GetString()).ToList();

        newForm.ShouldNotContain(linked);
        newForm.ShouldNotContain(unlinked);
        productForm.ShouldContain(linked);
        productForm.ShouldNotContain(unlinked);
    }

    private static async Task<string> CreateNamedCategory(HttpClient client, string name, string? parentId = null)
    {
        var response = await client.PostAsJsonAsync("/api/categories", new { name, parentId });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await Json(response)).GetProperty("id").GetString()!;
    }

    [Fact]
    public async Task Should_Return404_When_FormOfUnknownProduct()
    {
        using var admin = await api.AdminAsync();
        (await admin.GetAsync($"{Route}/{Guid.NewGuid()}/form")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---------- Unicidade (RN-CE57) ----------

    [Fact]
    [Trait("Rule", "RN-CE57")]
    public async Task Should_Return422OnSku_When_SkuUsedIgnoringCase()
    {
        using var admin = await api.AdminAsync();
        var sku = Unique("abc");
        await Create(admin, SimpleBody(sku));

        await ShouldFailOn(await admin.PostAsJsonAsync(Route, SimpleBody(sku.ToUpperInvariant())), "variants[0].sku");
    }

    [Fact]
    [Trait("Rule", "RN-CE57")]
    public async Task Should_Return422OnBarcode_When_BarcodeUsed()
    {
        using var admin = await api.AdminAsync();
        var barcode = Unique("789");
        await Create(admin, SimpleBody(Unique("A"), barcode));

        await ShouldFailOn(await admin.PostAsJsonAsync(Route, SimpleBody(Unique("B"), barcode)), "variants[0].barcode");
    }

    [Fact]
    [Trait("Rule", "RN-CE57")]
    public async Task Should_AcceptSeveralProducts_When_BarcodeNotInformed()
    {
        using var admin = await api.AdminAsync();
        await Create(admin, SimpleBody(Unique("A")));
        await Create(admin, SimpleBody(Unique("B"), "  "));
        await Create(admin, SimpleBody(Unique("C")));
    }

    [Fact]
    [Trait("Rule", "RN-CE57")]
    public async Task Should_KeepSkuTaken_When_ProductInactive()
    {
        using var admin = await api.AdminAsync();
        var sku = Unique("A");
        var id = await Create(admin, SimpleBody(sku));
        (await admin.DeleteAsync($"{Route}/{id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await ShouldFailOn(await admin.PostAsJsonAsync(Route, SimpleBody(sku)), "variants[0].sku");
    }

    [Fact]
    [Trait("Rule", "RN-CE57")]
    public async Task Should_Return422OnSku_When_SkuDiffersOnlyByBlanks()
    {
        using var admin = await api.AdminAsync();
        var sku = Unique("abc");
        await Create(admin, SimpleBody(sku));

        await ShouldFailOn(await admin.PostAsJsonAsync(Route, SimpleBody($"  {sku}  ")), "variants[0].sku");
    }

    [Fact]
    [Trait("Rule", "RN-CE57")]
    public async Task Should_Return422OnBarcode_When_BarcodeDiffersOnlyByCaseAndBlanks()
    {
        using var admin = await api.AdminAsync();
        var barcode = Unique("abc");
        await Create(admin, SimpleBody(Unique("A"), barcode));

        await ShouldFailOn(await admin.PostAsJsonAsync(Route, SimpleBody(Unique("B"), $" {barcode.ToUpperInvariant()} ")), "variants[0].barcode");
    }

    private static StringContent RawBody(string attributes, string variants) => new(
        $$"""{ "name": "Camiseta", "productType": "Apparel", "saleUnit": "Unit", "attributes": {{attributes}}, "variants": {{variants}} }""",
        System.Text.Encoding.UTF8,
        "application/json");

    private const string ValidVariant = """[{ "sku": "NULO", "price": 10, "isActive": true }]""";

    [Theory]
    [InlineData("[null]", ValidVariant, "attributes[0]")]
    [InlineData("[]", "[null]", "variants[0]")]
    [InlineData("""[{ "name": "Tamanho", "values": [null] }]""", ValidVariant, "attributes[0].values[0]")]
    [InlineData("""[{ "name": "Tamanho", "values": [{ "tempKey": "p", "value": "P" }, null] }, null]""", "[null]", "attributes[0].values[1]")]
    public async Task Should_Return422OnItem_When_ListHasNullItem(string attributes, string variants, string field)
    {
        using var admin = await api.AdminAsync();
        using var content = RawBody(attributes, variants);

        await ShouldFailOn(await admin.PostAsync(Route, content), field);
    }

    [Theory]
    [InlineData("[null]")]
    [InlineData("""["nao-e-guid"]""")]
    public async Task Should_Return422_When_CategoryIdsHasInvalidItem(string categoryIds)
    {
        using var admin = await api.AdminAsync();
        using var content = new StringContent(
            $$"""{ "name": "Caderno", "productType": "Standard", "saleUnit": "Unit", "categoryIds": {{categoryIds}}, "variants": {{ValidVariant}} }""",
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await admin.PostAsync(Route, content);

        response.StatusCode.ShouldBe((HttpStatusCode)422, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Should_Return422_When_EditingWithNullItem()
    {
        using var admin = await api.AdminAsync();
        var id = await Create(admin, SimpleBody(Unique("A")));
        using var content = RawBody("[]", "[null]");

        await ShouldFailOn(await admin.PutAsync($"{Route}/{id}", content), "variants[0]");
    }

    [Fact]
    [Trait("Rule", "RN-CE50")]
    public async Task Should_Return422_When_CombinationReferenceIsNull()
    {
        using var admin = await api.AdminAsync();

        var body = new
        {
            name = "Camiseta",
            productType = "Apparel",
            saleUnit = "Unit",
            attributes = new[] { new { name = "Tamanho", values = new[] { new { tempKey = "p", value = "P" } } } },
            variants = new[] { new { attributeValueIds = new string?[] { null }, sku = Unique("CAM"), price = 30m, isActive = true } },
        };

        await ShouldFailOn(await admin.PostAsJsonAsync(Route, body), "variants[0].attributeValueIds");
    }

    [Fact]
    [Trait("Rule", "RN-CE57")]
    public async Task Should_RefuseInDatabase_When_SkuOrBarcodeBypassUseCase()
    {
        using var admin = await api.AdminAsync();
        var sku = Unique("A");
        var barcode = Unique("789");
        await Create(admin, SimpleBody(sku, barcode));
        var other = Guid.Parse(await Create(admin, SimpleBody(Unique("B"))));

        await using var context = api.CreateContext();
        await Should.ThrowAsync<Npgsql.PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE product_variants SET sku = {sku} WHERE product_id = {other}"));
        await Should.ThrowAsync<Npgsql.PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE product_variants SET barcode = {barcode} WHERE product_id = {other}"));
    }

    [Fact]
    [Trait("Rule", "RN-CE57")]
    public async Task Should_RefuseInDatabase_When_InsertingSameCodeWithOtherCase()
    {
        using var admin = await api.AdminAsync();
        var sku = Unique("abc");
        var barcode = Unique("xyz");
        var product = Guid.Parse(await Create(admin, SimpleBody(sku, barcode)));

        await using var context = api.CreateContext();
        Task Insert(string newSku, string? newBarcode) => context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO product_variants (id, product_id, sku, barcode, price, sort_order, is_active, created_at) VALUES ({Guid.NewGuid()}, {product}, {newSku}, {newBarcode}, 1, 1, true, now())");

        var sameSku = await Should.ThrowAsync<Npgsql.PostgresException>(() => Insert(sku.ToUpperInvariant(), null));
        sameSku.ConstraintName.ShouldBe("ix_product_variants_lower_sku");
        var sameBarcode = await Should.ThrowAsync<Npgsql.PostgresException>(() => Insert(Unique("outro"), barcode.ToUpperInvariant()));
        sameBarcode.ConstraintName.ShouldBe("ix_product_variants_lower_barcode");

        // Nulos não colidem: código de barras é opcional.
        await Insert(Unique("sem-a"), null);
        await Insert(Unique("sem-b"), null);
    }

    // ---------- Editar: variações, cascatas, troca de tipo ----------

    [Fact]
    [Trait("Rule", "RN-CE51")]
    public async Task Should_KeepRemovedVariantAndItsHistory_When_VariantLeavesForm()
    {
        using var admin = await api.AdminAsync();
        var id = await Create(admin, ApparelBody(Unique("CAM")));
        var form = await Form(admin, id);
        var body = EditBody(form);
        var variants = form.GetProperty("variants").EnumerateArray().ToList();
        body["variants"] = new[] { variants[0], variants[1] };

        var response = await admin.PutAsJsonAsync($"{Route}/{id}", body);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        (await VariantLabels(admin, id)).ShouldBe(["P", "M"]);
        await using var context = api.CreateContext();
        // A variação que saiu continua gravada como removida, porque o histórico de preço aponta para ela.
        (await context.Set<ProductVariant>().CountAsync(v => v.ProductId == Guid.Parse(id) && v.RemovedOn == null)).ShouldBe(2);
        (await context.Set<ProductVariant>().CountAsync(v => v.ProductId == Guid.Parse(id) && v.RemovedOn != null)).ShouldBe(1);
        (await context.Set<ProductVariantValue>().CountAsync(v => v.Variant.ProductId == Guid.Parse(id))).ShouldBe(3);
        var history = await Json(await admin.GetAsync($"{Route}/{id}/price-history"));
        history.GetArrayLength().ShouldBe(3);
        history.EnumerateArray().Single(h => h.GetProperty("variantRemoved").GetBoolean()).GetProperty("validTo").ValueKind.ShouldNotBe(JsonValueKind.Null);
        // O valor G continua no atributo: quem saiu foi a variação.
        (await context.Set<ProductAttributeValue>().CountAsync(v => v.Attribute.ProductId == Guid.Parse(id))).ShouldBe(3);
    }

    [Fact]
    [Trait("Rule", "RN-CE51")]
    public async Task Should_DeleteValueAndLinks_When_ValueAndItsVariantLeaveForm()
    {
        using var admin = await api.AdminAsync();
        var id = await Create(admin, ApparelBody(Unique("CAM")));
        var form = await Form(admin, id);
        var attribute = form.GetProperty("attributes")[0];
        var values = attribute.GetProperty("values").EnumerateArray().ToList();
        var body = EditBody(form);
        body["attributes"] = new[]
        {
            new
            {
                id = attribute.GetProperty("id").GetString(),
                name = "Tamanho",
                values = new[] { values[0], values[1] },
            },
        };
        var variants = form.GetProperty("variants").EnumerateArray().ToList();
        body["variants"] = new[] { variants[0], variants[1] };

        var response = await admin.PutAsJsonAsync($"{Route}/{id}", body);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        await using var context = api.CreateContext();
        var productId = Guid.Parse(id);
        var remaining = await context.Set<ProductAttributeValue>().Where(v => v.Attribute.ProductId == productId).Select(v => v.Value).ToListAsync();
        remaining.ShouldBe(["P", "M"], ignoreOrder: true);
        (await context.Set<ProductVariantValue>().CountAsync(v => v.Variant.ProductId == productId)).ShouldBe(2);
        (await VariantLabels(admin, id)).ShouldBe(["P", "M"]);
    }

    [Fact]
    [Trait("Rule", "RN-CE51")]
    public async Task Should_CascadeAttributeValuesAndLinks_When_GridTurnedOff()
    {
        using var admin = await api.AdminAsync();
        var id = await Create(admin, ApparelBody(Unique("CAM")));
        var form = await Form(admin, id);
        var kept = form.GetProperty("variants")[0];
        var body = EditBody(form);
        body["attributes"] = Array.Empty<object>();
        body["variants"] = new[] { new { id = kept.GetProperty("id").GetString(), sku = kept.GetProperty("sku").GetString(), price = 30m, isActive = true } };

        var response = await admin.PutAsJsonAsync($"{Route}/{id}", body);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        (await Json(response)).GetProperty("hasVariants").GetBoolean().ShouldBeFalse();
        await using var context = api.CreateContext();
        var productId = Guid.Parse(id);
        (await context.Set<ProductAttribute>().CountAsync(a => a.ProductId == productId)).ShouldBe(0);
        (await context.Set<ProductAttributeValue>().CountAsync(v => v.Attribute.ProductId == productId)).ShouldBe(0);
        (await context.Set<ProductVariantValue>().CountAsync(v => v.Variant.ProductId == productId)).ShouldBe(0);
        (await context.Set<ProductVariant>().SingleAsync(v => v.ProductId == productId && v.RemovedOn == null)).Id.ShouldBe(kept.GetProperty("id").GetGuid());
    }

    [Fact]
    [Trait("Rule", "RN-CE48")]
    public async Task Should_KeepHiddenVariantId_When_GridTurnedOn()
    {
        using var admin = await api.AdminAsync();
        var sku = Unique("CAD");
        var id = await Create(admin, SimpleBody(sku));
        var form = await Form(admin, id);
        var hidden = form.GetProperty("variants")[0].GetProperty("id").GetString();
        var body = EditBody(form);
        body["attributes"] = new[] { new { name = "Cor", values = new[] { new { tempKey = "a", value = "Azul" }, new { tempKey = "b", value = "Branco" } } } };
        body["variants"] = new object[]
        {
            new { id = hidden, attributeValueIds = new[] { "a" }, sku, price = 12.5m, isActive = true },
            new { id = (string?)null, attributeValueIds = new[] { "b" }, sku = sku + "-B", price = 13m, isActive = true },
        };

        var response = await admin.PutAsJsonAsync($"{Route}/{id}", body);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        (await VariantLabels(admin, id)).ShouldBe(["Azul", "Branco"]);
        var variants = await Json(await admin.GetAsync($"{Route}/{id}/variants"));
        variants[0].GetProperty("id").GetString().ShouldBe(hidden);
    }

    [Fact]
    [Trait("Rule", "RN-CE50")]
    public async Task Should_AddValueAndVariant_When_EditingWithTempKey()
    {
        using var admin = await api.AdminAsync();
        var prefix = Unique("CAM");
        var id = await Create(admin, ApparelBody(prefix));
        var form = await Form(admin, id);
        var attribute = form.GetProperty("attributes")[0];
        var body = EditBody(form);
        body["attributes"] = new[]
        {
            new
            {
                id = attribute.GetProperty("id").GetString(),
                name = "Tamanho",
                values = attribute.GetProperty("values").EnumerateArray()
                    .Select(v => new { id = v.GetProperty("id").GetString(), tempKey = (string?)null, value = v.GetProperty("value").GetString() })
                    .Append(new { id = (string?)null, tempKey = (string?)"gg", value = (string?)"GG" })
                    .ToArray(),
            },
        };
        var variants = form.GetProperty("variants").EnumerateArray().Select(v => (object)v).ToList();
        variants.Add(new { attributeValueIds = new[] { "gg" }, sku = $"{prefix}-GG", price = 40m, isActive = true });
        body["variants"] = variants;

        var response = await admin.PutAsJsonAsync($"{Route}/{id}", body);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        (await VariantLabels(admin, id)).ShouldBe(["P", "M", "G", "GG"]);
    }

    [Fact]
    [Trait("Rule", "RN-CE57")]
    public async Task Should_Return422_When_ReusingSkuOfVariantRemovedInSameSave()
    {
        using var admin = await api.AdminAsync();
        var prefix = Unique("CAM");
        var id = await Create(admin, ApparelBody(prefix));
        var form = await Form(admin, id);
        var variants = form.GetProperty("variants").EnumerateArray().ToList();
        var body = EditBody(form);
        body["variants"] = new object[]
        {
            variants[0],
            variants[1],
            new { attributeValueIds = new[] { variants[2].GetProperty("attributeValueIds")[0].GetString() }, sku = $"{prefix}-G", price = 99m, isActive = true },
        };

        await ShouldFailOn(await admin.PutAsJsonAsync($"{Route}/{id}", body), "variants[2].sku");
    }

    [Fact]
    [Trait("Rule", "RN-CE57")]
    public async Task Should_Return422_When_KeptVariantTakesCodeOfRemovedVariant()
    {
        using var admin = await api.AdminAsync();
        var prefix = Unique("CAM");
        var barcode = Unique("789");
        var id = await Create(admin, ApparelBody(prefix));
        var initial = EditBody(await Form(admin, id));
        var rows = (await Form(admin, id)).GetProperty("variants");
        initial["variants"] = new[] { rows[0], rows[1], VariantRow(rows[2], $"{prefix}-G", barcode) };
        (await admin.PutAsJsonAsync($"{Route}/{id}", initial)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // O G sai, mas segue reservado pelo histórico: o P não pode assumir o SKU dele.
        var form = await Form(admin, id);
        var variants = form.GetProperty("variants").EnumerateArray().ToList();
        var body = EditBody(form);
        body["variants"] = new object[] { VariantRow(variants[0], $"{prefix}-G", barcode), variants[1] };

        await ShouldFailOn(await admin.PutAsJsonAsync($"{Route}/{id}", body), "variants[0].sku");
    }

    [Fact]
    [Trait("Rule", "RN-CE57")]
    public async Task Should_Return422_When_ReusingCodeOfLeavingVariantWithOtherCase()
    {
        using var admin = await api.AdminAsync();
        var prefix = Unique("cam");
        var id = await Create(admin, ApparelBody(prefix));
        var form = await Form(admin, id);
        var variants = form.GetProperty("variants").EnumerateArray().ToList();
        var body = EditBody(form);
        body["variants"] = new object[] { VariantRow(variants[0], $"{prefix}-G".ToUpperInvariant(), null), variants[1] };

        await ShouldFailOn(await admin.PutAsJsonAsync($"{Route}/{id}", body), "variants[0].sku");
    }

    private static object VariantRow(JsonElement variant, string sku, string? barcode) => new
    {
        id = variant.GetProperty("id").GetString(),
        attributeValueIds = variant.GetProperty("attributeValueIds").EnumerateArray().Select(v => v.GetString()).ToArray(),
        sku,
        barcode,
        price = variant.GetProperty("price").GetDecimal(),
        isActive = true,
    };

    [Fact]
    [Trait("Rule", "RN-CE49")]
    public async Task Should_PersistFormOrder_When_EditingMovesAttributesValuesAndVariants()
    {
        using var admin = await api.AdminAsync();
        var prefix = Unique("CAM");
        var id = await Create(admin, new
        {
            name = "Camiseta",
            productType = "Apparel",
            saleUnit = "Unit",
            attributes = new[]
            {
                new { name = "Tamanho", values = new[] { new { tempKey = "p", value = "P" }, new { tempKey = "m", value = "M" } } },
                new { name = "Cor", values = new[] { new { tempKey = "a", value = "Azul" } } },
            },
            variants = new[]
            {
                new { attributeValueIds = new[] { "p", "a" }, sku = $"{prefix}-P", price = 30m, isActive = true },
                new { attributeValueIds = new[] { "m", "a" }, sku = $"{prefix}-M", price = 30m, isActive = true },
            },
        });
        var form = await Form(admin, id);
        var body = EditBody(form);
        var size = form.GetProperty("attributes")[0];
        var color = form.GetProperty("attributes")[1];
        body["attributes"] = new object[]
        {
            color,
            new
            {
                id = size.GetProperty("id").GetString(),
                name = "Tamanho",
                values = size.GetProperty("values").EnumerateArray().Reverse().ToArray(),
            },
        };
        body["variants"] = form.GetProperty("variants").EnumerateArray().Reverse().ToArray();

        var response = await admin.PutAsJsonAsync($"{Route}/{id}", body);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var after = await Form(admin, id);
        after.GetProperty("attributes").EnumerateArray().Select(a => a.GetProperty("name").GetString()).ShouldBe(["Cor", "Tamanho"]);
        after.GetProperty("attributes")[1].GetProperty("values").EnumerateArray().Select(v => v.GetProperty("value").GetString()).ShouldBe(["M", "P"]);
        after.GetProperty("variants").EnumerateArray().Select(v => v.GetProperty("sku").GetString()).ShouldBe([$"{prefix}-M", $"{prefix}-P"]);
        (await VariantLabels(admin, id)).ShouldBe(["Azul / M", "Azul / P"]);
    }

    [Fact]
    public async Task Should_PersistNewName_When_RenamingValueAndAttribute()
    {
        using var admin = await api.AdminAsync();
        var id = await Create(admin, ApparelBody(Unique("CAM")));
        var form = await Form(admin, id);
        var attribute = form.GetProperty("attributes")[0];
        var values = attribute.GetProperty("values").EnumerateArray().ToList();
        var body = EditBody(form);
        body["attributes"] = new[]
        {
            new
            {
                id = attribute.GetProperty("id").GetString(),
                name = "Numeração",
                values = new object[]
                {
                    new { id = values[0].GetProperty("id").GetString(), value = "Pequeno" },
                    values[1],
                    values[2],
                },
            },
        };

        var response = await admin.PutAsJsonAsync($"{Route}/{id}", body);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        (await VariantLabels(admin, id)).ShouldBe(["Pequeno", "M", "G"]);
        var after = await Form(admin, id);
        after.GetProperty("attributes")[0].GetProperty("name").GetString().ShouldBe("Numeração");
        after.GetProperty("attributes")[0].GetProperty("values")[0].GetProperty("id").GetString().ShouldBe(values[0].GetProperty("id").GetString());
    }

    [Fact]
    [Trait("Rule", "RN-CE54")]
    public async Task Should_KeepVariantInactive_When_Editing()
    {
        using var admin = await api.AdminAsync();
        var id = await Create(admin, ApparelBody(Unique("CAM")));
        var form = await Form(admin, id);
        var rows = form.GetProperty("variants").EnumerateArray().ToList();
        var body = EditBody(form);
        body["variants"] = new object[]
        {
            new
            {
                id = rows[0].GetProperty("id").GetString(),
                attributeValueIds = rows[0].GetProperty("attributeValueIds").EnumerateArray().Select(v => v.GetString()).ToArray(),
                sku = rows[0].GetProperty("sku").GetString(),
                price = 30m,
                isActive = false,
            },
            rows[1],
            rows[2],
        };

        (await admin.PutAsJsonAsync($"{Route}/{id}", body)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var variants = await Json(await admin.GetAsync($"{Route}/{id}/variants"));
        variants[0].GetProperty("isActive").GetBoolean().ShouldBeFalse();
        variants[1].GetProperty("isActive").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    [Trait("Rule", "RN-CE50")]
    public async Task Should_ResolveCombination_When_ValueIdComesInUppercase()
    {
        using var admin = await api.AdminAsync();
        var id = await Create(admin, ApparelBody(Unique("CAM")));
        var form = await Form(admin, id);
        var rows = form.GetProperty("variants").EnumerateArray().ToList();
        var body = EditBody(form);
        var upper = rows[0].GetProperty("attributeValueIds")[0].GetString()!.ToUpperInvariant();
        body["variants"] = new object[]
        {
            new { id = rows[0].GetProperty("id").GetString(), attributeValueIds = new[] { upper }, sku = rows[0].GetProperty("sku").GetString(), price = 30m, isActive = true },
            rows[1],
            rows[2],
        };

        var response = await admin.PutAsJsonAsync($"{Route}/{id}", body);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        (await VariantLabels(admin, id)).ShouldBe(["P", "M", "G"]);
    }

    [Fact]
    [Trait("Rule", "RN-CE53")]
    public async Task Should_ReturnPathsInResponse_When_EditingCategoryAndCollection()
    {
        using var admin = await api.AdminAsync();
        var root = await CreateCategory(admin);
        var child = await CreateCategory(admin, root);
        var collection = await CreateCollection(admin);
        var id = await Create(admin, SimpleBody(Unique("A")));
        var body = EditBody(await Form(admin, id));
        body["categoryIds"] = new[] { child };
        body["collectionId"] = collection;

        var response = await admin.PutAsJsonAsync($"{Route}/{id}", body);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var detail = await Json(response);
        detail.GetProperty("categories")[0].GetProperty("path").GetString()!.ShouldContain(" > ");
        detail.GetProperty("collection").GetProperty("value").GetString().ShouldBe(collection);
    }

    [Fact]
    [Trait("Rule", "RN-CE55")]
    public async Task Should_KeepEverything_When_TypeChanges()
    {
        using var admin = await api.AdminAsync();
        var id = await Create(admin, ApparelBody(Unique("CAM")));
        var form = await Form(admin, id);
        var before = form.GetProperty("variants").EnumerateArray().Select(v => v.GetProperty("id").GetString()).ToList();
        var body = EditBody(form);
        body["productType"] = "Perishable";
        body["tracksLots"] = true;
        body["shelfLifeDays"] = 30;

        var response = await admin.PutAsJsonAsync($"{Route}/{id}", body);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var detail = await Json(response);
        detail.GetProperty("productType").GetString().ShouldBe("Perishable");
        detail.GetProperty("tracksLots").GetBoolean().ShouldBeTrue();
        detail.GetProperty("shelfLifeDays").GetInt32().ShouldBe(30);
        detail.GetProperty("hasVariants").GetBoolean().ShouldBeTrue();
        var after = await Json(await admin.GetAsync($"{Route}/{id}/variants"));
        after.EnumerateArray().Select(v => v.GetProperty("id").GetString()).ShouldBe(before);
    }

    [Fact]
    [Trait("Rule", "RN-CE04")]
    public async Task Should_Return400_When_EditingInactiveThenAcceptAfterReactivate()
    {
        using var admin = await api.AdminAsync();
        var id = await Create(admin, SimpleBody(Unique("A")));
        var body = EditBody(await Form(admin, id));
        (await admin.DeleteAsync($"{Route}/{id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await admin.PutAsJsonAsync($"{Route}/{id}", body)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await admin.PatchAsync($"{Route}/{id}/reactivate", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await admin.PutAsJsonAsync($"{Route}/{id}", body)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Rule", "RN-CE19")]
    public async Task Should_MarkDiscontinued_When_PatchingLifecycle()
    {
        using var admin = await api.AdminAsync();
        var id = await Create(admin, SimpleBody(Unique("A")));

        (await admin.PatchAsJsonAsync($"{Route}/{id}/lifecycle", new { lifecycle = "Discontinued" })).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await Json(await admin.GetAsync($"{Route}/{id}"))).GetProperty("lifecycle").GetString().ShouldBe("Discontinued");
    }

    [Fact]
    [Trait("Rule", "RN-CE19")]
    public async Task Should_Return422_When_LifecycleUnknown()
    {
        using var admin = await api.AdminAsync();
        var id = await Create(admin, SimpleBody(Unique("A")));

        (await admin.PatchAsJsonAsync($"{Route}/{id}/lifecycle", new { lifecycle = "Quebrado" })).StatusCode.ShouldBe((HttpStatusCode)422);
    }

    // ---------- Categorias e coleções (RN-CE53) ----------

    [Fact]
    public async Task Should_ReturnPaths_When_ProductHasCategoryAndCollection()
    {
        using var admin = await api.AdminAsync();
        var root = await CreateCategory(admin);
        var child = await CreateCategory(admin, root);
        var collection = await CreateCollection(admin);

        var response = await admin.PostAsJsonAsync(Route, SimpleBody(Unique("A"), categoryIds: new[] { root, child }, collectionId: collection));
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var detail = await Json(response);
        detail.GetProperty("categories").GetArrayLength().ShouldBe(2);
        detail.GetProperty("collection").GetProperty("value").GetString().ShouldBe(collection);
        detail.GetProperty("categories").EnumerateArray().ShouldContain(c => c.GetProperty("path").GetString()!.Contains(" > "));
    }

    [Fact]
    [Trait("Rule", "RN-CE52")]
    public async Task Should_ShowMostSpecificPath_When_ListingProductInRootAndChild()
    {
        using var admin = await api.AdminAsync();
        var root = await CreateCategory(admin);
        var child = await CreateCategory(admin, root);
        var name = Unique("Principal");
        await Create(admin, SimpleBody(Unique("A"), name: name, categoryIds: new[] { root, child }));

        var page = await Json(await admin.GetAsync($"{Route}?filters={Filters(new { search = name })}"));

        page.GetProperty("items")[0].GetProperty("categoryPath").GetString()!.ShouldContain(" › ");
    }

    [Fact]
    [Trait("Rule", "RN-CE53")]
    public async Task Should_RemoveLinkRow_When_CategoryLeavesProduct()
    {
        using var admin = await api.AdminAsync();
        var first = await CreateCategory(admin);
        var second = await CreateCategory(admin);
        var id = await Create(admin, SimpleBody(Unique("A"), categoryIds: new[] { first }));
        var body = EditBody(await Form(admin, id));
        body["categoryIds"] = new[] { second };

        (await admin.PutAsJsonAsync($"{Route}/{id}", body)).StatusCode.ShouldBe(HttpStatusCode.OK);

        await using var context = api.CreateContext();
        var links = await context.Set<CategoryProduct>().Where(c => c.ProductId == Guid.Parse(id)).Select(c => c.CategoryId).ToListAsync();
        links.ShouldBe([Guid.Parse(second)]);
    }

    [Fact]
    [Trait("Rule", "RN-CE53")]
    public async Task Should_AskThenInactivateKeepingLink_When_CategoryHasActiveProduct()
    {
        using var admin = await api.AdminAsync();
        var category = await CreateCategory(admin);
        var id = await Create(admin, SimpleBody(Unique("A"), categoryIds: new[] { category }));

        await ShouldFailOn(await admin.DeleteAsync($"/api/categories/{category}"), "confirm");
        (await Json(await admin.GetAsync($"/api/categories/{category}"))).GetProperty("isActive").GetBoolean().ShouldBeTrue();

        (await admin.DeleteAsync($"/api/categories/{category}?confirmWarnings=true")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Json(await admin.GetAsync($"/api/categories/{category}"))).GetProperty("isActive").GetBoolean().ShouldBeFalse();

        // O vínculo fica, o produto segue editável e o combo do formulário ainda mostra a categoria.
        var form = await Form(admin, id);
        form.GetProperty("categoryIds")[0].GetString().ShouldBe(category);
        form.GetProperty("options").GetProperty("categories").EnumerateArray().ShouldContain(c => c.GetProperty("id").GetString() == category);
        (await admin.PutAsJsonAsync($"{Route}/{id}", EditBody(form))).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Rule", "RN-CE53")]
    public async Task Should_InactivateWithoutAsking_When_OnlyInactiveProducts()
    {
        using var admin = await api.AdminAsync();
        var category = await CreateCategory(admin);
        var id = await Create(admin, SimpleBody(Unique("A"), categoryIds: new[] { category }));
        (await admin.DeleteAsync($"{Route}/{id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await admin.DeleteAsync($"/api/categories/{category}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    [Trait("Rule", "RN-CE53")]
    public async Task Should_CountActiveProducts_When_ReadingCategoryTree()
    {
        using var admin = await api.AdminAsync();
        var root = await CreateCategory(admin);
        var child = await CreateCategory(admin, root);
        await Create(admin, SimpleBody(Unique("A"), categoryIds: new[] { root }));
        await Create(admin, SimpleBody(Unique("B"), categoryIds: new[] { child }));
        await Create(admin, SimpleBody(Unique("C"), categoryIds: new[] { child }));
        var inactive = await Create(admin, SimpleBody(Unique("D"), categoryIds: new[] { child }));
        (await admin.DeleteAsync($"{Route}/{inactive}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var tree = await Json(await admin.GetAsync("/api/categories/tree"));

        var node = tree.EnumerateArray().Single(n => n.GetProperty("id").GetString() == root);
        node.GetProperty("productCount").GetInt32().ShouldBe(1);
        node.GetProperty("children")[0].GetProperty("productCount").GetInt32().ShouldBe(2);

        // Inativo vinculado direto ao nível 1 também não conta.
        var inactiveRoot = await Create(admin, SimpleBody(Unique("E"), categoryIds: new[] { root }));
        (await admin.DeleteAsync($"{Route}/{inactiveRoot}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        tree = await Json(await admin.GetAsync("/api/categories/tree"));
        tree.EnumerateArray().Single(n => n.GetProperty("id").GetString() == root).GetProperty("productCount").GetInt32().ShouldBe(1);
    }

    [Fact]
    [Trait("Rule", "RN-CE53")]
    public async Task Should_CountActiveProducts_When_ReadingCollection()
    {
        using var admin = await api.AdminAsync();
        var collection = await CreateCollection(admin);
        await Create(admin, SimpleBody(Unique("A"), collectionId: collection));
        var inactive = await Create(admin, SimpleBody(Unique("B"), collectionId: collection));
        (await admin.DeleteAsync($"{Route}/{inactive}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await Json(await admin.GetAsync($"/api/collections/{collection}"))).GetProperty("productCount").GetInt32().ShouldBe(1);

        var page = await Json(await admin.GetAsync($"/api/collections?limit=100&filters={Filters(new { isActive = "true" })}"));
        page.GetProperty("items").EnumerateArray().Single(c => c.GetProperty("id").GetString() == collection)
            .GetProperty("productCount").GetInt32().ShouldBe(1);

        var update = await admin.PutAsJsonAsync($"/api/collections/{collection}", new { name = Unique("Col") });
        (await Json(update)).GetProperty("productCount").GetInt32().ShouldBe(1);
    }

    [Fact]
    [Trait("Rule", "RN-CE53")]
    public async Task Should_InactivateWithoutAsking_When_ProductsOnlyInChild()
    {
        using var admin = await api.AdminAsync();
        var root = await CreateCategory(admin);
        var child = await CreateCategory(admin, root);
        await Create(admin, SimpleBody(Unique("A"), categoryIds: new[] { child }));
        (await admin.DeleteAsync($"/api/categories/{child}?confirmWarnings=true")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // A pergunta conta só o vínculo direto: o produto da filha não vale para o nível 1.
        (await admin.DeleteAsync($"/api/categories/{root}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    [Trait("Rule", "RN-CE53")]
    public async Task Should_ShowInactiveLinkedCollectionOnlyInItsForm_When_CollectionInactivated()
    {
        using var admin = await api.AdminAsync();
        var collection = await CreateCollection(admin);
        var id = await Create(admin, SimpleBody(Unique("A"), collectionId: collection));
        (await admin.DeleteAsync($"/api/collections/{collection}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var form = await Form(admin, id);
        form.GetProperty("collectionId").GetString().ShouldBe(collection);
        form.GetProperty("options").GetProperty("collections").EnumerateArray().ShouldContain(c => c.GetProperty("value").GetString() == collection);

        var newForm = await Json(await admin.GetAsync($"{Route}/form"));
        newForm.GetProperty("options").GetProperty("collections").EnumerateArray().ShouldNotContain(c => c.GetProperty("value").GetString() == collection);
    }

    [Fact]
    [Trait("Rule", "RN-CE52")]
    public async Task Should_ShowFirstPathAlphabetically_When_ProductInSeveralCategoriesOfSameLevel()
    {
        using var admin = await api.AdminAsync();
        var suffix = Unique("x");
        var ids = new List<string>();
        foreach (var letter in new[] { "F", "E", "D", "C", "B", "A" })
        {
            var created = await admin.PostAsJsonAsync("/api/categories", new { name = $"{letter}-{suffix}" });
            ids.Add((await Json(created)).GetProperty("id").GetString()!);
        }
        var name = Unique("Primeira");
        await Create(admin, SimpleBody(Unique("A"), name: name, categoryIds: ids));

        var page = await Json(await admin.GetAsync($"{Route}?filters={Filters(new { search = name })}"));

        page.GetProperty("items")[0].GetProperty("categoryPath").GetString().ShouldBe($"A-{suffix}");
    }

    // ---------- Listagem (T1) ----------

    [Fact]
    public async Task Should_FindBySkuBarcodeBrandOrName_When_Searching()
    {
        using var admin = await api.AdminAsync();
        var sku = Unique("BUSCA");
        var barcode = Unique("789");
        var name = Unique("Produto");
        await Create(admin, SimpleBody(sku, barcode, name));

        foreach (var term in new[] { sku.ToLowerInvariant(), barcode, name.ToUpperInvariant() })
        {
            var page = await Json(await admin.GetAsync($"{Route}?filters={Filters(new { search = term })}"));
            Names(page).ShouldBe([name], term);
        }
    }

    [Fact]
    public async Task Should_FilterByRootCategory_When_ProductIsInChild()
    {
        using var admin = await api.AdminAsync();
        var root = await CreateCategory(admin);
        var child = await CreateCategory(admin, root);
        var otherRoot = await CreateCategory(admin);
        var name = Unique("Filha");
        await Create(admin, SimpleBody(Unique("A"), name: name, categoryIds: new[] { child }));

        Names(await Json(await admin.GetAsync($"{Route}?filters={Filters(new { categoryId = root })}"))).ShouldBe([name]);
        Names(await Json(await admin.GetAsync($"{Route}?filters={Filters(new { categoryId = child })}"))).ShouldBe([name]);
        Names(await Json(await admin.GetAsync($"{Route}?filters={Filters(new { categoryId = otherRoot })}"))).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_CombineFilters_When_ListingByTypeLifecycleCollectionAndStatus()
    {
        using var admin = await api.AdminAsync();
        var collection = await CreateCollection(admin);
        var keep = Unique("Fica");
        var discontinued = await Create(admin, SimpleBody(Unique("A"), name: Unique("Fora"), collectionId: collection, productType: "Bulk"));
        await Create(admin, SimpleBody(Unique("B"), name: keep, collectionId: collection, productType: "Bulk"));
        await Create(admin, SimpleBody(Unique("C"), name: Unique("Tipo"), collectionId: collection));
        var inactive = await Create(admin, SimpleBody(Unique("D"), name: Unique("Inativo"), collectionId: collection, productType: "Bulk"));
        (await admin.PatchAsJsonAsync($"{Route}/{discontinued}/lifecycle", new { lifecycle = "Discontinued" })).EnsureSuccessStatusCode();
        (await admin.DeleteAsync($"{Route}/{inactive}")).EnsureSuccessStatusCode();

        var page = await Json(await admin.GetAsync($"{Route}?filters={Filters(new { collectionId = collection, productType = "Bulk", lifecycle = "Active", isActive = "true" })}"));

        Names(page).ShouldBe([keep]);
        page.GetProperty("total").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Should_FindByBrand_When_Searching()
    {
        using var admin = await api.AdminAsync();
        var brand = Unique("Marca");
        var name = Unique("ComMarca");
        await Create(admin, new { name, brand, productType = "Standard", saleUnit = "Unit", variants = new[] { new { sku = Unique("A"), price = 5m, isActive = true } } });

        Names(await Json(await admin.GetAsync($"{Route}?filters={Filters(new { search = brand.ToUpperInvariant() })}"))).ShouldBe([name]);
    }

    [Fact]
    public async Task Should_IgnoreInactiveVariant_When_SortingByPrice()
    {
        using var admin = await api.AdminAsync();
        var collection = await CreateCollection(admin);
        var withInactiveCheap = Unique("Grade");
        var middle = Unique("Meio");
        await Create(admin, new
        {
            name = withInactiveCheap,
            productType = "Apparel",
            saleUnit = "Unit",
            collectionId = collection,
            attributes = new[] { new { name = "Tamanho", values = new[] { new { tempKey = "p", value = "P" }, new { tempKey = "m", value = "M" } } } },
            variants = new[]
            {
                new { attributeValueIds = new[] { "p" }, sku = Unique("A"), price = 1m, isActive = false },
                new { attributeValueIds = new[] { "m" }, sku = Unique("B"), price = 50m, isActive = true },
            },
        });
        await Create(admin, new { name = middle, productType = "Standard", saleUnit = "Unit", collectionId = collection, variants = new[] { new { sku = Unique("C"), price = 10m, isActive = true } } });

        Names(await Json(await admin.GetAsync($"{Route}?sort=price&order=asc&filters={Filters(new { collectionId = collection })}"))).ShouldBe([middle, withInactiveCheap]);
    }

    [Fact]
    public async Task Should_SortByLowestActivePrice_When_SortingByPrice()
    {
        using var admin = await api.AdminAsync();
        var collection = await CreateCollection(admin);
        var expensive = Unique("Caro");
        var cheap = Unique("Barato");
        await Create(admin, new { name = expensive, productType = "Standard", saleUnit = "Unit", collectionId = collection, variants = new[] { new { sku = Unique("A"), price = 50m, isActive = true } } });
        await Create(admin, new { name = cheap, productType = "Standard", saleUnit = "Unit", collectionId = collection, variants = new[] { new { sku = Unique("B"), price = 5m, isActive = true } } });

        var filter = Filters(new { collectionId = collection });
        Names(await Json(await admin.GetAsync($"{Route}?sort=price&order=asc&filters={filter}"))).ShouldBe([cheap, expensive]);
        Names(await Json(await admin.GetAsync($"{Route}?sort=price&order=desc&filters={filter}"))).ShouldBe([expensive, cheap]);
    }

    [Fact]
    public async Task Should_ReturnProvisionalValues_When_ListingBeforeLedger()
    {
        using var admin = await api.AdminAsync();
        var name = Unique("Provisorio");
        await Create(admin, ApparelBody(Unique("CAM"), name));

        var item = (await Json(await admin.GetAsync($"{Route}?filters={Filters(new { search = name })}"))).GetProperty("items")[0];

        item.GetProperty("availableQuantity").GetDecimal().ShouldBe(0);
        item.GetProperty("hasLowStock").GetBoolean().ShouldBeFalse();
        item.GetProperty("activePromotionPercent").ValueKind.ShouldBe(JsonValueKind.Null);
        item.GetProperty("variantCount").GetInt32().ShouldBe(3);
        item.GetProperty("minEffectivePrice").GetDecimal().ShouldBe(30m);
        item.GetProperty("maxEffectivePrice").GetDecimal().ShouldBe(35m);
    }

    // ---------- Imagem (RN-CE05) ----------

    private static MultipartFormDataContent Image(byte[] bytes, string fileName, string contentType = "image/png")
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "image", fileName } };
    }

    [Fact]
    [Trait("Rule", "RN-CE05")]
    public async Task Should_StoreKeyAndReturnUrl_When_UploadingImage()
    {
        using var admin = await api.AdminAsync();
        var id = await Create(admin, SimpleBody(Unique("A")));

        var response = await admin.PostAsync($"{Route}/{id}/image", Image(Png, "foto.png"));
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var url = (await Json(response)).GetProperty("imageUrl").GetString()!;
        url.ShouldStartWith(InMemoryFileStorage.BaseUrl + "products/");
        var key = url[InMemoryFileStorage.BaseUrl.Length..];
        api.Files.Files[key].ShouldBe(Png);
        (await Json(await admin.GetAsync($"{Route}/{id}"))).GetProperty("imageUrl").GetString().ShouldBe(url);

        await using var context = api.CreateContext();
        (await context.Set<Product>().SingleAsync(p => p.Id == Guid.Parse(id))).ImageKey.ShouldBe(key);
    }

    [Fact]
    [Trait("Rule", "RN-CE05")]
    public async Task Should_Return422AndStoreNothing_When_ContentIsNotImage()
    {
        using var admin = await api.AdminAsync();
        var id = await Create(admin, SimpleBody(Unique("A")));
        var before = api.Files.Files.Count;

        await ShouldFailOn(await admin.PostAsync($"{Route}/{id}/image", Image("nao sou png"u8.ToArray(), "foto.png")), "image");

        api.Files.Files.Count.ShouldBe(before);
    }

    [Fact]
    [Trait("Rule", "RN-CE05")]
    public async Task Should_Return422_When_MultipartWithoutFile()
    {
        using var admin = await api.AdminAsync();
        var id = await Create(admin, SimpleBody(Unique("A")));

        await ShouldFailOn(await admin.PostAsync($"{Route}/{id}/image", new MultipartFormDataContent { { new StringContent("x"), "outro" } }), "image");
    }

    [Fact]
    public async Task Should_DeleteOldFile_When_ReplacingImage()
    {
        using var admin = await api.AdminAsync();
        var id = await Create(admin, SimpleBody(Unique("A")));
        var first = (await Json(await admin.PostAsync($"{Route}/{id}/image", Image(Png, "a.png")))).GetProperty("imageUrl").GetString()!;

        var second = (await Json(await admin.PostAsync($"{Route}/{id}/image", Image(Png, "b.png")))).GetProperty("imageUrl").GetString()!;

        second.ShouldNotBe(first);
        api.Files.Files.ContainsKey(first[InMemoryFileStorage.BaseUrl.Length..]).ShouldBeFalse();
        api.Files.Files.ContainsKey(second[InMemoryFileStorage.BaseUrl.Length..]).ShouldBeTrue();
    }

    [Fact]
    public async Task Should_ClearKeyAndFile_When_RemovingImage()
    {
        using var admin = await api.AdminAsync();
        var id = await Create(admin, SimpleBody(Unique("A")));
        var url = (await Json(await admin.PostAsync($"{Route}/{id}/image", Image(Png, "a.png")))).GetProperty("imageUrl").GetString()!;

        (await admin.DeleteAsync($"{Route}/{id}/image")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        api.Files.Files.ContainsKey(url[InMemoryFileStorage.BaseUrl.Length..]).ShouldBeFalse();
        (await Json(await admin.GetAsync($"{Route}/{id}"))).GetProperty("imageUrl").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    // ---------- F1.4: preço, fornecedores e eventos ----------

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

    private static async Task<string> CreateSupplier(HttpClient client, string name, int leadTimeDays = 10)
    {
        var response = await client.PostAsJsonAsync("/api/suppliers", new { name, cnpj = NewCnpj(), leadTimeDays });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await Json(response)).GetProperty("id").GetString()!;
    }

    private static async Task<string> CreateEvent(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/calendar-events", new { name = Unique("Evento"), dateRule = "Fixed", month = 6, day = 24, leadDays = 7 });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await Json(response)).GetProperty("id").GetString()!;
    }

    [Fact]
    [Trait("Rule", "RN-CE21")]
    public async Task Should_CountOnlyActiveProducts_When_ListingCalendarEvents()
    {
        using var admin = await api.AdminAsync();
        var eventId = await CreateEvent(admin);
        Dictionary<string, object?> Body() => JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(SimpleBody(Unique("EV"))))!;
        var active = Body();
        active["eventIds"] = new[] { eventId };
        var toInactivate = Body();
        toInactivate["eventIds"] = new[] { eventId };
        await Create(admin, active);
        var inactiveId = await Create(admin, toInactivate);
        (await admin.DeleteAsync($"{Route}/{inactiveId}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var calendarEvent = await Json(await admin.GetAsync($"/api/calendar-events/{eventId}"));
        var page = await Json(await admin.GetAsync($"/api/calendar-events?limit=100&filters={Filters(new { search = calendarEvent.GetProperty("name").GetString() })}"));

        calendarEvent.GetProperty("productCount").GetInt32().ShouldBe(1);
        page.GetProperty("items")[0].GetProperty("productCount").GetInt32().ShouldBe(1);
    }

    [Fact]
    [Trait("Rule", "RN-CE21")]
    public async Task Should_Return422_When_EventInactive()
    {
        using var admin = await api.AdminAsync();
        var eventId = await CreateEvent(admin);
        (await admin.DeleteAsync($"/api/calendar-events/{eventId}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var body = JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(SimpleBody(Unique("EV"))))!;
        body["eventIds"] = new[] { eventId };

        await ShouldFailOn(await admin.PostAsJsonAsync(Route, body), "eventIds");
    }

    [Fact]
    [Trait("Rule", "RN-CE25")]
    public async Task Should_ReturnPriceHistory_When_PriceChangedInEdit()
    {
        using var admin = await api.AdminAsync();
        var id = await Create(admin, SimpleBody(Unique("PR")));
        var form = await Form(admin, id);
        var body = EditBody(form);
        var row = form.GetProperty("variants")[0];
        body["variants"] = new object[] { new { id = row.GetProperty("id").GetString(), attributeValueIds = Array.Empty<string>(), sku = row.GetProperty("sku").GetString(), price = 20m, isActive = true } };

        (await admin.PutAsJsonAsync($"{Route}/{id}", body)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await admin.PutAsJsonAsync($"{Route}/{id}", body)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var history = await Json(await admin.GetAsync($"{Route}/{id}/price-history"));
        history.GetArrayLength().ShouldBe(2);
        history[0].GetProperty("price").GetDecimal().ShouldBe(20m);
        history[0].GetProperty("validTo").ValueKind.ShouldBe(JsonValueKind.Null);
        history[0].GetProperty("changedByName").GetString().ShouldNotBeNullOrEmpty();
        history[1].GetProperty("price").GetDecimal().ShouldBe(12.5m);
        history[1].GetProperty("validTo").ValueKind.ShouldNotBe(JsonValueKind.Null);
    }

    [Fact]
    [Trait("Rule", "RN-CE27")]
    public async Task Should_ReturnEffectiveLeadTime_When_ListingProductSuppliers()
    {
        using var admin = await api.AdminAsync();
        var withDefault = await CreateSupplier(admin, Unique("Forn"), leadTimeDays: 10);
        var withOwn = await CreateSupplier(admin, Unique("Forn"), leadTimeDays: 10);
        var body = JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(SimpleBody(Unique("SU"))))!;
        body["suppliers"] = new object[]
        {
            new { supplierId = withDefault, isPreferred = false },
            new { supplierId = withOwn, isPreferred = true, leadTimeDays = 3, minOrderQty = 24, orderMultiple = 12 },
        };
        var id = await Create(admin, body);

        var suppliers = await Json(await admin.GetAsync($"{Route}/{id}/suppliers"));

        suppliers.GetArrayLength().ShouldBe(2);
        suppliers[0].GetProperty("supplierId").GetString().ShouldBe(withOwn);
        suppliers[0].GetProperty("leadTimeDays").GetInt32().ShouldBe(3);
        suppliers[0].GetProperty("orderMultiple").GetDecimal().ShouldBe(12m);
        suppliers[1].GetProperty("leadTimeDays").GetInt32().ShouldBe(10);
    }
}
