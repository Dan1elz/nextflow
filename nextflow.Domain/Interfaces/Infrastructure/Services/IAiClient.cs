namespace Nextflow.Domain.Interfaces.Infrastructure.Services;

/// <summary>
/// Cliente da IA generativa da previsão (ADR 0005: o backend calcula, a IA só prioriza e explica). Atrás de uma interface para
/// trocar de provedor e para os testes nunca chamarem a IA real. Sem chave configurada, <see cref="IsConfigured"/> é falso e a
/// análise segue só com o cálculo (RN-DF13).
/// </summary>
public interface IAiClient
{
    bool IsConfigured { get; }

    /// <summary>Nome do provedor ("Gemini"); vai para a análise, nunca a chave.</summary>
    string ProviderName { get; }

    string Model { get; }

    /// <summary>Envia os prompts e devolve o JSON da resposta, no formato do schema. Qualquer falha lança: quem chama cai para o cálculo.</summary>
    Task<string> GenerateJsonAsync(string systemPrompt, string userPrompt, object responseSchema, CancellationToken ct);
}
