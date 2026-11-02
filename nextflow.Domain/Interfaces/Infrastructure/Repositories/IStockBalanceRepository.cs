using Nextflow.Domain.Models;

namespace Nextflow.Domain.Interfaces.Infrastructure.Repositories;

/// <summary>Leitura livre; escrita só pelo <c>IStockLedger</c> (RN-CE14).</summary>
public interface IStockBalanceRepository : IBaseRepository<StockBalance>
{
    /// <summary>Saldo físico de cada variação somando todos os locais (base do custo médio, RN-CE31). Variação sem saldo fica fora.</summary>
    Task<Dictionary<Guid, decimal>> SumByVariant(IReadOnlyCollection<Guid> variantIds, CancellationToken ct = default);

    /// <summary>Saldo físico de cada produto em locais ativos, só da loja informada ou da empresa (T1). Produto sem saldo fica fora.</summary>
    Task<Dictionary<Guid, decimal>> SumByProduct(IReadOnlyCollection<Guid> productIds, Guid? storeId, CancellationToken ct = default);
}
