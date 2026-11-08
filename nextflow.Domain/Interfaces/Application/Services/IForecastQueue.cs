namespace Nextflow.Domain.Interfaces.Application.Services;

/// <summary>
/// Fila em memória das análises a processar (RN-DF23). Quem dispara só cria a análise em <c>Running</c> e enfileira o id; o job em
/// segundo plano lê a fila, uma análise por vez.
/// </summary>
public interface IForecastQueue
{
    ValueTask EnqueueAsync(Guid runId, CancellationToken ct = default);
    IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct);
}
