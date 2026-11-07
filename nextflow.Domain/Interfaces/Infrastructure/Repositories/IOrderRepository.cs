using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.DerivedData;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Interfaces.Infrastructure.Repositories;

public interface IOrderRepository : IBaseRepository<Order>
{
    /// <summary>Vendas pagas de cada cliente da lista: quantidade, total e a data do último pagamento.</summary>
    Task<Dictionary<Guid, PurchaseStatsDto>> GetClientStats(IReadOnlyCollection<Guid> clientIds, CancellationToken ct = default);

    /// <summary>Clientes com alguma venda paga a partir de <paramref name="since"/> (nulo = em qualquer data).</summary>
    Task<List<Guid>> GetClientIdsWithPurchase(DateTime? since, CancellationToken ct = default);

    /// <summary>Itens de pedidos Pagos, por produto (F3.1): base de <c>product_sales_monthly</c>. Só leitura.</summary>
    Task<List<SaleLineDto>> GetPaidSaleLines(CancellationToken ct = default);

    /// <summary>Quantidade vendida (pedidos Pagos) por variação desde o instante: base do rateio da grade (RN-DF21). Só leitura.</summary>
    Task<Dictionary<Guid, decimal>> GetPaidQuantityByVariant(DateTime sinceUtc, CancellationToken ct = default);
}
