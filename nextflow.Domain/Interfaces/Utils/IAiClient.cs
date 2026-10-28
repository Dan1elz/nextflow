namespace Nextflow.Domain.Interfaces.Utils;

/// <summary>
/// Cliente de IA generativa (provedor atrás de uma interface — hoje Gemini).
/// Usado pelo módulo de previsão para priorizar e explicar as recomendações calculadas.
/// </summary>
public interface IAiClient
{
    /// <summary>true quando há chave de API configurada (sem chave → modo "sem IA").</summary>
    bool IsConfigured { get; }

    /// <summary>Nome do provedor (ex.: "Gemini").</summary>
    string ProviderName { get; }

    /// <summary>Modelo configurado (ex.: "gemini-flash-latest").</summary>
    string Model { get; }

    /// <summary>
    /// Gera uma resposta JSON aderente a <paramref name="responseSchema"/>.
    /// Qualquer falha (HTTP, timeout, resposta bloqueada/vazia) lança exceção — o chamador decide o fallback.
    /// </summary>
    Task<string> GenerateJsonAsync(string systemPrompt, string userPrompt, object responseSchema, CancellationToken ct);
}
