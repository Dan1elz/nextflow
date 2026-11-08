using Nextflow.Domain.Dtos.Forecasts;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Interfaces.Infrastructure.Repositories;

/// <summary>Recomendações por produto de cada análise (F3.2).</summary>
public interface IForecastRecommendationRepository : IBaseRepository<ForecastRecommendation>
{
    /// <summary>Contagem por ação e por status de cada análise (T-DF2, T-DF9); análise sem recomendação não aparece.</summary>
    Task<Dictionary<Guid, ForecastRecommendationCountsDto>> GetCountsByRun(IReadOnlyCollection<Guid> runIds, CancellationToken ct = default);

    /// <summary>
    /// Recomendações ligadas ao pedido (pela lista de pedidos, RN-DF28, ou pelo primeiro pedido), rastreadas para alterar, e para
    /// cada uma outro pedido dela que não está cancelado (nulo se não há). Base do cancelamento do pedido de compra.
    /// </summary>
    Task<List<(ForecastRecommendation Recommendation, Guid? StillOpenOrderId)>> GetAppliedToPurchaseOrderForUpdate(
        Guid purchaseOrderId, CancellationToken ct = default);
}
