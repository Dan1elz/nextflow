using Nextflow.Domain.Dtos.Forecasts;

namespace Nextflow.Domain.Interfaces.Application.Services;

/// <summary>
/// Motor de previsão (F3.2): dado uma análise em andamento, lê os dados derivados (F3.1), o livro-razão e os pedidos de compra,
/// roda o cálculo e a IA em lotes sequenciais e grava projeções, recomendações e lotes. Quem cria a análise, enfileira e
/// agenda é a F3.3; aplicar as recomendações é a F3.4.
/// <para>
/// <b>Pré-requisito da F3.3:</b> chamar <see cref="IDerivedDataService.RebuildAsync"/> antes de <see cref="RunAsync"/>. O motor lê
/// <c>product_sales_monthly</c> e <c>product_seasonality</c> como estão e não os recalcula.
/// </para>
/// </summary>
public interface IForecastEngine
{
    /// <summary>
    /// Executa a análise <paramref name="runId"/> (status <c>Running</c>). A falha da IA nunca derruba a análise (RN-DF13); falha do
    /// cálculo ou do banco a fecha como <c>Failed</c>; cancelamento também fecha como <c>Failed</c> (senão o índice único de
    /// run <c>Running</c>, RN-DF23, bloquearia a próxima) e relança o cancelamento. Idempotente só no sentido de recusar análise que já terminou.
    /// </summary>
    Task<ForecastRunOutcomeDto> RunAsync(Guid runId, CancellationToken ct);
}
