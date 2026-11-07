using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Utils;

namespace Nextflow.Application.Forecast;

/// <summary>
/// Prompt, payload (spec 6.3), responseSchema (spec 6.4) e leitura da resposta da IA para a previsão de demanda.
/// </summary>
public static partial class ForecastAiPrompt
{
    /// <summary>Máximo de produtos enviados à IA (candidatos Comprar/Promover primeiro, depois maiores receitas).</summary>
    public const int MaxProducts = 40;

    public const string ActionBuy = "Comprar";
    public const string ActionPromote = "Promover";
    public const string ActionHold = "Manter";
    public const string PriorityHigh = "Alta";
    public const string PriorityMedium = "Media";
    public const string PriorityLow = "Baixa";

    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // mantém acentos legíveis (menos bytes/tokens)
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
        distribuidora/loja dentro de um ERP. Você recebe, em JSON, a previsão de demanda já CALCULADA por um modelo
        estatístico (sazonalidade por mês, nível recente, estoque, estoque a chegar, prazo de entrega, validade e margem)
        e deve revisar a sugestão de cada produto, priorizar e explicar em português do Brasil.

        Regras obrigatórias:
        1. Use SOMENTE os números fornecidos. Não invente produtos, vendas, preços, datas ou eventos.
        2. Responda com exatamente um item em "recomendacoes" para cada produto do payload, usando o mesmo "produtoId".
        3. "sugestaoCalculada" é a base. Só mude a ação, a quantidade ou o desconto quando os dados justificarem
           (ex.: evento comercial no horizonte, validade próxima, cobertura muito abaixo do prazo de entrega, baixa temporada).
           A quantidade de compra deve ficar entre 50% e 150% da quantidade calculada; o desconto não pode passar de
           "descontoMaximoPct" nem ficar abaixo de 5; a promoção deve durar de 7 a 45 dias.
        4. "Comprar" exige necessidade de compra calculada (quantidade calculada > 0). Se não houver, use "Manter" ou "Promover".
        5. Não sugira "Promover" para produto com "promocaoVigente" preenchida, com "descontoMaximoPct" menor que 5
           ou que precise de compra.
        6. Considere os "eventosComerciais" do horizonte (ex.: Black Friday, Natal, Páscoa, Dia das Mães) e a validade dos produtos.
        7. Prioridade: "Alta" quando há risco de ruptura antes do próximo pico/entrega ou de perda por validade;
           "Media" para ações importantes sem urgência; "Baixa" para o restante. Produtos "Manter" normalmente são "Baixa".
        8. "motivo": no máximo 400 caracteres, direto, citando os números que embasam a decisão (estoque, cobertura em dias,
           prazo de entrega, demanda projetada, índice sazonal, validade, quantidade ou desconto). Se
           "projecaoBaseadaNoAnoAnterior" for true, diga que a projeção é baseada no mesmo período do ano anterior.
        9. "resumoGeral": no máximo 600 caracteres, com os principais destaques do horizonte (o que comprar primeiro,
           o que promover e por quê), citando produtos pelo nome.
        10. Em "Manter", use quantidade, descontoPct e diasPromocao nulos. Em "Comprar", descontoPct e diasPromocao nulos.
            Em "Promover", quantidade nula e descontoPct em pontos percentuais (ex.: 15 = 15%).

        Escreva "motivo" e "resumoGeral" em português do Brasil com acentuação e ortografia corretas.
        Responda apenas com o JSON no formato do schema, sem texto adicional.
        """;

    // ------------------------------------------------------------------ payload

    /// <summary>
    /// Seleciona os produtos enviados à IA (com vendas; candidatos Comprar/Promover primeiro, por prioridade;
    /// depois maiores receitas dos últimos 12 meses; máx. <see cref="MaxProducts"/>).
    /// </summary>
    public static List<ProductForecastResult> SelectProducts(IEnumerable<ProductForecastResult> results) => results
        .Where(r => r.HasSales)
        .OrderBy(r => r.Action == RecommendationAction.Hold ? 1 : 0)
        .ThenByDescending(r => r.Action == RecommendationAction.Hold ? 0 : (int)r.Priority)
        .ThenByDescending(r => r.RevenueLast12Months)
        .ThenBy(r => r.Input.Name)
        .Take(MaxProducts)
        .ToList();

    /// <summary>Monta o payload JSON compacto da seção 6.3 (também gravado em <c>ForecastRun.InputSnapshotJson</c>).</summary>
    public static string BuildPayload(
        ForecastCalculationContext ctx,
        IReadOnlyList<ProductForecastResult> products,
        IReadOnlyList<CommercialEvent> events)
    {
        var payload = new Dictionary<string, object?>
        {
            ["dataReferencia"] = ctx.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["horizonteMeses"] = ctx.HorizonMonths,
            ["mesesDoHorizonte"] = ctx.Horizon.Select(m => m.Key).ToList(),
            ["eventosComerciais"] = events
                .Select(e => new Dictionary<string, object?>
                {
                    ["nome"] = e.Name,
                    ["data"] = e.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                })
                .ToList(),
            ["produtos"] = products.Select(p => BuildProduct(p, ctx)).ToList()
        };

        return JsonSerializer.Serialize(payload, PayloadJsonOptions);
    }

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
            ["estoqueAtual"] = R(input.CurrentStock, 2),
            ["estoqueAChegar"] = R(input.IncomingStock, 2),
            ["estoqueMinimo"] = R(input.MinStock, 2),
            ["leadTimeDias"] = input.LeadTimeDays,
            ["preco"] = R(input.Price, 2),
            ["custo"] = input.CostPrice.HasValue ? R(input.CostPrice.Value, 2) : null,
            ["validade"] = input.Validity?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
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

    // ------------------------------------------------------------------ responseSchema (OpenAPI subset do Gemini)

    /// <summary>Schema da resposta (seção 6.4) no formato OpenAPI-subset aceito pelo Gemini (tipos em maiúsculas).</summary>
    public static object ResponseSchema { get; } = new Dictionary<string, object>
    {
        ["type"] = "OBJECT",
        ["properties"] = new Dictionary<string, object>
        {
            ["resumoGeral"] = new Dictionary<string, object>
            {
                ["type"] = "STRING",
                ["description"] = "Resumo geral da análise em pt-BR (até 600 caracteres)."
            },
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
        ["required"] = new[] { "resumoGeral", "recomendacoes" },
        ["propertyOrdering"] = new[] { "resumoGeral", "recomendacoes" }
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

    public static RecommendationAction? ParseAction(string? value) => Normalize(value) switch
    {
        "comprar" or "buy" => RecommendationAction.Buy,
        "promover" or "promote" => RecommendationAction.Promote,
        "manter" or "hold" => RecommendationAction.Hold,
        _ => null
    };

    public static RecommendationPriority? ParsePriority(string? value) => Normalize(value) switch
    {
        "alta" or "high" => RecommendationPriority.High,
        "media" or "medium" => RecommendationPriority.Medium,
        "baixa" or "low" => RecommendationPriority.Low,
        _ => null
    };

    public static string ActionLabel(RecommendationAction action) => action switch
    {
        RecommendationAction.Buy => ActionBuy,
        RecommendationAction.Promote => ActionPromote,
        _ => ActionHold
    };

    public static string PriorityLabel(RecommendationPriority priority) => priority switch
    {
        RecommendationPriority.High => PriorityHigh,
        RecommendationPriority.Medium => PriorityMedium,
        _ => PriorityLow
    };

    public static string ConfidenceLabel(ForecastConfidence confidence) => confidence switch
    {
        ForecastConfidence.High => "Alta",
        ForecastConfidence.Medium => "Média",
        _ => "Baixa"
    };

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        // sem string.Normalize (pode não estar disponível com globalização invariante)
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

    private static decimal R(decimal value, int decimals) => Math.Round(value, decimals, MidpointRounding.AwayFromZero);
}

/// <summary>Resposta da IA (seção 6.4).</summary>
public sealed class AiForecastResponse
{
    public string? ResumoGeral { get; set; }
    public List<AiRecommendationItem>? Recomendacoes { get; set; }
}

public sealed class AiRecommendationItem
{
    public string? ProdutoId { get; set; }
    public string? Acao { get; set; }
    public string? Prioridade { get; set; }
    public decimal? Quantidade { get; set; }
    public decimal? DescontoPct { get; set; }

    /// <summary>decimal para tolerar "14.0" vindo do modelo.</summary>
    public decimal? DiasPromocao { get; set; }
    public string? Motivo { get; set; }
}
