namespace Nextflow.Domain.Interfaces.Application.Services;

/// <summary>
/// Processa uma análise enfileirada (F3.3): recalcula os dados derivados (F3.1), roda o motor (F3.2) e, ao terminar, avisa
/// (F3.5). Falha em qualquer passo fecha a análise como <c>Failed</c>; nunca deixa a análise presa em <c>Running</c>.
/// </summary>
public interface IForecastRunProcessor
{
    Task ProcessAsync(Guid runId, CancellationToken ct);

    /// <summary>Análise que ficou <c>Running</c> porque a API caiu no meio: fecha como <c>Failed</c> na subida (RN-DF23).</summary>
    Task FailInterruptedAsync(CancellationToken ct);
}
