using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nextflow.Domain.Interfaces.Infrastructure.Services;

namespace Nextflow.Infrastructure.Services;

/// <summary>
/// Cliente REST do Google Gemini (<c>generateContent</c>, v1beta) com saída estruturada em JSON (<c>responseMimeType</c> +
/// <c>responseSchema</c>). Configuração na seção <c>Ai</c>: <c>Provider</c>, <c>ApiKey</c>, <c>Model</c>, <c>FallbackModel</c>,
/// <c>TimeoutSeconds</c>. A chave vai só no header <c>x-goog-api-key</c> e <b>nunca</b> em URL, log, mensagem de erro ou resposta
/// da API. Sem chave, <see cref="IsConfigured"/> é falso e a análise segue só com o cálculo (RN-DF13). HttpClient tipado: o
/// registro é do DI (uma instância por escopo, então <see cref="Model"/> reflete as chamadas da análise em curso).
/// <para>
/// O modelo principal pode ficar sobrecarregado (HTTP 503 "high demand", 429) ou travar a conexão por minutos antes de responder.
/// Por isso cada tentativa tem o seu prazo (<c>TimeoutSeconds</c>) e, se o principal não responder, a mesma chamada vai para o
/// <c>FallbackModel</c> antes de o lote cair para o cálculo.
/// </para>
/// </summary>
public class GeminiAiClient : IAiClient
{
    public const string DefaultModel = "gemini-flash-latest";
    public const string DefaultFallbackModel = "gemini-flash-lite-latest";
    public const int DefaultTimeoutSeconds = 60;
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models/";
    private const double Temperature = 0.2;

    /// <summary>Tentativas por modelo em caso de sobrecarga momentânea (429/500/503).</summary>
    public const int AttemptsPerModel = 2;

    private readonly HttpClient _httpClient;
    private readonly ILogger<GeminiAiClient> _logger;
    private readonly string _apiKey;
    private readonly int _timeoutSeconds;
    private readonly TimeSpan _retryDelay;
    private readonly IReadOnlyList<string> _models;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);
    public string ProviderName { get; }

    /// <summary>Modelo da última resposta bem-sucedida; antes dela, o modelo principal.</summary>
    public string Model { get; private set; }

    public GeminiAiClient(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiAiClient> logger)
        : this(httpClient, configuration, logger, TimeSpan.FromSeconds(2))
    {
    }

    internal GeminiAiClient(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiAiClient> logger, TimeSpan retryDelay)
    {
        _httpClient = httpClient;
        _logger = logger;
        _retryDelay = retryDelay;

        var section = configuration.GetSection("Ai");
        _apiKey = section["ApiKey"]?.Trim() ?? string.Empty;
        ProviderName = string.IsNullOrWhiteSpace(section["Provider"]) ? "Gemini" : section["Provider"]!.Trim();
        Model = string.IsNullOrWhiteSpace(section["Model"]) ? DefaultModel : section["Model"]!.Trim();
        var fallback = string.IsNullOrWhiteSpace(section["FallbackModel"]) ? DefaultFallbackModel : section["FallbackModel"]!.Trim();
        _models = string.Equals(fallback, Model, StringComparison.OrdinalIgnoreCase) ? [Model] : [Model, fallback];
        _timeoutSeconds = int.TryParse(section["TimeoutSeconds"], out var timeout) && timeout > 0 ? timeout : DefaultTimeoutSeconds;

        // O prazo efetivo é o de cada tentativa (CancellationToken abaixo); o HttpClient fica com folga.
        _httpClient.Timeout = TimeSpan.FromSeconds(_timeoutSeconds + 15);
    }

    public async Task<string> GenerateJsonAsync(string systemPrompt, string userPrompt, object responseSchema, CancellationToken ct)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("IA não configurada: defina Ai:ApiKey (AI_API_KEY).");

        var body = new
        {
            systemInstruction = new { parts = new[] { new { text = systemPrompt } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = userPrompt } } } },
            generationConfig = new
            {
                responseMimeType = "application/json",
                responseSchema,
                temperature = Temperature
            }
        };
        var json = JsonSerializer.Serialize(body);

        Exception? lastError = null;
        foreach (var model in _models)
        {
            for (var attempt = 1; attempt <= AttemptsPerModel; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                var outcome = await SendAsync(model, attempt, json, ct);
                if (outcome.Text != null)
                {
                    Model = model;
                    return outcome.Text;
                }

                lastError = outcome.Error;
                if (outcome.Next == NextStep.Fail)
                    throw outcome.Error!;
                if (outcome.Next == NextStep.NextModel)
                    break;

                if (attempt < AttemptsPerModel)
                    await Task.Delay(_retryDelay * attempt, ct);
            }

            if (model != _models[^1])
                _logger.LogWarning("{Provider} ({Model}) não respondeu; tentando o modelo reserva {Fallback}.",
                    ProviderName, model, _models[^1]);
        }

        throw lastError!;
    }

    private async Task<Outcome> SendAsync(string model, int attempt, string json, CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}{Uri.EscapeDataString(model)}:generateContent")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-goog-api-key", _apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var response = await _httpClient.SendAsync(request, timeoutCts.Token);
            var content = await response.Content.ReadAsStringAsync(timeoutCts.Token);
            var status = (int)response.StatusCode;

            _logger.LogInformation("{Provider} ({Model}): tentativa {Attempt}, HTTP {Status} em {Elapsed} ms ({Bytes} bytes enviados).",
                ProviderName, model, attempt, status, stopwatch.ElapsedMilliseconds, json.Length);

            if (response.IsSuccessStatusCode)
                return new Outcome(ExtractText(content), null, NextStep.Fail);

            var error = new HttpRequestException(
                $"{ProviderName} ({model}) retornou HTTP {status}: {ExtractErrorMessage(content)}", null, response.StatusCode);

            // Sobrecarga ou limite momentâneo: tenta de novo e depois o reserva. Modelo inexistente/descontinuado (404): direto
            // para o reserva. Chave inválida, requisição malformada etc.: o reserva falharia igual.
            if (IsTransient(status))
                return new Outcome(null, error, NextStep.Retry);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return new Outcome(null, error, NextStep.NextModel);
            return new Outcome(null, error, NextStep.Fail);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Um modelo que travou a conexão até o prazo costuma travar de novo: vai direto para o reserva.
            _logger.LogWarning("{Provider} ({Model}): tempo limite de {Timeout}s excedido na tentativa {Attempt}.",
                ProviderName, model, _timeoutSeconds, attempt);
            return new Outcome(null,
                new TimeoutException($"Tempo limite de {_timeoutSeconds}s excedido ao consultar a IA ({ProviderName} {model})."),
                NextStep.NextModel);
        }
    }

    private enum NextStep { Retry, NextModel, Fail }

    private sealed record Outcome(string? Text, Exception? Error, NextStep Next);

    private static bool IsTransient(int statusCode) => statusCode is 429 or 500 or 503;

    /// <summary>Extrai o texto de <c>candidates[0].content.parts</c>, tratando bloqueio, ausência de candidatos e truncamento.</summary>
    private static string ExtractText(string content)
    {
        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;

        if (root.TryGetProperty("promptFeedback", out var feedback) &&
            feedback.TryGetProperty("blockReason", out var blockReason))
            throw new InvalidOperationException($"Requisição bloqueada pela IA (motivo: {blockReason.GetString()}).");

        if (!root.TryGetProperty("candidates", out var candidates) ||
            candidates.ValueKind != JsonValueKind.Array ||
            candidates.GetArrayLength() == 0)
            throw new InvalidOperationException("A IA não retornou nenhuma resposta (sem candidates).");

        var candidate = candidates[0];
        var finishReason = candidate.TryGetProperty("finishReason", out var fr) ? fr.GetString() : null;

        var text = new StringBuilder();
        if (candidate.TryGetProperty("content", out var candidateContent) &&
            candidateContent.TryGetProperty("parts", out var parts) &&
            parts.ValueKind == JsonValueKind.Array)
        {
            foreach (var part in parts.EnumerateArray())
            {
                // Ignora partes de "pensamento" (modelos com thinking).
                if (part.TryGetProperty("thought", out var thought) && thought.ValueKind == JsonValueKind.True)
                    continue;
                if (part.TryGetProperty("text", out var t))
                    text.Append(t.GetString());
            }
        }

        if (text.Length == 0)
            throw new InvalidOperationException($"A IA retornou uma resposta vazia (finishReason: {finishReason ?? "desconhecido"}).");

        if (finishReason is "SAFETY" or "RECITATION" or "PROHIBITED_CONTENT" or "BLOCKLIST" or "SPII")
            throw new InvalidOperationException($"Resposta da IA bloqueada (finishReason: {finishReason}).");

        if (finishReason == "MAX_TOKENS")
            throw new InvalidOperationException("A resposta da IA foi truncada (limite de tokens atingido).");

        return text.ToString();
    }

    private static string ExtractErrorMessage(string content)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            if (document.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message))
                return Truncate(message.GetString() ?? string.Empty, 300);
        }
        catch (JsonException) { }

        return Truncate(content, 300);
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max] + "…";
}
