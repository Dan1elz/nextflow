using Nextflow.Domain.Dtos;
using Nextflow.Domain.Interfaces.Repositories.Base;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Interfaces.Repositories;

public interface IProductSalesMonthlyRepository : IBaseRepository<ProductSalesMonthly>
{
    /// <summary>Remove todos os registros da agregação (rebuild completo). Retorna a quantidade removida.</summary>
    Task<int> DeleteAllAsync(CancellationToken ct);

    /// <summary>
    /// Agrega (no banco) as vendas confirmadas (<c>Order.Status == PaymentConfirmed</c>) por produto e mês,
    /// com o mês calculado a partir de <c>Order.CreateAt</c> convertido para o fuso de negócio (America/Sao_Paulo).
    /// Revenue = Σ(UnitPrice × Quantity − Discount); OrdersCount = pedidos distintos.
    /// </summary>
    /// <param name="fromUtc">Início (UTC, inclusive) do período.</param>
    /// <param name="productId">Opcional: restringe a um produto.</param>
    Task<List<ProductMonthlySalesAggregateDto>> AggregateConfirmedSalesAsync(DateTime fromUtc, Guid? productId, CancellationToken ct);
}
