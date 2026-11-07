using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nextflow.Domain.Interfaces.Utils;

namespace Nextflow.Application.Services.Ai;

/// <summary>
/// Cliente REST do Google Gemini (v1beta <c>generateContent</c>) com saída estruturada em JSON
/// (<c>responseMimeType = application/json</c> + <c>responseSchema</c>).
/// Configuração na seção <c>Ai</c>: Provider, ApiKey, Model, TimeoutSeconds. A chave nunca é logada.
/// </summary>
public class GeminiAiClient : IAiClient
{
    public const string DefaultModel = "gemini-flash-latest";
    public const int DefaultTimeoutSeconds = 60;
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models/";
    private const double Temperature = 0.2;

    private readonly HttpClient _httpClient;
    private readonly ILogger<GeminiAiClient> _logger;
    private readonly string _apiKey;
    private readonly int _timeoutSeconds;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);
    public string ProviderName { get; }
    public string Model { get; }

    public GeminiAiClient(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiAiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var section = configuration.GetSection("Ai");
        _apiKey = section["ApiKey"]?.Trim() ?? string.Empty;
        ProviderName = string.IsNullOrWhiteSpace(section["Provider"]) ? "Gemini" : section["Provider"]!.Trim();
        Model = string.IsNullOrWhiteSpace(section["Model"]) ? DefaultModel : section["Model"]!.Trim();
        _timeoutSeconds = int.TryParse(section["TimeoutSeconds"], out var timeout) && timeout > 0 ? timeout : DefaultTimeoutSeconds;

        // o timeout efetivo é controlado pelo CancellationToken abaixo (HttpClient fica com folga)
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

        // timeout total (todas as tentativas) = Ai:TimeoutSeconds
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));

        var stopwatch = Stopwatch.StartNew();
        try
        {
            for (var attempt = 1; ; attempt++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}{Uri.EscapeDataString(Model)}:generateContent")
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                request.Headers.Add("x-goog-api-key", _apiKey);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                using var response = await _httpClient.SendAsync(request, timeoutCts.Token);
                var content = await response.Content.ReadAsStringAsync(timeoutCts.Token);

                _logger.LogInformation("Gemini ({Model}): tentativa {Attempt}, HTTP {Status} em {Elapsed} ms ({Bytes} bytes enviados).",
                    Model, attempt, (int)response.StatusCode, stopwatch.ElapsedMilliseconds, json.Length);

                if (response.IsSuccessStatusCode)
                    return ExtractText(content);

                // Sobrecarga/limite momentâneo (429/500/503): nova tentativa com espera crescente, dentro do timeout total
                if (IsTransient((int)response.StatusCode) && attempt < MaxAttempts)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2 * attempt), timeoutCts.Token);
                    continue;
                }

                throw new HttpRequestException(
                    $"{ProviderName} retornou HTTP {(int)response.StatusCode}: {ExtractErrorMessage(content)}",
                    null,
                    response.StatusCode);
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Gemini ({Model}): tempo limite de {Timeout}s excedido.", Model, _timeoutSeconds);
            throw new TimeoutException($"Tempo limite de {_timeoutSeconds}s excedido ao consultar a IA ({ProviderName} {Model}).");
        }
    }

    private const int MaxAttempts = 3;

    private static bool IsTransient(int statusCode) => statusCode is 429 or 500 or 503;

    /// <summary>Extrai <c>candidates[0].content.parts[*].text</c>, tratando bloqueio e ausência de candidatos.</summary>
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
                // ignora partes de "pensamento" (modelos com thinking)
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
