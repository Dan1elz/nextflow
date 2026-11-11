using System.Text.Json;
using Nextflow.Application.Forecast;
using Nextflow.Domain.Enums;
using Nextflow.Tests.Support;
using static Nextflow.Tests.Unit.Forecast.ForecastTestData;

namespace Nextflow.Tests.Unit.Forecast;

/// <summary>
/// O que sai do servidor para a IA (ADR 0005, LGPD art. 6º, III): só os campos de produto da lista fechada em
/// <see cref="AiPayloadContract"/>, com as vendas já agregadas por mês e nenhum dado de cliente ou usuário.
/// </summary>
public class ForecastAiPayloadTests
{
    private static readonly IReadOnlyList<ForecastEvent> Events = [new("Natal", new DateOnly(2026, 12, 25), 30)];

    /// <summary>Produto com todos os campos opcionais preenchidos, para nenhum ramo do payload ficar de fora.</summary>
    private static ProductForecastResult FullProduct() => ForecastCalculator.Calculate(
        Input(stock: 40, incoming: 10, minStock: 5, configure: b =>
        {
            b.NextExpiry = Today.AddDays(20);
            b.NextExpiryQuantity = 10;
            b.ActivePromotionDiscount = 10;
            b.Revenue = 5000;
        }),
        Context());

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    [Trait("Rule", "LGPD-ART6-III")]
    public void Should_SendOnlyContractFields_When_BuildingBatchPayload()
    {
        var payload = Parse(ForecastAiPrompt.BuildPayload(Context(), [FullProduct()], Events));

        AiPayloadContract.Keys(payload).ShouldBe(AiPayloadContract.BatchRoot, ignoreOrder: true);
        AiPayloadContract.Keys(payload.GetProperty("eventosComerciais")[0]).ShouldBe(AiPayloadContract.Event, ignoreOrder: true);

        var product = payload.GetProperty("produtos")[0];
        AiPayloadContract.Keys(product).ShouldBe(AiPayloadContract.Product, ignoreOrder: true);
        AiPayloadContract.Keys(product.GetProperty("promocaoVigente")).ShouldBe(AiPayloadContract.ActivePromotion, ignoreOrder: true);
        AiPayloadContract.Keys(product.GetProperty("sugestaoCalculada")).ShouldBe(AiPayloadContract.CalculatedSuggestion, ignoreOrder: true);

        // Vendas agregadas: no máximo 12 linhas, uma por mês, só com o mês e a quantidade.
        var sales = product.GetProperty("vendasUltimos12Meses").EnumerateArray().ToList();
        sales.Count.ShouldBeInRange(1, 12);
        sales.ShouldAllBe(s => AiPayloadContract.Keys(s).OrderBy(k => k).SequenceEqual(AiPayloadContract.MonthlySale.OrderBy(k => k)));

        AiPayloadContract.PersonalDataKeys(payload).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Rule", "LGPD-ART6-III")]
    public void Should_SendOnlyCountsAndTopItems_When_BuildingSummaryPayload()
    {
        var decisions = new List<(string, RecommendationActionEnum, RecommendationPriorityEnum, decimal?, decimal?, string)>
        {
            ("Panetone 500 g", RecommendationActionEnum.Buy, RecommendationPriorityEnum.High, 48m, null, "Cobertura abaixo do prazo."),
            ("Ovo de Páscoa 200 g", RecommendationActionEnum.Promote, RecommendationPriorityEnum.Medium, null, 15m, "Excesso em baixa temporada."),
            ("Bala de goma", RecommendationActionEnum.Hold, RecommendationPriorityEnum.Low, null, null, "Estoque suficiente.")
        };

        var payload = Parse(ForecastAiPrompt.BuildSummaryPayload(Context(), decisions, productsAnalyzed: 3, Events));

        AiPayloadContract.Keys(payload).ShouldBe(AiPayloadContract.SummaryRoot, ignoreOrder: true);
        var top = payload.GetProperty("principais").EnumerateArray().ToList();
        top.Count.ShouldBe(2); // "Manter" não entra nos principais
        top.ShouldAllBe(t => AiPayloadContract.Keys(t).OrderBy(k => k).SequenceEqual(AiPayloadContract.SummaryItem.OrderBy(k => k)));

        AiPayloadContract.PersonalDataKeys(payload).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Rule", "LGPD-ART6-III")]
    public void Should_HaveNoCustomerOrUserField_When_InspectingForecastInput()
    {
        // A filtragem é estrutural: a entrada da previsão nem tem onde guardar dado pessoal.
        var fields = typeof(ForecastProductInput).GetProperties().Select(p => p.Name);
        string[] hints = ["Client", "Customer", "User", "Cpf", "Cnpj", "Email", "Phone", "Address", "Supplier"];

        fields.Where(f => hints.Any(h => f.Contains(h, StringComparison.OrdinalIgnoreCase))).ShouldBeEmpty();
    }
}
