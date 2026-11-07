using Nextflow.Domain.Dtos;
using Nextflow.Domain.Interfaces.Repositories.Base;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Interfaces.Repositories;

public interface IPurchaseOrderRepository : IBaseRepository<PurchaseOrder>
{
    /// <summary>
    /// Estoque a chegar por produto: Σ quantidade dos itens de pedidos de compra ativos em Orçamento ou Pendente.
    /// </summary>
    Task<Dictionary<Guid, decimal>> GetIncomingStockByProductAsync(CancellationToken ct);

    /// <summary>Amostras de lead time real (pedidos recebidos com <c>ReceivedAt</c>) criados a partir de <paramref name="sinceUtc"/>.</summary>
    Task<List<PurchaseLeadTimeSampleDto>> GetReceivedLeadTimeSamplesAsync(DateTime sinceUtc, CancellationToken ct);
}
