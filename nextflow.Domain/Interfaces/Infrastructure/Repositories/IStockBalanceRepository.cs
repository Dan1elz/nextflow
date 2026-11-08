using Nextflow.Domain.Dtos.Dashboard;
using Nextflow.Domain.Dtos.Forecasts;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Interfaces.Infrastructure.Repositories;

/// <summary>Leitura livre; escrita só pelo <c>IStockLedger</c> (RN-CE14).</summary>
public interface IStockBalanceRepository : IBaseRepository<StockBalance>
{
    /// <summary>Saldo físico de cada variação somando todos os locais (base do custo médio, RN-CE31). Variação sem saldo fica fora.</summary>
    Task<Dictionary<Guid, decimal>> SumByVariant(IReadOnlyCollection<Guid> variantIds, CancellationToken ct = default);

    /// <summary>Saldo físico de cada produto em locais ativos, só da loja informada ou da empresa (T1). Produto sem saldo fica fora.</summary>
    Task<Dictionary<Guid, decimal>> SumByProduct(IReadOnlyCollection<Guid> productIds, Guid? storeId, CancellationToken ct = default);

    /// <summary>
    /// Produtos com estoque baixo (RN-CE33): alguma variação ativa, em algum local ativo de loja ativa (da loja informada ou
    /// da empresa), com disponível abaixo do mínimo efetivo (o do local ou, sem ele, o padrão do produto). Local sem saldo
    /// conta como zero. Sem <paramref name="productIds"/>, olha todos os produtos ativos.
    /// </summary>
    Task<HashSet<Guid>> GetLowStockProductIds(IReadOnlyCollection<Guid>? productIds, Guid? storeId, CancellationToken ct = default);

    /// <summary>Saldo positivo por variação e lote com validade, somando os locais (previsão, RN-DF07). Só leitura.</summary>
    Task<List<ExpiringLotBalanceDto>> GetLotBalances(CancellationToken ct = default);

    /// <summary>
    /// Variações × locais abaixo do mínimo (RN-CE33, mesma regra de <see cref="GetLowStockProductIds"/>): o total e as
    /// <paramref name="top"/> mais críticas (menor disponível em relação ao mínimo). Painel e resumo diário (T-DF1, L-DF4).
    /// </summary>
    Task<(int Count, List<LowStockRowDto> Top)> GetLowStock(Guid? storeId, int top, CancellationToken ct = default);

    /// <summary>Saldos positivos de lote com validade de hoje até <paramref name="until"/>, por local ativo (T-DF1, L-DF4).</summary>
    Task<List<ExpiringLotRowDto>> GetExpiringLots(DateOnly from, DateOnly until, Guid? storeId, CancellationToken ct = default);
}
