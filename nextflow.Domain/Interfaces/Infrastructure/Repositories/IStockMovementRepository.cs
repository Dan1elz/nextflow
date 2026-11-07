using System.Linq.Expressions;
using Nextflow.Domain.Dtos.DerivedData;
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

    /// <summary>Todas as movimentações até o instante, reduzidas ao necessário para o saldo diário (F3.1). Só leitura.</summary>
    Task<List<MovementDeltaDto>> GetDeltasUntil(DateTime untilUtc, CancellationToken ct = default);

    /// <summary>Página do kardex (T8), só leitura.</summary>
    Task<IEnumerable<StockMovement>> GetAll(
        Expression<Func<StockMovement, bool>> predicate,
        int offset,
        int limit,
        Func<IQueryable<StockMovement>, IQueryable<StockMovement>>? includeExpression = null,
        Func<IQueryable<StockMovement>, IOrderedQueryable<StockMovement>>? orderBy = null,
        CancellationToken ct = default);
}
