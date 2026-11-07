using Nextflow.Domain.Models;

namespace Nextflow.Domain.Interfaces.Infrastructure.Repositories;

/// <summary>Leitura dos itens fora do agregado: a quantidade "a caminho" (RN-CP05). A escrita passa pelo <c>PurchaseOrder</c>.</summary>
public interface IPurchaseItemRepository : IBaseRepository<PurchaseItem>
{
    /// <summary>Quantidade de cada variação em pedidos Orçamento ou Pendente com entrega em local da loja (RN-CP05).</summary>
    Task<Dictionary<Guid, decimal>> SumIncomingByVariant(IReadOnlyCollection<Guid> variantIds, Guid storeId, CancellationToken ct = default);
}
