using Nextflow.Domain.Dtos;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Interfaces.Infrastructure.Repositories;

public interface IOrderRepository : IBaseRepository<Order>
{
    /// <summary>Vendas pagas de cada cliente da lista: quantidade, total e a data do último pagamento.</summary>
    Task<Dictionary<Guid, PurchaseStatsDto>> GetClientStats(IReadOnlyCollection<Guid> clientIds, CancellationToken ct = default);

    /// <summary>Clientes com alguma venda paga a partir de <paramref name="since"/> (nulo = em qualquer data).</summary>
    Task<List<Guid>> GetClientIdsWithPurchase(DateTime? since, CancellationToken ct = default);
}
