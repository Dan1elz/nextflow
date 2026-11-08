using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Nextflow.Application.Forecast;
using Nextflow.Domain.Enums;
using Nextflow.Tests.Support;
using static Nextflow.Tests.Unit.Forecast.ForecastTestData;

namespace Nextflow.Tests.Unit.Forecast;

/// <summary>Orquestração da IA em lotes sequenciais (RN-DF20), sempre com a IA fake: nenhum teste chama a IA real.</summary>
public class ForecastAiOrchestratorTests
{
    private static readonly IReadOnlyList<ForecastEvent> NoEvents = [];

    private readonly FakeAiClient _ai = new();

    private ForecastAiOrchestrator Orchestrator() => new(_ai, NullLogger<ForecastAiOrchestrator>.Instance);

    private static List<ProductForecastResult> Products(int count)
        => [.. Enumerable.Range(1, count).Select(i =>
            ForecastCalculator.Calculate(Input(name: $"Produto {i:D3}", configure: b => b.Revenue = 1000 - i), Context()))];

    private static IReadOnlyList<IReadOnlyList<ProductForecastResult>> Batches(IReadOnlyList<ProductForecastResult> products, int size = 40)
        => [.. ForecastAiOrchestrator.Split(ForecastAiPrompt.OrderForAi(products), size).Select(b => (IReadOnlyList<ProductForecastResult>)b)];

    [Fact]
    [Trait("Rule", "RN-DF20")]
    public async Task Should_SendSequentialBatchesOf40_When_ThereAreMoreProducts()
    {
        var products = Products(95);

        var result = await Orchestrator().AnalyzeAsync(Context(), Batches(products), NoEvents, null, CancellationToken.None);

        _ai.BatchProductIds.Select(b => b.Count).ShouldBe([40, 40, 15]);
        _ai.BatchProductIds.SelectMany(b => b).Distinct().Count().ShouldBe(95); // cada produto vai uma vez só
        _ai.MaxConcurrentCalls.ShouldBe(1);
        result.Batches.Select(b => b.Number).ShouldBe([1, 2, 3]);
        result.Batches.ShouldAllBe(b => b.Status == AiBatchStatusEnum.Completed);
        result.Items.Count.ShouldBe(95);
        result.UsedAi.ShouldBeTrue();
        result.Error.ShouldBeNull();
    }

    [Fact]
    [Trait("Rule", "RN-DF20")]
    public async Task Should_WaitForPreviousBatch_When_SendingNext()
    {
        var events = new List<string>();

        await Orchestrator().AnalyzeAsync(Context(), Batches(Products(90)), NoEvents,
            (report, _) =>
            {
                events.Add($"{report.Number}:{report.Status}");
                return Task.CompletedTask;
            },
            CancellationToken.None);

        events.ShouldBe(["1:Sent", "1:Completed", "2:Sent", "2:Completed", "3:Sent", "3:Completed"]);
    }

    [Fact]
    [Trait("Rule", "RN-DF20")]
    public async Task Should_FallBackAndContinue_When_BatchFails()
    {
        _ai.FailBatches.Add(2);
        var products = Products(95);
        var batches = Batches(products);

        var result = await Orchestrator().AnalyzeAsync(Context(), batches, NoEvents, null, CancellationToken.None);

        result.Batches.Select(b => b.Status).ShouldBe([AiBatchStatusEnum.Completed, AiBatchStatusEnum.Failed, AiBatchStatusEnum.Completed]);
        result.Batches[1].Error!.ShouldContain("lote 2");
        var failedIds = batches[1].Select(p => p.Input.ProductId).ToHashSet();
        result.Items.Keys.Any(failedIds.Contains).ShouldBeFalse(); // os do lote com falha ficam sem item: usam o cálculo
        result.Items.Count.ShouldBe(55);
        result.UsedAi.ShouldBeTrue();
        result.Error!.ShouldContain("Lote 2");
    }

    [Fact]
    [Trait("Rule", "RN-DF13")]
    public async Task Should_NotCallAi_When_NotConfigured()
    {
        _ai.Configured = false;

        var result = await Orchestrator().AnalyzeAsync(Context(), Batches(Products(10)), NoEvents, null, CancellationToken.None);

        _ai.Calls.ShouldBeEmpty();
        result.Batches.ShouldBeEmpty();
        result.Items.ShouldBeEmpty();
        result.UsedAi.ShouldBeFalse();
        result.Error!.ShouldContain("IA não configurada");
        (await Orchestrator().SummarizeAsync(Context(), [], 10, NoEvents, CancellationToken.None)).ShouldBeNull();
        _ai.Calls.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Rule", "RN-DF13")]
    public async Task Should_NotCallAi_When_NoProductHasSales()
    {
        var result = await Orchestrator().AnalyzeAsync(Context(), [], NoEvents, null, CancellationToken.None);

        _ai.Calls.ShouldBeEmpty();
        result.UsedAi.ShouldBeFalse();
        result.Error!.ShouldContain("Nenhum produto com vendas");
    }

    [Fact]
    [Trait("Rule", "RN-DF10")]
    public async Task Should_IgnoreProduct_When_AiAnswersOutsideTheBatch()
    {
        var products = Products(60);
        var batches = Batches(products);
        var stranger = Guid.NewGuid();
        var otherBatchProduct = batches[1][0].Input.ProductId;
        _ai.BatchResponse = (number, ids) => number != 1 ? null : JsonSerializer.Serialize(new
        {
            recomendacoes = new object[]
            {
                new { produtoId = ids[0].ToString(), acao = "Manter", prioridade = "Baixa", motivo = "ok" },
                new { produtoId = stranger.ToString(), acao = "Comprar", prioridade = "Alta", quantidade = 10, motivo = "inventado" },
                new { produtoId = otherBatchProduct.ToString(), acao = "Comprar", prioridade = "Alta", quantidade = 10, motivo = "do lote 2" },
            }
        });

        var result = await Orchestrator().AnalyzeAsync(Context(), batches, NoEvents, null, CancellationToken.None);

        result.Items.ContainsKey(stranger).ShouldBeFalse();
        result.Items[otherBatchProduct].Motivo.ShouldNotBe("do lote 2"); // vale o item do próprio lote 2, não o do lote 1
        result.Items.Count.ShouldBe(21); // 1 válido do lote 1 + os 20 do lote 2
        result.Items[IdsOf(batches, 0)[0]].Motivo.ShouldBe("ok");
    }

    private static List<Guid> IdsOf(IReadOnlyList<IReadOnlyList<ProductForecastResult>> batches, int index)
        => [.. batches[index].Select(p => p.Input.ProductId)];

    [Fact]
    [Trait("Rule", "RN-DF20")]
    public async Task Should_FailBatch_When_ResponseIsNotJson()
    {
        _ai.BatchResponse = (_, _) => "isto não é json";

        var result = await Orchestrator().AnalyzeAsync(Context(), Batches(Products(5)), NoEvents, null, CancellationToken.None);

        result.Batches.Single().Status.ShouldBe(AiBatchStatusEnum.Failed);
        result.UsedAi.ShouldBeFalse();
    }

    [Fact]
    [Trait("Rule", "RN-DF20")]
    public async Task Should_SummarizeInFinalCall_When_BatchesAreDone()
    {
        var products = Products(5);
        await Orchestrator().AnalyzeAsync(Context(), Batches(products), NoEvents, null, CancellationToken.None);

        var summary = await Orchestrator().SummarizeAsync(
            Context(),
            [("Produto 001", RecommendationActionEnum.Buy, RecommendationPriorityEnum.High, 157m, null, "Motivo")],
            5, NoEvents, CancellationToken.None);

        summary.ShouldBe(FakeAiClient.SummaryText);
        _ai.Calls.ShouldBe(["batch:1", "summary"]);
    }

    [Fact]
    [Trait("Rule", "RN-DF13")]
    public async Task Should_ReturnNull_When_SummaryCallFails()
    {
        _ai.FailSummary = true;

        var summary = await Orchestrator().SummarizeAsync(Context(), [], 5, NoEvents, CancellationToken.None);

        summary.ShouldBeNull();
    }

    [Fact]
    [Trait("Rule", "RN-DF20")]
    public async Task Should_PropagateCancellation_When_TokenIsCancelled()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() =>
            Orchestrator().AnalyzeAsync(Context(), Batches(Products(5)), NoEvents, null, cts.Token));
    }

    [Fact]
    public void Should_Reject_When_BatchSizeIsBelowOne()
        => Should.Throw<ArgumentOutOfRangeException>(() => ForecastAiOrchestrator.Split(Products(3), 0));

    [Fact]
    [Trait("Rule", "RN-DF09")]
    public void Should_LeaveProductOutOfTheAi_When_ItHasNoSales()
    {
        var noSales = ForecastCalculator.Calculate(Input(Series((_, _) => 0m), monthsOfHistory: 0, name: "Sem venda"), Context());

        ForecastAiPrompt.OrderForAi([noSales, .. Products(2)]).Select(p => p.Input.Name).ShouldNotContain("Sem venda");
    }

    [Fact]
    [Trait("Rule", "RN-DF20")]
    public void Should_SendBuyAndPromoteFirst_When_OrderingForTheAi()
    {
        var hold = ForecastCalculator.Calculate(Input(stock: 1000, name: "Mantém", configure: b => b.Revenue = 9999), Context());
        var buy = ForecastCalculator.Calculate(Input(name: "Compra", configure: b => b.Revenue = 1), Context());

        ForecastAiPrompt.OrderForAi([hold, buy]).Select(p => p.Input.Name).ShouldBe(["Compra", "Mantém"]);
    }

    [Fact]
    public void Should_ParseFencedResponse_When_ModelWrapsJsonInMarkdown()
    {
        var response = ForecastAiPrompt.ParseResponse("```json\n{\"recomendacoes\":[{\"produtoId\":\"x\",\"acao\":\"Comprar\",\"diasPromocao\":\"14.0\"}]}\n```");

        response.Recomendacoes!.Single().DiasPromocao.ShouldBe(14m);
        Should.Throw<FormatException>(() => ForecastAiPrompt.ParseResponse("sem json"));
    }
}
