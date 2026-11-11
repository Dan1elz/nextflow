using System.Text.Json;
using Nextflow.Application.Forecast;
using Nextflow.Domain.Interfaces.Infrastructure.Services;

namespace Nextflow.Tests.Support;

/// <summary>
/// IA fake: nenhum teste chama a IA real. Por padrão responde cada lote repetindo a sugestão calculada de cada produto (com o motivo
/// "IA: ...") e o resumo com um texto fixo. Registra as chamadas e a concorrência para provar que os lotes são sequenciais.
/// </summary>
public sealed class FakeAiClient : IAiClient
{
    public const string SummaryText = "Resumo da IA fake.";

    private int _inFlight;
    private int _batchCalls;

    public bool Configured { get; set; } = true;
    public bool IsConfigured => Configured;
    public string ProviderName => "Fake";
    public string Model => "fake-model";

    /// <summary>Números (1, 2, ...) dos lotes cuja chamada deve falhar.</summary>
    public HashSet<int> FailBatches { get; } = [];

    public bool FailSummary { get; set; }

    /// <summary>Motivo devolvido na sugestão sob demanda (RN-DF30).</summary>
    public const string OnDemandReason = "IA: sugestão sob demanda.";

    /// <summary>Resposta da sugestão sob demanda; nulo usa a padrão, que pede valores fora dos limites para provar o corte.</summary>
    public string? OnDemandResponse { get; set; }

    public int OnDemandCalls => Calls.Count(c => c == "on-demand");

    /// <summary>Substitui a resposta de um lote: recebe o número do lote e os ids do payload e devolve o JSON.</summary>
    public Func<int, IReadOnlyList<Guid>, string?>? BatchResponse { get; set; }

    public List<IReadOnlyList<Guid>> BatchProductIds { get; } = [];
    public List<string> Calls { get; } = [];

    /// <summary>Prompts recebidos, na ordem: os testes de contrato conferem os campos enviados (LGPD).</summary>
    public List<string> UserPrompts { get; } = [];
    public int MaxConcurrentCalls { get; private set; }
    public int SummaryCalls => Calls.Count(c => c == "summary");

    public void Reset()
    {
        Configured = true;
        FailBatches.Clear();
        FailSummary = false;
        BatchResponse = null;
        OnDemandResponse = null;
        BatchProductIds.Clear();
        Calls.Clear();
        UserPrompts.Clear();
        MaxConcurrentCalls = 0;
        _batchCalls = 0;
    }

    public async Task<string> GenerateJsonAsync(string systemPrompt, string userPrompt, object responseSchema, CancellationToken ct)
    {
        lock (UserPrompts)
            UserPrompts.Add(userPrompt);
        var inFlight = Interlocked.Increment(ref _inFlight);
        MaxConcurrentCalls = Math.Max(MaxConcurrentCalls, inFlight);
        try
        {
            await Task.Delay(5, ct); // dá chance a uma chamada concorrente de aparecer

            if (userPrompt.StartsWith("Escreva o resumo", StringComparison.Ordinal))
            {
                Calls.Add("summary");
                if (FailSummary)
                    throw new InvalidOperationException("Falha simulada no resumo.");
                return JsonSerializer.Serialize(new { resumoGeral = SummaryText });
            }

            if (userPrompt.StartsWith(OnDemandSuggestionPrompt.UserPromptPrefix, StringComparison.Ordinal))
            {
                Calls.Add("on-demand");
                return OnDemandResponse
                    ?? JsonSerializer.Serialize(new { quantidade = 1_000_000m, descontoPct = 99m, diasPromocao = 999, motivo = OnDemandReason });
            }

            var number = Interlocked.Increment(ref _batchCalls);
            var payload = JsonDocument.Parse(userPrompt[(userPrompt.IndexOf('\n') + 1)..]).RootElement;
            var products = payload.GetProperty("produtos").EnumerateArray().ToList();
            var ids = products.Select(p => Guid.Parse(p.GetProperty("id").GetString()!)).ToList();
            BatchProductIds.Add(ids);
            Calls.Add($"batch:{number}");

            if (FailBatches.Contains(number))
                throw new InvalidOperationException($"Falha simulada no lote {number}.");

            if (BatchResponse?.Invoke(number, ids) is { } custom)
                return custom;

            var items = products.Select(p =>
            {
                var suggestion = p.GetProperty("sugestaoCalculada");
                return new
                {
                    produtoId = p.GetProperty("id").GetString(),
                    acao = suggestion.GetProperty("acao").GetString(),
                    prioridade = suggestion.GetProperty("prioridade").GetString(),
                    quantidade = suggestion.GetProperty("quantidade").ValueKind == JsonValueKind.Number ? suggestion.GetProperty("quantidade").GetDecimal() : (decimal?)null,
                    descontoPct = suggestion.GetProperty("descontoPct").ValueKind == JsonValueKind.Number ? suggestion.GetProperty("descontoPct").GetDecimal() : (decimal?)null,
                    diasPromocao = suggestion.GetProperty("diasPromocao").ValueKind == JsonValueKind.Number ? suggestion.GetProperty("diasPromocao").GetInt32() : (int?)null,
                    motivo = "IA: " + p.GetProperty("nome").GetString()
                };
            });
            return JsonSerializer.Serialize(new { recomendacoes = items });
        }
        finally
        {
            Interlocked.Decrement(ref _inFlight);
        }
    }
}
