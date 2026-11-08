using Nextflow.Domain.Models;

namespace Nextflow.Domain.Interfaces.Infrastructure.Repositories;

/// <summary>Transferências de estoque (RN-CE62): só inclui e lê, como o livro-razão.</summary>
public interface IStockTransferRepository
{
    Task AddAsync(StockTransfer transfer, CancellationToken ct = default);

    /// <summary>Com os locais (e a loja), os itens e a variação com o produto.</summary>
    Task<StockTransfer?> GetDetail(Guid id, CancellationToken ct = default);
}
