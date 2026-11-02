using System.Linq.Expressions;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Interfaces.Infrastructure.Repositories;

/// <summary>
/// Livro-razão: só inclui e lê. Não herda <see cref="IBaseRepository{TModel}"/> de propósito, para não expor update nem
/// delete: movimentação nunca é editada nem apagada (RN-CE14).
/// </summary>
public interface IStockMovementRepository
{
    Task AddAsync(StockMovement movement, CancellationToken ct = default);
    Task<bool> Exists(Expression<Func<StockMovement, bool>> predicate, CancellationToken ct = default);
    Task<int> Count(Expression<Func<StockMovement, bool>> predicate, CancellationToken ct = default);
}
