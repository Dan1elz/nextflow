using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Exceptions;
using Nextflow.Domain.Interfaces.Repositories.Base;
using Nextflow.Infrastructure.Database;
using System.Linq.Expressions;
namespace Nextflow.Infrastructure.Repositories.Base;

public abstract class BaseRepository<TEntity>(AppDbContext context) : IBaseRepository<TEntity> where TEntity : class
{
    protected readonly AppDbContext _context = context;
    protected readonly DbSet<TEntity> DbSet = context.Set<TEntity>();
    public virtual async Task AddAsync(TEntity entity, CancellationToken ct)
    {
        await _context.Set<TEntity>().AddAsync(entity, ct);
        await _context.SaveChangesAsync(ct);
    }
    public virtual async Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct)
    {
        return await _context.Set<TEntity>().AnyAsync(predicate, ct);
    }
    public virtual async Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct)
    {
        await _context.Set<TEntity>().AddRangeAsync(entities, ct);
        await _context.SaveChangesAsync(ct);
    }
    public virtual async Task<IEnumerable<TEntity>> GetAllAsync(Expression<Func<TEntity, bool>> predicate, int offset, int limit, CancellationToken ct, Func<IQueryable<TEntity>, IQueryable<TEntity>>? includeExpression = null)
    {
        IQueryable<TEntity> query = _context.Set<TEntity>().Where(predicate);

        if (includeExpression != null)
            query = includeExpression(query);

        return await query.OrderByDescending(e => EF.Property<DateTime>(e, "CreateAt")).Skip(offset).Take(limit).ToListAsync(ct);
    }
    public virtual async Task<TEntity?> GetAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct, Func<IQueryable<TEntity>, IQueryable<TEntity>>? includeExpression = null)
    {
        IQueryable<TEntity> query = _context.Set<TEntity>().Where(predicate);

        if (includeExpression != null)
            query = includeExpression(query);

        return await query.FirstOrDefaultAsync(ct);
    }
    public virtual async Task<TEntity?> GetByIdAsync(Guid Id, CancellationToken ct, Func<IQueryable<TEntity>, IQueryable<TEntity>>? includeExpression = null)
    {
        IQueryable<TEntity> query = _context.Set<TEntity>().Where(e => EF.Property<Guid>(e, "Id") == Id);

        if (includeExpression != null)
            query = includeExpression(query);

        return await query.FirstOrDefaultAsync(ct);
    }
    public virtual async Task<int> CountAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct)
    {
        return await _context.Set<TEntity>().CountAsync(predicate, ct);
    }
    public virtual async Task RemoveAsync(TEntity entity, CancellationToken ct)
    {
        _context.Set<TEntity>().Remove(entity);
        await _context.SaveChangesAsync(ct);
    }
    public virtual async Task RemoveRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct)
    {
        _context.Set<TEntity>().RemoveRange(entities);
        await _context.SaveChangesAsync(ct);
    }
    public virtual async Task UpdateAsync(TEntity entity, CancellationToken ct)
    {
        _context.Set<TEntity>().Update(entity);
        await _context.SaveChangesAsync(ct);
    }
    public virtual async Task UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct)
    {
        if (entities == null || !entities.Any())
            throw new NotFoundException("A lista de entidades não pode estar vazia: " + nameof(entities));

        _context.Set<TEntity>().UpdateRange(entities);
        await _context.SaveChangesAsync(ct);
    }
    public virtual async Task SaveAsync(CancellationToken ct)
    {
        await _context.SaveChangesAsync(ct);
    }
}
