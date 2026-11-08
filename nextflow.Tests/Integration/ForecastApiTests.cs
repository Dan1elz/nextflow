using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nextflow.Domain.Dtos.Forecasts;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Models;

namespace Nextflow.Tests.Integration;

/// <summary>
/// Análise em segundo plano (F3.3) pela API, no Postgres real: disparo manual, andamento, histórico, recomendações, 409 com análise
/// em andamento (RN-DF23), agendamento (T-DF11) e o disparo semanal (RN-DF18). Sem chave de IA: termina <c>CompletedWithoutAi</c>.
/// </summary>
[Collection(AccessApiCollection.Name)]
public class ForecastApiTests(AccessApiFixture api)
{
    private const string Route = "/api/forecasts";
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    private static async Task<JsonElement> Json(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<JsonElement>();

    /// <summary>Consulta o andamento a cada 300 ms até a análise sair de Running (o front faz o mesmo a cada 5 s, L-DF1).</summary>
    private static async Task<JsonElement> WaitUntilFinished(HttpClient client, Guid runId)
    {
        var started = DateTime.UtcNow;
        while (true)
        {
            var progress = await Json(await client.GetAsync($"{Route}/{runId}/status"));
            if (progress.GetProperty("status").GetString() != "Running")
                return progress;
            if (DateTime.UtcNow - started > Timeout)
                throw new TimeoutException($"A análise {runId} não terminou em {Timeout}.");
            await Task.Delay(300);
        }
    }

    private async Task<Guid> WaitForNoRunningAnalysis()
    {
        var started = DateTime.UtcNow;
        while (true)
        {
            await using var context = api.CreateContext();
            var running = await context.Set<ForecastRun>().Where(r => r.Status == ForecastRunStatusEnum.Running).Select(r => r.Id).ToListAsync();
            if (running.Count == 0)
                return Guid.Empty;
            if (DateTime.UtcNow - started > Timeout)
                throw new TimeoutException("Ficou análise em andamento.");
            await Task.Delay(300);
        }
    }

    [Fact]
    [Trait("Rule", "RN-DF23")]
    public async Task Should_RunInBackground_When_Generating()
    {
        await WaitForNoRunningAnalysis();
        var client = await api.AdminAsync();

        var response = await client.PostAsync($"{Route}/generate", null);

        // Devolve na hora, com a análise em andamento; o job termina sozinho (L-DF1).
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var created = await Json(response);
        // Em geral ainda Running; num banco pequeno o job pode terminar antes da releitura.
        created.GetProperty("status").GetString().ShouldBeOneOf("Running", "CompletedWithoutAi");
        created.GetProperty("trigger").GetString().ShouldBe("Manual");
        created.GetProperty("requestedByName").GetString().ShouldNotBeNullOrEmpty();
        var runId = created.GetProperty("id").GetGuid();

        var progress = await WaitUntilFinished(client, runId);
        progress.GetProperty("status").GetString().ShouldBe("CompletedWithoutAi");
        progress.GetProperty("percent").GetInt32().ShouldBe(100);

        var latest = await Json(await client.GetAsync($"{Route}/latest"));
        latest.GetProperty("id").GetGuid().ShouldBe(runId);
        latest.GetProperty("horizonMonths").GetInt32().ShouldBeInRange(3, 6);
        latest.GetProperty("horizonStart").GetString().ShouldNotBeNull();
        latest.GetProperty("summary").GetString().ShouldNotBeNullOrEmpty();
        var analyzed = latest.GetProperty("productsAnalyzed").GetInt32();
        var counts = latest.GetProperty("counts");
        (counts.GetProperty("buy").GetInt32() + counts.GetProperty("promote").GetInt32() + counts.GetProperty("transfer").GetInt32()
            + counts.GetProperty("hold").GetInt32()).ShouldBe(analyzed);
        counts.GetProperty("pending").GetInt32().ShouldBe(analyzed);

        var history = await Json(await client.GetAsync($"{Route}?limit=50"));
        history.GetProperty("items").EnumerateArray().Select(r => r.GetProperty("id").GetGuid()).ShouldContain(runId);

        (await Json(await client.GetAsync($"{Route}/{runId}/batches"))).GetArrayLength().ShouldBe(0); // sem IA, sem lote

        // RN-DF18/RN-DF23: ao terminar, o sino do usuário avisa.
        var bell = await Json(await client.GetAsync("/api/notifications?limit=50"));
        bell.GetProperty("items").EnumerateArray()
            .ShouldContain(n => n.GetProperty("type").GetString() == "NewRecommendations" && n.GetProperty("link").GetString() == "/forecast");

        var page = await Json(await client.GetAsync($"{Route}/{runId}/recommendations?limit=5"));
        page.GetProperty("total").GetInt32().ShouldBe(analyzed);
        if (analyzed > 0)
        {
            var first = page.GetProperty("items")[0];
            first.GetProperty("forecastRunId").GetGuid().ShouldBe(runId);
            first.GetProperty("status").GetString().ShouldBe("Pending");
            first.GetProperty("reasonShort").GetString().ShouldNotBeNullOrEmpty();
        }
    }

    [Fact]
    [Trait("Rule", "RN-DF23")]
    public async Task Should_Return409_When_AnalysisIsAlreadyRunning()
    {
        await WaitForNoRunningAnalysis();
        var client = await api.AdminAsync();
        var running = new ForecastRun(new StartForecastRunDto(ForecastTriggerEnum.Scheduled, null), DateOnly.FromDateTime(DateTime.UtcNow), DateTime.UtcNow);
        await using (var context = api.CreateContext())
        {
            context.Add(running);
            await context.SaveChangesAsync();
        }

        try
        {
            var response = await client.PostAsync($"{Route}/generate", null);

            response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            (await Json(response)).GetProperty("errors")[0].GetString()!.ShouldContain("andamento");
            (await Json(await client.GetAsync($"{Route}/{running.Id}/status"))).GetProperty("percent").GetInt32().ShouldBe(0);
        }
        finally
        {
            await using var context = api.CreateContext();
            var run = await context.Set<ForecastRun>().SingleAsync(r => r.Id == running.Id);
            run.Fail("Limpeza do teste.", DateTime.UtcNow);
            await context.SaveChangesAsync();
        }
    }

    [Fact]
    [Trait("Rule", "RN-DF23")]
    public async Task Should_FailOrphanRun_When_ApiRestarts()
    {
        await WaitForNoRunningAnalysis();
        var orphan = new ForecastRun(new StartForecastRunDto(ForecastTriggerEnum.Manual, null), DateOnly.FromDateTime(DateTime.UtcNow), DateTime.UtcNow);
        await using (var context = api.CreateContext())
        {
            context.Add(orphan);
            await context.SaveChangesAsync();
        }

        // Subir outro host é um restart da API: o job fecha a análise que ficou em andamento.
        await using (var host = api.HostWithClock(DateOnly.FromDateTime(DateTime.UtcNow)))
        {
            using var scope = host.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IForecastRunProcessor>().FailInterruptedAsync(CancellationToken.None);
        }

        await using var check = api.CreateContext();
        var run = await check.Set<ForecastRun>().SingleAsync(r => r.Id == orphan.Id);
        run.Status.ShouldBe(ForecastRunStatusEnum.Failed);
        run.ErrorMessage!.ShouldContain("reiniciou");
    }

    [Fact]
    public async Task Should_Return404_When_RunDoesNotExist()
    {
        var client = await api.AdminAsync();
        var missing = Guid.NewGuid();

        (await client.GetAsync($"{Route}/{missing}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync($"{Route}/{missing}/status")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync($"{Route}/{missing}/batches")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync($"{Route}/{missing}/recommendations")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Rule", "RN-DF18")]
    public async Task Should_SaveSchedule_When_SettingsAreValid()
    {
        var client = await api.AdminAsync();
        const string settingsRoute = "/api/settings/forecast";

        try
        {
            var defaults = await Json(await client.GetAsync(settingsRoute));
            defaults.GetProperty("aiConfigured").GetBoolean().ShouldBeFalse();

            var invalid = await client.PutAsJsonAsync(settingsRoute, new { scheduleEnabled = true, weekday = 9, time = "25:00", aiBatchSize = 0 });
            invalid.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            (await Json(invalid)).GetProperty("fieldErrors").EnumerateObject().Select(f => f.Name)
                .ShouldBe(["weekday", "time", "aiBatchSize"], ignoreOrder: true);

            var saved = await client.PutAsJsonAsync(settingsRoute, new { scheduleEnabled = true, weekday = 5, time = "22:30", aiBatchSize = 25 });
            saved.StatusCode.ShouldBe(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());

            var reread = await Json(await client.GetAsync(settingsRoute));
            reread.GetProperty("weekday").GetInt32().ShouldBe(5);
            reread.GetProperty("time").GetString().ShouldBe("22:30");
            reread.GetProperty("aiBatchSize").GetInt32().ShouldBe(25);
            reread.GetProperty("nextScheduledAt").GetDateTime().DayOfWeek.ShouldBeOneOf(DayOfWeek.Friday, DayOfWeek.Saturday); // 22:30 local = 01:30 UTC

            var off = await Json(await client.PutAsJsonAsync(settingsRoute, new { scheduleEnabled = false, weekday = 1, time = "06:00", aiBatchSize = 40 }));
            off.GetProperty("nextScheduledAt").ValueKind.ShouldBe(JsonValueKind.Null);
        }
        finally
        {
            await client.PutAsJsonAsync(settingsRoute, new { scheduleEnabled = true, weekday = 1, time = "06:00", aiBatchSize = 40 });
        }
    }

    [Fact]
    [Trait("Rule", "RN-DF18")]
    public async Task Should_StartScheduledRun_When_WeeklySlotPasses()
    {
        await WaitForNoRunningAnalysis();
        var client = await api.AdminAsync();
        await using var host = api.HostWithClock(DateOnly.FromDateTime(DateTime.UtcNow));
        var clock = host.Services.GetRequiredService<IBusinessClock>();
        var now = clock.Now;
        // Horário de hoje à 00:00 local: o último horário agendado já passou.
        await client.PutAsJsonAsync("/api/settings/forecast", new { scheduleEnabled = true, weekday = (int)now.DayOfWeek, time = "00:00", aiBatchSize = 40 });
        var slot = now.Date;

        try
        {
            DateTime handled;
            using (var scope = host.Services.CreateScope())
                handled = await scope.ServiceProvider.GetRequiredService<IForecastScheduleService>().CheckAsync(slot.AddMinutes(-1), CancellationToken.None);

            handled.ShouldBeGreaterThanOrEqualTo(slot);
            await WaitForNoRunningAnalysis();
            await using (var context = api.CreateContext())
            {
                var scheduled = await context.Set<ForecastRun>().OrderByDescending(r => r.StartedAt).FirstAsync();
                scheduled.Trigger.ShouldBe(ForecastTriggerEnum.Scheduled);
                scheduled.RequestedById.ShouldBeNull();
                scheduled.Status.ShouldBe(ForecastRunStatusEnum.CompletedWithoutAi);
            }

            // Mesmo horário já tratado: não abre outra.
            int before;
            await using (var context = api.CreateContext())
                before = await context.Set<ForecastRun>().CountAsync();
            using (var scope = host.Services.CreateScope())
                (await scope.ServiceProvider.GetRequiredService<IForecastScheduleService>().CheckAsync(handled, CancellationToken.None)).ShouldBe(handled);
            await using (var context = api.CreateContext())
                (await context.Set<ForecastRun>().CountAsync()).ShouldBe(before);
        }
        finally
        {
            await client.PutAsJsonAsync("/api/settings/forecast", new { scheduleEnabled = true, weekday = 1, time = "06:00", aiBatchSize = 40 });
        }
    }
}
