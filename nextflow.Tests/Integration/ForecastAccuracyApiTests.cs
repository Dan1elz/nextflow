using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nextflow.Domain.Dtos.DerivedData;
using Nextflow.Domain.Dtos.Forecasts;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Models;
using Nextflow.Tests.Support;

namespace Nextflow.Tests.Integration;

/// <summary>
/// Rotas de precisão (F3.6c, T-DF10 e <c>accuracy</c> do T-DF5) pela API, num Postgres só deste cenário (o banco da
/// <see cref="AccessApiCollection"/> recebe análises de outros testes). Catálogo demo sem histórico, relógio em 26/10/2026 e dois
/// produtos com vendas e análises montadas à mão:
/// P1 (classe A): ago 110 × 100 (10%), set 88 × 80 (10%) → erro médio 10%, superestima, dentro de ±15%.
/// P2 (classe B): ago 70 × 100 (30%), set 40 × 50 (20%) → erro médio 25%, subestima, fora.
/// Os dois na mesma subcategoria; julho fica fora (nenhuma análise antes de 01/07) e a análise com falha é ignorada.
/// </summary>
[Collection(DemoSeedCollection.Name)]
public class ForecastAccuracyApiTests(DemoSeedFixture fixture)
{
    private const string Route = "/api/forecasts";
    private static readonly DateOnly Today = new(2026, 10, 26);

    private sealed record Seeded(WebApplicationFactory<Program> Host, string Connection, Guid P1, string P1Name, Guid P2, string CategoryPath);

    private static Seeded? _seeded;

    private async Task<Seeded> Seed()
    {
        if (_seeded is not null)
            return _seeded;

        var connection = await fixture.NewDatabaseAsync();
        var host = fixture.StartHost(connection, demoEnabled: true, configureServices: s =>
            s.AddSingleton<IBusinessClock>(new FixedBusinessClock(Today)));

        await using var context = fixture.CreateContext(connection);
        var products = await context.Set<Product>().OrderBy(p => p.Name).Take(2).ToListAsync();
        var (p1, p2) = (products[0], products[1]);
        var category = await context.Set<Category>().Include(c => c.Parent).Where(c => c.ParentId != null).OrderBy(c => c.Name).FirstAsync();

        await context.Set<CategoryProduct>().Where(l => l.ProductId == p1.Id || l.ProductId == p2.Id).ExecuteDeleteAsync();
        await context.Set<ProductSalesMonthly>().Where(s => s.ProductId == p1.Id || s.ProductId == p2.Id).ExecuteDeleteAsync();
        p1.ApplyAbcClass(AbcClassEnum.A);
        p2.ApplyAbcClass(AbcClassEnum.B);
        context.AddRange(new CategoryProduct(category.Id, p1.Id), new CategoryProduct(category.Id, p2.Id));
        context.AddRange(
            Sales(p1.Id, 7, 90m), Sales(p1.Id, 8, 100m, 3), Sales(p1.Id, 9, 80m),
            Sales(p2.Id, 8, 100m), Sales(p2.Id, 9, 50m));

        AddRun(context, new DateOnly(2026, 7, 10), ForecastRunStatusEnum.Completed, (p1.Id, 7, 95m), (p1.Id, 8, 110m), (p1.Id, 9, 120m), (p2.Id, 8, 70m));
        AddRun(context, new DateOnly(2026, 8, 20), ForecastRunStatusEnum.CompletedWithoutAi, (p1.Id, 9, 88m), (p2.Id, 9, 40m));
        AddRun(context, new DateOnly(2026, 8, 25), ForecastRunStatusEnum.Failed, (p1.Id, 9, 999m));
        await context.SaveChangesAsync();

        return _seeded = new Seeded(host, connection, p1.Id, p1.Name, p2.Id, $"{category.Parent!.Name} > {category.Name}");
    }

    private static ProductSalesMonthly Sales(Guid productId, short month, decimal sold, short daysOut = 0)
        => new(new ProductSalesMonthlyDto(productId, 2026, month, sold, 0, daysOut, 0, 0, 0));

    private static void AddRun(DbContext context, DateOnly reference, ForecastRunStatusEnum status, params (Guid ProductId, short Month, decimal Qty)[] projections)
    {
        var started = reference.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc);
        var run = new ForecastRun(new StartForecastRunDto(ForecastTriggerEnum.Scheduled, null), reference, started);
        if (status == ForecastRunStatusEnum.Failed)
            run.Fail("falhou", started.AddMinutes(1));
        else
            run.Complete(status, "ok", null, null, null, started.AddMinutes(1));

        context.Add(run);
        foreach (var (productId, month, qty) in projections)
            context.Add(new ForecastProjection(new NewForecastProjectionDto(run.Id, productId, 2026, month, qty)));
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<HttpClient> Login(WebApplicationFactory<Program> host, string email, string password)
    {
        var client = host.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        var token = (await Json(login)).GetProperty("token").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static Task<HttpClient> Admin(Seeded seeded)
        => Login(seeded.Host, AccessApiFixture.AdminEmail, AccessApiFixture.AdminPassword);

    [Fact]
    [Trait("Rule", "RN-DF19")]
    public async Task Should_SummarizeByMonthClassAndCategory_When_AdminAsksForSummary()
    {
        var seeded = await Seed();
        var client = await Admin(seeded);

        var summary = await Json(await client.GetAsync($"{Route}/accuracy/summary?months=12"));

        var monthly = summary.GetProperty("monthly").EnumerateArray().ToList();
        monthly.Count.ShouldBe(12);
        monthly[0].GetProperty("month").GetString().ShouldBe("2025-10");
        monthly[0].GetProperty("mapePercent").ValueKind.ShouldBe(JsonValueKind.Null);
        monthly[0].GetProperty("productsMeasured").GetInt32().ShouldBe(0);
        var august = monthly.Single(m => m.GetProperty("month").GetString() == "2026-08");
        (august.GetProperty("mapePercent").GetDecimal(), august.GetProperty("productsMeasured").GetInt32()).ShouldBe((20m, 2));
        var september = monthly[^1];
        september.GetProperty("month").GetString().ShouldBe("2026-09");
        (september.GetProperty("mapePercent").GetDecimal(), september.GetProperty("productsMeasured").GetInt32()).ShouldBe((15m, 2));

        var byClass = summary.GetProperty("byAbcClass").EnumerateArray()
            .ToDictionary(c => c.GetProperty("abcClass").GetString()!, c => c.GetProperty("mapePercent").GetDecimal());
        byClass.ShouldBe(new Dictionary<string, decimal> { ["A"] = 10m, ["B"] = 25m });

        var byCategory = summary.GetProperty("byCategory").EnumerateArray().Single();
        byCategory.GetProperty("categoryPath").GetString().ShouldBe(seeded.CategoryPath);
        byCategory.GetProperty("mapePercent").GetDecimal().ShouldBe(17.5m);

        summary.GetProperty("withinTolerancePercent").GetDecimal().ShouldBe(50m);
    }

    [Fact]
    [Trait("Rule", "RN-DF19")]
    public async Task Should_ListProductsWorstFirst_When_AdminAsksForProducts()
    {
        var seeded = await Seed();
        var client = await Admin(seeded);

        var page = await Json(await client.GetAsync($"{Route}/accuracy/products?offset=0&limit=1"));

        page.GetProperty("total").GetInt32().ShouldBe(2);
        var worst = page.GetProperty("items").EnumerateArray().Single();
        worst.GetProperty("productId").GetGuid().ShouldBe(seeded.P2);
        worst.GetProperty("abcClass").GetString().ShouldBe("B");
        worst.GetProperty("monthsMeasured").GetInt32().ShouldBe(2);
        worst.GetProperty("mapePercent").GetDecimal().ShouldBe(25m);
        worst.GetProperty("bias").GetString().ShouldBe("Under");
        var last = worst.GetProperty("lastMonth");
        (last.GetProperty("month").GetString(), last.GetProperty("projected").GetDecimal(), last.GetProperty("actual").GetDecimal())
            .ShouldBe(("2026-09", 40m, 50m));

        var filters = Uri.EscapeDataString(JsonSerializer.Serialize(new { search = seeded.P1Name }));
        var searched = await Json(await client.GetAsync($"{Route}/accuracy/products?filters={filters}"));
        var p1 = searched.GetProperty("items").EnumerateArray().Single();
        p1.GetProperty("productId").GetGuid().ShouldBe(seeded.P1);
        (p1.GetProperty("mapePercent").GetDecimal(), p1.GetProperty("bias").GetString()).ShouldBe((10m, "Over"));
    }

    [Theory]
    [InlineData("summary")]
    [InlineData("products")]
    public async Task Should_Return403_When_UserIsNotAdmin(string route)
    {
        var seeded = await Seed();
        var admin = await Admin(seeded);
        var email = $"precisao.{route}@nextflow.com";
        var cpf = AccessApiFixture.ValidCpf(route == "summary" ? 300_000_001 : 300_000_002);
        (await admin.PostAsJsonAsync("/api/users", AccessApiFixture.UserBody(email, cpf))).EnsureSuccessStatusCode();
        // Todo usuário nasce Admin enquanto só existe esse perfil (RN-AC04); o perfil "User" é posto direto no banco.
        await using (var context = fixture.CreateContext(seeded.Connection))
            await context.Set<User>().Where(u => u.Email == email).ExecuteUpdateAsync(u => u.SetProperty(x => x.Role, RoleEnum.User));
        var user = await Login(seeded.Host, email, AccessApiFixture.Password);

        var response = await user.GetAsync($"{Route}/accuracy/{route}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Trait("Rule", "RN-DF19")]
    public async Task Should_FillAccuracy_When_ReadingProductForecast()
    {
        var seeded = await Seed();
        var client = await Admin(seeded);

        var forecast = await Json(await client.GetAsync($"{Route}/products/{seeded.P1}?months=24"));

        var accuracy = forecast.GetProperty("accuracy").EnumerateArray()
            .Select(a => (a.GetProperty("month").GetString(), a.GetProperty("projected").GetDecimal(), a.GetProperty("actual").GetDecimal(),
                a.GetProperty("errorPercent").GetDecimal()))
            .ToList();
        accuracy.ShouldBe([("2026-08", 110m, 100m, 10m), ("2026-09", 88m, 80m, 10m)]);
    }
}
