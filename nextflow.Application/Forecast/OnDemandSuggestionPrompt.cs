using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nextflow.Application.Forecast;

/// <summary>Resposta da IA na sugestão sob demanda: só os campos da ação pedida vêm preenchidos.</summary>
public sealed class OnDemandAiResponse
{
    public decimal? Quantidade { get; set; }
    public decimal? DescontoPct { get; set; }
    public decimal? DiasPromocao { get; set; }
    public string? Motivo { get; set; }
}

/// <summary>
/// Prompts da sugestão sob demanda (RN-DF30, a aprovar): uma variação em estoque baixo (quanto comprar) ou um lote vencendo (qual
/// desconto e por quantos dias). Mesmo princípio da análise (ADR 0005): o backend manda o cálculo e os fatos, a IA revisa dentro dos
/// limites e explica; o backend aplica os limites de novo na resposta.
/// </summary>
public static class OnDemandSuggestionPrompt
{
    /// <summary>Começo do prompt do usuário: identifica a chamada (os testes usam para responder com a IA fake).</summary>
    public const string UserPromptPrefix = "Sugestão sob demanda";

    public const int MaxReasonLength = 600;

    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
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
        Você é um analista sênior de compras e promoções no varejo brasileiro, apoiando o gestor de uma loja dentro de um ERP.
        Você recebe, em JSON, os fatos de UM item (estoque, vendas recentes, prazo, validade, preço e custo) e a sugestão já
        CALCULADA pelo sistema, e devolve a sua recomendação com uma explicação curta em português do Brasil.

        Regras obrigatórias:
        1. Use SOMENTE os números fornecidos. Não invente vendas, preços, datas ou eventos.
        2. "sugestaoCalculada" é a base. Mude só quando os dados justificarem (tendência das vendas, validade, prazo de entrega).
        3. Compra ("acao" = "comprar"): devolva "quantidade" entre "quantidadeMinima" e "quantidadeMaxima".
        4. Promoção ("acao" = "promover"): devolva "descontoPct" entre 5 e "descontoMaximoPct" e "diasPromocao" entre
           "diasMinimos" e "diasMaximos". Prefira o menor desconto que escoe o lote antes de vencer.
        5. "motivo": até 3 frases, citando os números que levaram à decisão. Não repita o JSON.
        """;

    public static string BuildUserPrompt(object payload)
        => $"{UserPromptPrefix}. Revise a sugestão abaixo e responda no schema.\n{JsonSerializer.Serialize(payload, PayloadJsonOptions)}";

    public static object ResponseSchema { get; } = new Dictionary<string, object>
    {
        ["type"] = "OBJECT",
        ["properties"] = new Dictionary<string, object>
        {
            ["quantidade"] = new Dictionary<string, object> { ["type"] = "NUMBER", ["nullable"] = true, ["description"] = "Quantidade a comprar (só na compra)." },
            ["descontoPct"] = new Dictionary<string, object> { ["type"] = "NUMBER", ["nullable"] = true, ["description"] = "Desconto em % (só na promoção)." },
            ["diasPromocao"] = new Dictionary<string, object> { ["type"] = "INTEGER", ["nullable"] = true, ["description"] = "Duração da promoção em dias (só na promoção)." },
            ["motivo"] = new Dictionary<string, object> { ["type"] = "STRING", ["description"] = "Explicação curta em pt-BR." }
        },
        ["required"] = new[] { "motivo" },
        ["propertyOrdering"] = new[] { "quantidade", "descontoPct", "diasPromocao", "motivo" }
    };

    /// <exception cref="FormatException">JSON ausente ou inválido.</exception>
    public static OnDemandAiResponse ParseResponse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new FormatException("Resposta da IA vazia.");
        try
        {
            return JsonSerializer.Deserialize<OnDemandAiResponse>(ForecastAiPrompt.ExtractJson(raw), ResponseJsonOptions)
                ?? throw new FormatException("Resposta da IA vazia.");
        }
        catch (JsonException ex)
        {
            throw new FormatException("Resposta da IA em JSON inválido.", ex);
        }
    }

    /// <summary>Motivo da IA aparado; vazio vira nulo (fica o motivo calculado).</summary>
    public static string? CleanReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return null;
        var text = reason.Trim();
        return text.Length <= MaxReasonLength ? text : text[..(MaxReasonLength - 1)] + "…";
    }
}
