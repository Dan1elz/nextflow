using Nextflow.Domain.Dtos;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Interfaces.Infrastructure.Repositories;

public interface IPurchaseOrderRepository : IBaseRepository<PurchaseOrder>
{
    /// <summary>Pedidos enviados ou recebidos de cada fornecedor: quantidade, total, último envio e prazo real médio.</summary>
    Task<Dictionary<Guid, PurchaseStatsDto>> GetSupplierStats(IReadOnlyCollection<Guid> supplierIds, CancellationToken ct = default);

    /// <summary>Fornecedores com pedido enviado a partir de <paramref name="since"/> (nulo = em qualquer data).</summary>
    Task<List<Guid>> GetSupplierIdsWithPurchase(DateTime? since, CancellationToken ct = default);

    /// <summary>Fornecedores com pedido enviado e ainda não recebido.</summary>
    Task<List<Guid>> GetSupplierIdsWithPendingOrders(CancellationToken ct = default);
}
