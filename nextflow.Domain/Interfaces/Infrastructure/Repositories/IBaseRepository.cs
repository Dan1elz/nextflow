using System.Linq.Expressions;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Interfaces.Infrastructure.Repositories;

public interface IBaseRepository<TModel> where TModel : BaseModel
{
    Task<IEnumerable<TModel>> GetAll(Expression<Func<TModel, bool>> predicate, int offset, int limit, Func<IQueryable<TModel>, IQueryable<TModel>>? includeExpression = null, Func<IQueryable<TModel>, IOrderedQueryable<TModel>>? orderBy = null, CancellationToken ct = default);
    Task<TModel?> Get(Expression<Func<TModel, bool>> predicate, Func<IQueryable<TModel>, IQueryable<TModel>>? includeExpression = null, CancellationToken ct = default);
    Task<bool> Exists(Expression<Func<TModel, bool>> predicate, CancellationToken ct = default);
    Task<int> Count(Expression<Func<TModel, bool>> predicate, CancellationToken ct = default);
    Task<TModel?> GetById(Guid id, Func<IQueryable<TModel>, IQueryable<TModel>>? includeExpression = null, CancellationToken ct = default);
    /// <summary>
    /// Carrega a entidade RASTREADA, para alterar e commitar sem chamar Update(): o EF grava só
    /// as colunas que mudaram. Update() num grafo solto marca tudo como alterado e sobrescreve o
    /// que outra requisição gravou no meio.
    /// </summary>
    Task<TModel?> GetByIdForUpdate(Guid id, Func<IQueryable<TModel>, IQueryable<TModel>>? includeExpression = null, CancellationToken ct = default);
    /// <summary>Carrega RASTREADAS as entidades do predicado, para alterar várias no mesmo commit.</summary>
    Task<List<TModel>> GetAllForUpdate(Expression<Func<TModel, bool>> predicate, CancellationToken ct = default);
    Task AddAsync(TModel model, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<TModel> models, CancellationToken ct = default);
    void Update(TModel model);
    void UpdateRange(IEnumerable<TModel> models);
    void Remove(TModel model);
    void RemoveRange(IEnumerable<TModel> models);
}
