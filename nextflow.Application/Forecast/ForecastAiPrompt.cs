using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Nextflow.Domain.Enums;

namespace Nextflow.Application.Forecast;

/// <summary>
/// Prompt, payload (spec 6.3), schema de resposta (spec 6.4) e leitura da resposta da IA. A IA só prioriza e explica o que o
/// cálculo já decidiu (ADR 0005): os guardrails em <see cref="ForecastGuardrails"/> garantem os limites (RN-DF10 a RN-DF12).
/// </summary>
public static partial class ForecastAiPrompt
{
    public const string ActionBuy = "Comprar";
    public const string ActionPromote = "Promover";
    public const string ActionHold = "Manter";
    public const string PriorityHigh = "Alta";
    public const string PriorityMedium = "Media";
    public const string PriorityLow = "Baixa";

    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // mantém acentos legíveis (menos bytes e tokens)
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false
    };

    private static readonly JsonSerializerOptions ResponseJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public const string SystemPrompt = """
        Você é um analista sênior de compras e planejamento de demanda no varejo brasileiro, apoiando o gestor de uma
        loja dentro de um ERP. Você recebe, em JSON, a previsão de demanda já CALCULADA por um modelo estatístico
        (sazonalidade por mês, nível recente, estoque, estoque a chegar, prazo de entrega, validade e margem) e deve revisar a
        sugestão de cada produto, priorizar e explicar em português do Brasil.

        Regras obrigatórias:
        1. Use SOMENTE os números fornecidos. Não invente produtos, vendas, preços, datas ou eventos.
        2. Responda com exatamente um item em "recomendacoes" para cada produto do payload, usando o mesmo "produtoId".
        3. "sugestaoCalculada" é a base. Só mude a ação, a quantidade ou o desconto quando os dados justificarem
           (ex.: evento comercial no horizonte, validade próxima, cobertura muito abaixo do prazo de entrega, baixa temporada).
           A quantidade de compra deve ficar entre 50% e 150% da quantidade calculada; o desconto não pode passar de
           "descontoMaximoPct" nem ficar abaixo de 5; a promoção deve durar de 7 a 45 dias.
        4. "Comprar" exige necessidade de compra calculada (quantidade calculada > 0). Se não houver, use "Manter" ou "Promover".
           Produto "foraDeLinha" ou "foraDaEpoca" nunca é "Comprar".
        5. Não sugira "Promover" para produto com "promocaoVigente" preenchida, com "descontoMaximoPct" menor que 5
           ou que precise de compra.
        6. Considere os "eventosComerciais" do horizonte (ex.: Black Friday, Natal, Páscoa, Dia das Mães) e a validade dos produtos.
        7. Prioridade: "Alta" quando há risco de ruptura antes do próximo pico/entrega ou de perda por validade;
           "Media" para ações importantes sem urgência; "Baixa" para o restante. Produtos "Manter" normalmente são "Baixa".
        8. "motivo": no máximo 400 caracteres, direto, citando os números que embasam a decisão (estoque, cobertura em dias,
           prazo de entrega, demanda projetada, índice sazonal, validade, quantidade ou desconto). Se
           "projecaoBaseadaNoAnoAnterior" for true, diga que a projeção é baseada no mesmo período do ano anterior.
        9. Em "Manter", use quantidade, descontoPct e diasPromocao nulos. Em "Comprar", descontoPct e diasPromocao nulos.
           Em "Promover", quantidade nula e descontoPct em pontos percentuais (ex.: 15 = 15%).
        10. Nunca informe nem altere a confiança: ela é do cálculo.

        Escreva "motivo" em português do Brasil com acentuação e ortografia corretas.
        Responda apenas com o JSON no formato do schema, sem texto adicional.
        """;

    public const string SummarySystemPrompt = """
        Você é um analista sênior de compras no varejo brasileiro. Você recebe, em JSON, o resultado já fechado de uma análise
        de previsão de demanda (contagens por ação e os produtos mais importantes) e escreve o resumo geral para o gestor.

        Regras obrigatórias:
        1. Use SOMENTE os números e nomes fornecidos. Não invente produtos, vendas ou datas.
        2. "resumoGeral": no máximo 600 caracteres, em português do Brasil, com os principais destaques do horizonte
           (o que comprar primeiro, o que promover e por quê), citando produtos pelo nome.
        3. Considere os "eventosComerciais" do horizonte.
        Responda apenas com o JSON no formato do schema, sem texto adicional.
        """;

    // ------------------------------------------------------------------ produtos

    /// <summary>
    /// Ordena os produtos com venda para a IA (RN-DF09 deixa os sem venda de fora): candidatos Comprar e Promover primeiro, por
    /// prioridade; depois os maiores faturamentos dos últimos 12 meses. O fatiamento em lotes de N é do orquestrador (RN-DF20).
    /// </summary>
    public static List<ProductForecastResult> OrderForAi(IEnumerable<ProductForecastResult> results) => results
        .Where(r => r.HasSales)
        .OrderBy(r => r.Action == RecommendationActionEnum.Hold ? 1 : 0)
        .ThenByDescending(r => r.Action == RecommendationActionEnum.Hold ? 0 : (int)r.Priority)
        .ThenByDescending(r => r.Input.RevenueLast12Months)
        .ThenBy(r => r.Input.Name, StringComparer.Ordinal)
        .ToList();

    /// <summary>Payload JSON compacto de um lote (spec 6.3).</summary>
    public static string BuildPayload(
        ForecastCalculationContext ctx,
        IReadOnlyList<ProductForecastResult> products,
        IReadOnlyList<ForecastEvent> events)
    {
        var payload = new Dictionary<string, object?>
        {
            ["dataReferencia"] = ctx.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["horizonteMeses"] = ctx.HorizonMonths,
            ["mesesDoHorizonte"] = ctx.Horizon.Select(m => m.Key).ToList(),
            ["eventosComerciais"] = EventsPayload(events),
            ["produtos"] = products.Select(p => BuildProduct(p, ctx)).ToList()
        };

        return JsonSerializer.Serialize(payload, PayloadJsonOptions);
    }

    /// <summary>Payload da chamada final de resumo (RN-DF20): contagens e os produtos que mais importam, nunca o lote inteiro de novo.</summary>
    public static string BuildSummaryPayload(
        ForecastCalculationContext ctx,
        IReadOnlyList<(string Name, RecommendationActionEnum Action, RecommendationPriorityEnum Priority, decimal? Quantity, decimal? Discount, string Reason)> decisions,
        int productsAnalyzed,
        IReadOnlyList<ForecastEvent> events)
    {
        var top = decisions
            .Where(d => d.Action != RecommendationActionEnum.Hold)
            .OrderByDescending(d => (int)d.Priority)
            .ThenBy(d => d.Name, StringComparer.Ordinal)
            .Take(15)
            .Select(d => new Dictionary<string, object?>
            {
                ["nome"] = d.Name,
                ["acao"] = ActionLabel(d.Action),
                ["prioridade"] = PriorityLabel(d.Priority),
                ["quantidade"] = d.Quantity,
                ["descontoPct"] = d.Discount,
                ["motivo"] = Shorten(d.Reason, 200)
            })
            .ToList();

        var payload = new Dictionary<string, object?>
        {
            ["dataReferencia"] = ctx.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["horizonteMeses"] = ctx.HorizonMonths,
            ["mesesDoHorizonte"] = ctx.Horizon.Select(m => m.Key).ToList(),
            ["eventosComerciais"] = EventsPayload(events),
            ["produtosAnalisados"] = productsAnalyzed,
            ["comprar"] = decisions.Count(d => d.Action == RecommendationActionEnum.Buy),
            ["comprarPrioridadeAlta"] = decisions.Count(d => d.Action == RecommendationActionEnum.Buy && d.Priority == RecommendationPriorityEnum.High),
            ["promover"] = decisions.Count(d => d.Action == RecommendationActionEnum.Promote),
            ["manter"] = decisions.Count(d => d.Action == RecommendationActionEnum.Hold),
            ["principais"] = top
        };

        return JsonSerializer.Serialize(payload, PayloadJsonOptions);
    }

    private static List<Dictionary<string, object?>> EventsPayload(IReadOnlyList<ForecastEvent> events) => events
        .Select(e => new Dictionary<string, object?>
        {
            ["nome"] = e.Name,
            ["data"] = e.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["vendaComecaASubirEm"] = e.DemandStartsOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        })
        .ToList();

    private static Dictionary<string, object?> BuildProduct(ProductForecastResult r, ForecastCalculationContext ctx)
    {
        var input = r.Input;
        var sales = new List<Dictionary<string, object?>>();
        var series = input.MonthlySales;
        for (var i = Math.Max(0, series.Count - 12); i < series.Count; i++)
        {
            var month = ctx.LastCompleteMonth.AddMonths(i - (series.Count - 1));
            sales.Add(new Dictionary<string, object?> { ["mes"] = month.Key, ["qtd"] = R(series[i], 1) });
        }

        return new Dictionary<string, object?>
        {
            ["id"] = input.ProductId.ToString(),
            ["nome"] = input.Name,
            ["marca"] = input.Brand,
            ["categoria"] = input.ReferenceCategory,
            ["classeAbc"] = input.AbcClass?.GetApiName(),
            ["estoqueAtual"] = R(input.CurrentStock, 2),
            ["estoqueAChegar"] = R(input.IncomingStock, 2),
            ["estoqueMinimo"] = R(input.MinStock, 2),
            ["leadTimeDias"] = input.LeadTimeDays,
            ["preco"] = R(input.Price, 2),
            ["custo"] = input.CostPrice.HasValue ? R(input.CostPrice.Value, 2) : null,
            ["validade"] = input.NextExpiry?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["foraDeLinha"] = input.IsDiscontinued,
            ["foraDaEpoca"] = r.OffSeasonGuardActive,
            ["eventosDoProduto"] = input.LinkedEvents,
            ["promocaoVigente"] = input.ActivePromotionDiscountPercent.HasValue
                ? new Dictionary<string, object?>
                {
                    ["descontoPct"] = R(input.ActivePromotionDiscountPercent.Value, 2),
                    ["fim"] = input.ActivePromotionEndDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                }
                : null,
            ["vendasUltimos12Meses"] = sales,
            ["indiceSazonal"] = r.Projections.ToDictionary(p => p.Month.Key, p => (object?)R(p.SeasonalIndex, 2)),
            ["demandaProjetada"] = r.Projections.ToDictionary(p => p.Month.Key, p => (object?)R(p.Quantity, 1)),
            ["coberturaDias"] = r.CoverageDays.HasValue ? R(r.CoverageDays.Value, 1) : null,
            ["mesesDeHistorico"] = r.MonthsOfHistory,
            ["confianca"] = ConfidenceLabel(r.Confidence),
            ["projecaoBaseadaNoAnoAnterior"] = r.UsedSamePeriodLastYear,
            ["descontoMaximoPct"] = R(r.MaxDiscountPercent, 1),
            ["sugestaoCalculada"] = new Dictionary<string, object?>
            {
                ["acao"] = ActionLabel(r.Action),
                ["quantidade"] = r.SuggestedQuantity,
                ["descontoPct"] = r.SuggestedDiscountPercent,
                ["diasPromocao"] = r.SuggestedPromotionDays,
                ["prioridade"] = PriorityLabel(r.Priority),
                ["dataLimiteCompra"] = r.Deadline?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            }
        };
    }

    /// <summary>User prompt = instrução curta + payload JSON.</summary>
    public static string BuildUserPrompt(string payloadJson)
        => "Revise e explique as sugestões calculadas para os produtos abaixo (dados em JSON):\n" + payloadJson;

    public static string BuildSummaryUserPrompt(string payloadJson)
        => "Escreva o resumo geral desta análise (dados em JSON):\n" + payloadJson;

    // ------------------------------------------------------------------ schemas (OpenAPI subset do Gemini)

    /// <summary>Schema da resposta de um lote (seção 6.4): um item por produto, sem resumo (o resumo vem na chamada final).</summary>
    public static object ResponseSchema { get; } = new Dictionary<string, object>
    {
        ["type"] = "OBJECT",
        ["properties"] = new Dictionary<string, object>
        {
            ["recomendacoes"] = new Dictionary<string, object>
            {
                ["type"] = "ARRAY",
                ["description"] = "Exatamente um item por produto do payload.",
                ["items"] = new Dictionary<string, object>
                {
                    ["type"] = "OBJECT",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["produtoId"] = new Dictionary<string, object> { ["type"] = "STRING", ["description"] = "Mesmo id do produto no payload." },
                        ["acao"] = new Dictionary<string, object> { ["type"] = "STRING", ["enum"] = new[] { ActionBuy, ActionPromote, ActionHold } },
                        ["prioridade"] = new Dictionary<string, object> { ["type"] = "STRING", ["enum"] = new[] { PriorityHigh, PriorityMedium, PriorityLow } },
                        ["quantidade"] = new Dictionary<string, object> { ["type"] = "NUMBER", ["nullable"] = true, ["description"] = "Quantidade a comprar (somente Comprar)." },
                        ["descontoPct"] = new Dictionary<string, object> { ["type"] = "NUMBER", ["nullable"] = true, ["description"] = "Desconto em pontos percentuais, ex.: 15 (somente Promover)." },
                        ["diasPromocao"] = new Dictionary<string, object> { ["type"] = "INTEGER", ["nullable"] = true, ["description"] = "Duração da promoção em dias (somente Promover)." },
                        ["motivo"] = new Dictionary<string, object> { ["type"] = "STRING", ["description"] = "Explicação em pt-BR, até 400 caracteres, citando números." }
                    },
                    ["required"] = new[] { "produtoId", "acao", "prioridade", "motivo" },
                    ["propertyOrdering"] = new[] { "produtoId", "acao", "prioridade", "quantidade", "descontoPct", "diasPromocao", "motivo" }
                }
            }
        },
        ["required"] = new[] { "recomendacoes" },
        ["propertyOrdering"] = new[] { "recomendacoes" }
    };

    /// <summary>Schema da chamada final: só o resumo geral.</summary>
    public static object SummaryResponseSchema { get; } = new Dictionary<string, object>
    {
        ["type"] = "OBJECT",
        ["properties"] = new Dictionary<string, object>
        {
            ["resumoGeral"] = new Dictionary<string, object>
            {
                ["type"] = "STRING",
                ["description"] = "Resumo geral da análise em pt-BR (até 600 caracteres)."
            }
        },
        ["required"] = new[] { "resumoGeral" },
        ["propertyOrdering"] = new[] { "resumoGeral" }
    };

    // ------------------------------------------------------------------ resposta

    /// <summary>Lê a resposta da IA (tolerante a cercas de markdown e texto em volta do JSON).</summary>
    /// <exception cref="FormatException">JSON ausente ou inválido.</exception>
    public static AiForecastResponse ParseResponse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new FormatException("Resposta da IA vazia.");

        var json = ExtractJson(raw);
        try
        {
            var response = JsonSerializer.Deserialize<AiForecastResponse>(json, ResponseJsonOptions)
                ?? throw new FormatException("Resposta da IA vazia.");
            response.Recomendacoes ??= [];
            return response;
        }
        catch (JsonException ex)
        {
            throw new FormatException($"JSON inválido na resposta da IA: {ex.Message}", ex);
        }
    }

    /// <summary>Remove cercas ```json ... ``` e recorta do primeiro '{' ao último '}'.</summary>
    public static string ExtractJson(string raw)
    {
        var text = raw.Trim();
        var fence = FenceRegex().Match(text);
        if (fence.Success)
            text = fence.Groups[1].Value.Trim();

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
            throw new FormatException("A resposta da IA não contém um objeto JSON.");

        return text[start..(end + 1)];
    }

    [GeneratedRegex(@"```(?:json)?\s*([\s\S]*?)```", RegexOptions.IgnoreCase)]
    private static partial Regex FenceRegex();

    /// <summary>Lê a ação da IA. Transferir não existe no schema (F3.4b): qualquer outro texto vira nulo e o produto usa o cálculo.</summary>
    public static RecommendationActionEnum? ParseAction(string? value) => Normalize(value) switch
    {
        "comprar" or "buy" => RecommendationActionEnum.Buy,
        "promover" or "promote" => RecommendationActionEnum.Promote,
        "manter" or "hold" => RecommendationActionEnum.Hold,
        _ => null
    };

    public static RecommendationPriorityEnum? ParsePriority(string? value) => Normalize(value) switch
    {
        "alta" or "high" => RecommendationPriorityEnum.High,
        "media" or "medium" => RecommendationPriorityEnum.Medium,
        "baixa" or "low" => RecommendationPriorityEnum.Low,
        _ => null
    };

    public static string ActionLabel(RecommendationActionEnum action) => action switch
    {
        RecommendationActionEnum.Buy => ActionBuy,
        RecommendationActionEnum.Promote => ActionPromote,
        _ => ActionHold
    };

    public static string PriorityLabel(RecommendationPriorityEnum priority) => priority switch
    {
        RecommendationPriorityEnum.High => PriorityHigh,
        RecommendationPriorityEnum.Medium => PriorityMedium,
        _ => PriorityLow
    };

    public static string ConfidenceLabel(ForecastConfidenceEnum confidence) => confidence switch
    {
        ForecastConfidenceEnum.High => "Alta",
        ForecastConfidenceEnum.Medium => "Média",
        _ => "Baixa"
    };

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        // Sem string.Normalize (pode não estar disponível com globalização invariante).
        var sb = new StringBuilder(value.Length);
        foreach (var c in value.Trim().ToLowerInvariant())
        {
            sb.Append(c switch
            {
                'á' or 'à' or 'â' or 'ã' => 'a',
                'é' or 'ê' => 'e',
                'í' => 'i',
                'ó' or 'ô' or 'õ' => 'o',
                'ú' => 'u',
                'ç' => 'c',
                _ => c
            });
        }
        return sb.ToString();
    }

    private static string Shorten(string value, int max) => value.Length <= max ? value : value[..(max - 1)].TrimEnd() + "…";

    private static decimal R(decimal value, int decimals) => Math.Round(value, decimals, MidpointRounding.AwayFromZero);
}
