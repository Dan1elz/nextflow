using System.Linq.Expressions;

namespace Nextflow.Domain.Interfaces.Repositories.Base;

public interface IBaseRepository<TEntity> where TEntity : class
{
    // Metodos de leitura
    Task<TEntity?> GetByIdAsync(Guid Id, CancellationToken ct, Func<IQueryable<TEntity>, IQueryable<TEntity>>? includeExpression = null);
    Task<IEnumerable<TEntity>> GetAllAsync(Expression<Func<TEntity, bool>> predicate, int offset, int limit, CancellationToken ct, Func<IQueryable<TEntity>, IQueryable<TEntity>>? includeExpression = null);
    Task<TEntity?> GetAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct, Func<IQueryable<TEntity>, IQueryable<TEntity>>? includeExpression = null);
    Task<int> CountAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct);
    Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct);
    // Metodos de escrita
    Task AddAsync(TEntity entity, CancellationToken ct);
    Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct);
    Task UpdateAsync(TEntity entity, CancellationToken ct);
    Task UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct);
    Task RemoveAsync(TEntity entity, CancellationToken ct);
    Task RemoveRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}
