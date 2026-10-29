using System.Linq.Expressions;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Nextflow.Infrastructure.Repositories;

public abstract class BaseRepository<TModel>(AppDbContext context) : IBaseRepository<TModel> where TModel : BaseModel
{
    protected readonly AppDbContext _context = context;

    public virtual async Task<IEnumerable<TModel>> GetAll(
        Expression<Func<TModel, bool>> predicate,
        int offset,
        int limit,
        Func<IQueryable<TModel>, IQueryable<TModel>>? includeExpression = null,
        Func<IQueryable<TModel>, IOrderedQueryable<TModel>>? orderBy = null,
        CancellationToken ct = default)
    {
        IQueryable<TModel> query = _context.Set<TModel>().AsNoTracking().Where(predicate);

        if (includeExpression != null)
            query = includeExpression(query);

        var orderedQuery = orderBy != null
            ? orderBy(query)
            : query.OrderByDescending(e => e.CreatedAt).ThenBy(e => e.Id);

        return await orderedQuery
            .Skip(offset)
            .Take(limit)
            .ToListAsync(ct);
    }

    public virtual async Task<TModel?> Get(
        Expression<Func<TModel, bool>> predicate,
        Func<IQueryable<TModel>, IQueryable<TModel>>? includeExpression = null,
        CancellationToken ct = default)
    {
        IQueryable<TModel> query = _context.Set<TModel>().AsNoTracking().Where(predicate);

        if (includeExpression != null)
            query = includeExpression(query);

        return await query.FirstOrDefaultAsync(ct);
    }

    public virtual async Task<TModel?> GetById(
        Guid id,
        Func<IQueryable<TModel>, IQueryable<TModel>>? includeExpression = null,
        CancellationToken ct = default)
    {
        var query = _context.Set<TModel>().AsNoTracking().AsQueryable();

        if (includeExpression != null)
            query = includeExpression(query);

        return await query.FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    public virtual async Task<TModel?> GetByIdForUpdate(
        Guid id,
        Func<IQueryable<TModel>, IQueryable<TModel>>? includeExpression = null,
        CancellationToken ct = default)
    {
        var query = _context.Set<TModel>().AsQueryable();

        if (includeExpression != null)
            query = includeExpression(query);

        return await query.FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    public virtual async Task<List<TModel>> GetAllForUpdate(Expression<Func<TModel, bool>> predicate, CancellationToken ct = default)
        => await _context.Set<TModel>().Where(predicate).ToListAsync(ct);

    public virtual async Task<bool> Exists(Expression<Func<TModel, bool>> predicate, CancellationToken ct = default)
        => await _context.Set<TModel>().AsNoTracking().AnyAsync(predicate, ct);

    public virtual async Task<int> Count(Expression<Func<TModel, bool>> predicate, CancellationToken ct = default)
        => await _context.Set<TModel>().AsNoTracking().CountAsync(predicate, ct);

    public virtual async Task AddAsync(TModel model, CancellationToken ct = default)
        => await _context.Set<TModel>().AddAsync(model, ct);

    public virtual async Task AddRangeAsync(IEnumerable<TModel> models, CancellationToken ct = default)
        => await _context.Set<TModel>().AddRangeAsync(models, ct);

    public virtual void Update(TModel model)
        => _context.Set<TModel>().Update(model);

    public virtual void UpdateRange(IEnumerable<TModel> models)
        => _context.Set<TModel>().UpdateRange(models);

    public virtual void Remove(TModel model)
        => _context.Set<TModel>().Remove(model);

    public virtual void RemoveRange(IEnumerable<TModel> models)
        => _context.Set<TModel>().RemoveRange(models);
}
